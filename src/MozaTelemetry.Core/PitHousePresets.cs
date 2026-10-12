using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace MozaTelemetry.Core;

public enum PresetOutlook
{
    /// <summary>Pit House is expected to load a different Motor preset when it detects the game.</summary>
    Switches,
    /// <summary>The preset Pit House is expected to load is the one already selected.</summary>
    Same,
    /// <summary>Pit House's Motor auto-load option is off.</summary>
    AutoLoadOff,
    /// <summary>Missing, unreadable or ambiguous data: no claim either way.</summary>
    Unknown,
}

public sealed record MotorPreset(string Id, string Name, int? SteeringAngle, int? MaximumTorque, int? GameForceFeedback)
{
    public string Describe() => Name + (SteeringAngle is null && MaximumTorque is null ? "" :
        $" ({string.Join(", ", new[] { SteeringAngle is { } a ? $"{a}°" : null, MaximumTorque is { } t ? $"{t}% torque" : null }.OfType<string>())})");
}

public sealed record PresetPrediction(PresetOutlook Outlook, MotorPreset? Current, MotorPreset? Predicted, string Reason)
{
    public string Summary => Outlook switch
    {
        PresetOutlook.Switches => $"Pit House is expected to switch your wheel base from {Current!.Describe()} to {Predicted!.Describe()}. It stays selected afterwards until you change it in Pit House.",
        PresetOutlook.Same => $"Pit House is expected to keep {Current!.Describe()}.",
        PresetOutlook.AutoLoadOff => "Pit House's Motor preset auto-load is off, so it should not switch presets.",
        _ => "Pit House may switch your wheel-base preset: " + Reason,
    };
}

/// <summary>The Motor preset Pit House last saved as selected. A saved selection is not a readback of the device.</summary>
public sealed record PresetSnapshot(MotorPreset? Motor, string? Problem);

