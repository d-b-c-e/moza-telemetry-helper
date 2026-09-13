using System.Text.Json.Nodes;
using MozaTelemetry.App;

namespace MozaTelemetry.Tests;

public sealed class InstallerPreferencesTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "MozaPreferences-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void MissingSettingsUseAppDefaultAndCanBeCreated()
    {
        Assert.True(InstallerPreferences.ReadUpdateChecks(path));
        InstallerPreferences.WriteUpdateChecks(path, false);
        Assert.False(InstallerPreferences.ReadUpdateChecks(path));
    }

    [Fact]
    public void UsesRealJsonBooleanAndPreservesOtherSettings()
    {
        File.WriteAllText(path, "{\"ProcessName\":\"Custom.exe\",\"CheckForUpdates\" : false,\"Bridge\":{\"Mode\":2},\"FutureSetting\":[1,2]}");
        Assert.False(InstallerPreferences.ReadUpdateChecks(path));
        InstallerPreferences.WriteUpdateChecks(path, true);
        Assert.True(InstallerPreferences.ReadUpdateChecks(path));
        var settings = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal("Custom.exe", settings["ProcessName"]!.GetValue<string>());
        Assert.Equal(2, settings["Bridge"]!["Mode"]!.GetValue<int>());
        Assert.Equal(2, settings["FutureSetting"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("invalid json")]
    [InlineData("[]")]
    [InlineData("null")]
    public void MalformedExistingSettingsAreNeverOverwritten(string original)
    {
        File.WriteAllText(path, original);
        Assert.ThrowsAny<Exception>(() => InstallerPreferences.ReadUpdateChecks(path));
        Assert.ThrowsAny<Exception>(() => InstallerPreferences.WriteUpdateChecks(path, false));
        Assert.Equal(original, File.ReadAllText(path));
    }

    public void Dispose() { if (File.Exists(path)) File.Delete(path); }
}
