using System.Text.Json;
using MozaTelemetry.Core;

namespace MozaTelemetry.App;

public sealed record AppSettings
{
    public string ProcessName { get; init; } = "ForzaHorizon5.exe";
    public bool RunProcess { get; init; } = true;
    public bool StartWithWindows { get; init; }
    public bool CloseToTray { get; init; }
    public BridgeOptions Bridge { get; init; } = new();
    public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MozaTelemetryHelper", "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? throw new InvalidDataException("Settings file is empty.");
        ProcessIdentity.NormalizeName(settings.ProcessName);
        if (settings.Bridge == null) throw new InvalidDataException("Missing bridge settings.");
        settings.Bridge.Validate();
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, true);
    }
}