/// <summary>Read-only view of MOZA Pit House's saved presets (Documents\MOZA Pit House). Never writes there.</summary>
public static class PitHousePresets
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MOZA Pit House");

    public static bool IsPresent(string? root = null) => Directory.Exists(Path.Combine(root ?? DefaultRoot, "Presets"));

    public static PresetPrediction Predict(string identity, string? root = null)
    {
        root ??= DefaultRoot;
        try
        {
            var exe = ProcessIdentity.NormalizeName(identity);
            var config = ReadIni(Path.Combine(root, "Presets", "config.ini"));
            if (config is null) return Unknown("its preset settings were not found.");
            var autoLoad = Value(config, "IsAutoLoadPreset", "Motor");
            if (string.Equals(autoLoad, "false", StringComparison.OrdinalIgnoreCase)) return new(PresetOutlook.AutoLoadOff, null, null, "auto-load off");
            if (!string.Equals(autoLoad, "true", StringComparison.OrdinalIgnoreCase)) return Unknown("its Motor auto-load setting could not be read.");

            var (presets, unreadable) = ReadMotorPresets(root);
            if (unreadable > 0) return Unknown($"{unreadable} saved Motor preset(s) could not be read.");
            var current = CurrentMotor(config, presets);
            if (current is null) return Unknown("the currently selected Motor preset could not be identified.");
            var model = current.Tags.FirstOrDefault();
            if (model is null) return Unknown("the wheel-base model of the current preset is not recorded.");

            var game = GameName(root, exe);
            if (game is null) return Unknown($"Pit House does not list {exe} as a game it recognizes.");
            // Pit House's game id is not in GameConfigInfo (rank is alphabetical). Take it from presets tagged with the
            // game that are bound to exactly one game; cross-game presets (FH4+FH5+FH6) say nothing about which is which.
            var ids = presets.Where(p => p.Games.Length == 1 && p.Tags.Contains(game, StringComparer.OrdinalIgnoreCase)).Select(p => p.Games[0]).Distinct().ToArray();
            if (ids.Length != 1) return Unknown($"the preset binding for {game} is {(ids.Length == 0 ? "not recorded" : "ambiguous")}.");
            var gameId = ids[0];

            Entry? predicted;
            if (Value(config, "DefaultGamePreset_" + gameId, model) is { } chosen)
            {
                predicted = presets.FirstOrDefault(p => p.Id.Equals(chosen, StringComparison.OrdinalIgnoreCase));
                if (predicted is null) return Unknown($"your default {game} preset is missing.");
            }
            else
            {
                var bound = presets.Where(p => p.Games.Contains(gameId) && p.Tags.Contains(model, StringComparer.OrdinalIgnoreCase)).ToArray();
                if (bound.Length != 1) return Unknown(bound.Length == 0
                    ? $"no {model} preset is bound to {game}, and Pit House's choice in that case is not known."
                    : $"{bound.Length} {model} presets are bound to {game}: {string.Join(", ", bound.Select(p => p.Name))}.");
                predicted = bound[0];
            }
            return predicted.Id.Equals(current.Id, StringComparison.OrdinalIgnoreCase)
                ? new(PresetOutlook.Same, current.Preset, predicted.Preset, "already selected")
                : new(PresetOutlook.Switches, current.Preset, predicted.Preset, $"bound to {game}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException)
        {
            return Unknown(e.Message);
        }

        static PresetPrediction Unknown(string reason) => new(PresetOutlook.Unknown, null, null, reason);
    }

    public static PresetSnapshot Snapshot(string? root = null)
    {
        root ??= DefaultRoot;
        try
        {
            var config = ReadIni(Path.Combine(root, "Presets", "config.ini"));
            if (config is null) return new(null, "Pit House preset settings were not found.");
            var (presets, _) = ReadMotorPresets(root);
            return CurrentMotor(config, presets) is { } current ? new(current.Preset, null) : new(null, "The selected Motor preset could not be identified.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(null, e.Message); }
    }

    /// <summary>A message when the saved Motor selection differs from <paramref name="before"/>, otherwise null.
    /// Unknown on either side is reported, never treated as "unchanged".</summary>
    public static string? DescribeChange(PresetSnapshot before, PresetSnapshot after)
    {
        if (before.Motor is null || after.Motor is null)
        {
            var problem = after.Problem ?? before.Problem;
            return "Pit House's saved wheel-base preset could not be compared" + (problem is null ? "." : $": {problem}") + " Check the active preset in Pit House.";
        }
        if (before.Motor.Id.Equals(after.Motor.Id, StringComparison.OrdinalIgnoreCase)) return null;
        return $"Pit House's saved wheel-base preset changed from {before.Motor.Describe()} to {after.Motor.Describe()} and was not restored. Reselect {before.Motor.Name} in Pit House if you want it back.";
    }

    private sealed record Entry(string Id, string Name, string[] Tags, string[] Games, MotorPreset Preset);

    private static Entry? CurrentMotor(Dictionary<string, Dictionary<string, string>> config, List<Entry> presets)
    {
        if (!config.TryGetValue("LastUsedPreset", out var used)) return null;
        var motor = used.Values.Select(id => presets.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).OfType<Entry>().Distinct().ToArray();
        return motor.Length == 1 ? motor[0] : null;
    }

    private static (List<Entry> Presets, int Unreadable) ReadMotorPresets(string root)
    {
        var list = new List<Entry>();
        var unreadable = 0;
        var folder = Path.Combine(root, "Presets", "Motor");
        if (!Directory.Exists(folder)) return (list, 0);
        foreach (var file in Directory.EnumerateFiles(folder, "*.mzpreset"))
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = zip.GetEntry("preset.json") ?? throw new InvalidDataException("No preset.json.");
                using var json = JsonDocument.Parse(entry.Open());
                var rootElement = json.RootElement;
                var name = rootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : Path.GetFileNameWithoutExtension(file);
                var tags = rootElement.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
                    ? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
                var games = rootElement.TryGetProperty("games", out var g) && g.ValueKind == JsonValueKind.Object
                    ? g.EnumerateObject().Select(x => x.Name).ToArray() : [];
                int? Param(string key) => rootElement.TryGetProperty("deviceParams", out var d) && d.ValueKind == JsonValueKind.Object
                    && d.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
                var id = Path.GetFileNameWithoutExtension(file);
                list.Add(new Entry(id, name, tags, games, new MotorPreset(id, name, Param("maximumSteeringAngle"), Param("maximumTorque"), Param("gameForceFeedbackStrength"))));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException) { unreadable++; }
        }
        return (list, unreadable);
    }

    private static string? GameName(string root, string exe)
    {
        var path = Path.Combine(root, "GameConfigInfo.xml");
        if (!File.Exists(path)) return null;
        var games = XDocument.Load(path).Descendants("Game")
            .Where(game => string.Equals((string?)game.Element("program"), exe, StringComparison.OrdinalIgnoreCase))
            .Select(game => ((string?)game.Element("name"))?.Trim()).OfType<string>().Distinct().ToArray();
        return games.Length == 1 ? games[0] : null;
    }

    private static Dictionary<string, Dictionary<string, string>>? ReadIni(string path)
    {
        if (!File.Exists(path)) return null;
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;
            if (line[0] == '[' && line[^1] == ']') { current = sections.TryGetValue(line[1..^1], out var s) ? s : sections[line[1..^1]] = new(StringComparer.OrdinalIgnoreCase); continue; }
            var equals = line.IndexOf('=');
            if (current != null && equals > 0) current[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        return sections;
    }

    private static string? Value(Dictionary<string, Dictionary<string, string>> ini, string section, string key) =>
        ini.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) && v.Length > 0 ? v : null;
}
