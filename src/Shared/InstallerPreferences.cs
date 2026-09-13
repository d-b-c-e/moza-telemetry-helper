using System.Text.Json;
using System.Text.Json.Nodes;

namespace MozaTelemetry.App;

public static class InstallerPreferences
{
    private static JsonObject ReadSettings(string path)
    {
        if (!File.Exists(path)) return new JsonObject();
        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException("Settings must be a JSON object.");
    }

    public static bool ReadUpdateChecks(string path)
        => ReadSettings(path)["CheckForUpdates"]?.GetValue<bool>() ?? true;

    public static void WriteUpdateChecks(string path, bool enabled)
    {
        var settings = ReadSettings(path);
        settings["CheckForUpdates"] = enabled;
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
