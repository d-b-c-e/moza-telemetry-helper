using Microsoft.Win32;

namespace MozaTelemetry.App;

public static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MozaTelemetryHelper";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string command && command.Length > 0;
    }

    public static string StartupCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"'))
            throw new ArgumentException("Windows startup requires the full path to the published executable.");
        return $"\"{executable}\" --tray";
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, StartupCommand(Environment.ProcessPath ?? throw new InvalidOperationException("Application path is unavailable.")), RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
