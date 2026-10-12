using System.IO.Compression;
using System.Text.Json;
using MozaTelemetry.Core;

namespace MozaTelemetry.Tests;

// Synthetic Pit House folders only; the owner's real presets are never read by tests.
public sealed class PitHousePresetTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "MozaPitHouse-" + Guid.NewGuid().ToString("N"));
    private const string Arcade = "6218f6b0-0000-0000-0000-000000000001";
    private const string Fh5Official = "6ba3b07a-0000-0000-0000-000000000002";
    private const string Dr2Custom = "7d417cbb-0000-0000-0000-000000000003";

    public PitHousePresetTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "Presets", "Motor"));
        File.WriteAllText(Path.Combine(root, "GameConfigInfo.xml"),
            "<Game_Config><Game><name>DiRT Rally 2.0</name><rank>15</rank><path></path><program>dirtrally2.exe</program></Game>" +
            "<Game><name>Forza Horizon 4</name><rank>30</rank><path></path><program>ForzaHorizon4.exe</program></Game>" +
            "<Game><name>Forza Horizon 5</name><rank>31</rank><path></path><program>ForzaHorizon5.exe</program></Game></Game_Config>");
        Preset(Arcade, "Arcade Xbox 270", ["R12"], [], 135, 75, 40);
        Preset(Fh5Official, "R12-Forza Horizon 5-Official2", ["R12", "Forza Horizon 5"], ["32"], 450, 100, 55);
        Preset(Dr2Custom, "R12-DiRT Rally 2.0-Custom", ["R12", "DiRT Rally 2.0"], ["3"], 540, 80, 60);
        Preset("aaaaaaaa-0000-0000-0000-000000000004", "R16-Forza Horizon 5-Official", ["R16", "Forza Horizon 5"], ["32"], 900, 100, 50);
        // A cross-game preset, as found on the real rig: it must not make the FH5 id ambiguous or invent one for FH4.
        Preset("cccccccc-0000-0000-0000-000000000006", "R5 Pro-FH6-Community", ["R5 Pro", "Forza Horizon 4", "Forza Horizon 5"], ["31", "32", "66"], 900, 100, 50);
        Config(Arcade);
    }

    private void Preset(string id, string name, string[] tags, string[] games, int angle, int torque, int ffb)
    {
        using var zip = ZipFile.Open(Path.Combine(root, "Presets", "Motor", id + ".mzpreset"), ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry("preset.json").Open());
        writer.Write(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["name"] = name, ["tags"] = tags, ["games"] = games.ToDictionary(g => g, _ => new { cars = Array.Empty<int>() }),
            ["deviceType"] = "Motor", ["deviceParams"] = new Dictionary<string, object> { ["maximumSteeringAngle"] = angle, ["maximumTorque"] = torque, ["gameForceFeedbackStrength"] = ffb, [""] = 0 },
        }));
    }

    private void Config(string motor, string autoLoad = "true", string? extra = null) =>
        File.WriteAllText(Path.Combine(root, "Presets", "config.ini"),
            $"[Other]\nversion=95\n\n[LastUsedPreset]\n0971b9deb64a3887=cf0766b6-not-a-motor-preset\n805151ab833f1581={motor}\n\n[IsAutoLoadPreset]\nMotor={autoLoad}\n\n[DefaultGamePreset_3]\nR12={Dr2Custom}\n{extra}");

    [Fact]
    public void Fh5IdentityPredictsTheBoundOfficialPresetAndItsSettings()
    {
        var prediction = PitHousePresets.Predict("ForzaHorizon5", root);
        Assert.Equal(PresetOutlook.Switches, prediction.Outlook);
        Assert.Equal("Arcade Xbox 270", prediction.Current!.Name);
        Assert.Equal("R12-Forza Horizon 5-Official2", prediction.Predicted!.Name);
        Assert.Equal((450, 100, 55), (prediction.Predicted.SteeringAngle, prediction.Predicted.MaximumTorque, prediction.Predicted.GameForceFeedback));
        Assert.Contains("450°, 100% torque", prediction.Summary);
    }

    [Fact]
    public void OwnerDefaultForTheGameTakesPrecedence()
    {
        Assert.Equal("R12-DiRT Rally 2.0-Custom", PitHousePresets.Predict("dirtrally2.exe", root).Predicted!.Name);
        Config(Arcade, extra: $"[DefaultGamePreset_32]\nR12={Arcade}\n");
        Assert.Equal(PresetOutlook.Same, PitHousePresets.Predict("ForzaHorizon5.exe", root).Outlook);
    }

    [Fact]
    public void AutoLoadOffIsReportedAsSuch()
    {
        Config(Arcade, autoLoad: "false");
        Assert.Equal(PresetOutlook.AutoLoadOff, PitHousePresets.Predict("ForzaHorizon5.exe", root).Outlook);
    }

    [Fact]
    public void AlreadySelectedPresetIsSame()
    {
        Config(Fh5Official);
        Assert.Equal(PresetOutlook.Same, PitHousePresets.Predict("ForzaHorizon5.exe", root).Outlook);
    }

    [Fact]
    public void MissingOrAmbiguousDataIsUnknownNeverNoChange()
    {
        // FH4 has no preset tagged with its name: its binding id is not recorded.
        Assert.Equal(PresetOutlook.Unknown, PitHousePresets.Predict("ForzaHorizon4.exe", root).Outlook);
        // Not a Pit House game at all.
        Assert.Equal(PresetOutlook.Unknown, PitHousePresets.Predict("MozaTelemetrySmoke.exe", root).Outlook);
        // Two R12 presets bound to FH5.
        Preset("bbbbbbbb-0000-0000-0000-000000000005", "R12-FH5-Community", ["R12", "Forza Horizon 5"], ["32"], 600, 90, 50);
        var ambiguous = PitHousePresets.Predict("ForzaHorizon5.exe", root);
        Assert.Equal(PresetOutlook.Unknown, ambiguous.Outlook);
        Assert.Contains("2 R12 presets", ambiguous.Reason);
    }

    [Fact]
    public void UnreadablePresetInventoryIsUnknown()
    {
        File.WriteAllText(Path.Combine(root, "Presets", "Motor", "broken.mzpreset"), "not a zip");
        var prediction = PitHousePresets.Predict("ForzaHorizon5.exe", root);
        Assert.Equal(PresetOutlook.Unknown, prediction.Outlook);
        Assert.Contains("could not be read", prediction.Reason);
    }

    [Fact]
    public void MissingPitHouseIsUnknown()
    {
        var missing = Path.Combine(root, "absent");
        Assert.False(PitHousePresets.IsPresent(missing));
        Assert.Equal(PresetOutlook.Unknown, PitHousePresets.Predict("ForzaHorizon5.exe", missing).Outlook);
        Assert.Null(PitHousePresets.Snapshot(missing).Motor);
    }

    [Fact]
    public void ChangedSelectionIsReportedAndUnknownIsNeverUnchanged()
    {
        var before = PitHousePresets.Snapshot(root);
        Assert.Equal("Arcade Xbox 270", before.Motor!.Name);
        Assert.Null(PitHousePresets.DescribeChange(before, PitHousePresets.Snapshot(root)));
        Config(Fh5Official);
        var change = PitHousePresets.DescribeChange(before, PitHousePresets.Snapshot(root));
        Assert.Contains("changed from Arcade Xbox 270 (135°, 75% torque) to R12-Forza Horizon 5-Official2 (450°, 100% torque)", change);
        Assert.Contains("could not be compared", PitHousePresets.DescribeChange(before, new PresetSnapshot(null, "gone")));
    }

    [Fact]
    public void ReadingDoesNotChangeAnyPitHouseFile()
    {
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(f => f, f => (File.GetLastWriteTimeUtc(f), new FileInfo(f).Length));
        PitHousePresets.Predict("ForzaHorizon5.exe", root);
        PitHousePresets.Snapshot(root);
        foreach (var (file, state) in files) Assert.Equal(state, (File.GetLastWriteTimeUtc(file), new FileInfo(file).Length));
    }

    public void Dispose() { try { Directory.Delete(root, true); } catch (IOException) { } }
}
