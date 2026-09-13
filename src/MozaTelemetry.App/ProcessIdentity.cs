using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MozaTelemetry.App;

public sealed class ProcessIdentity : IDisposable
{
    private Process? child;
    private EventWaitHandle? stop;
    private string? directory;
    public int? ProcessId => child?.Id;
    public bool IsRunning => child is { HasExited: false };

    public static int[] FindMatchingProcessIds(string name)
    {
        var matches = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(NormalizeName(name)));
        try { return matches.Select(process => process.Id).Order().ToArray(); }
        finally { foreach (var process in matches) process.Dispose(); }
    }

    public static string NormalizeName(string value)
    {
        var name = value.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length is < 1 or > 100 || !Regex.IsMatch(name, @"^[A-Za-z0-9_-][A-Za-z0-9_. -]*$") || name.EndsWith('.') || name.EndsWith(' '))
            throw new ArgumentException("Enter an executable name, such as ForzaHorizon5.exe, without a folder path.");
        if (Regex.IsMatch(name.Split('.')[0], @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase))
            throw new ArgumentException("That name is reserved by Windows.");
        return name + ".exe";
    }

    public void Start(string name, string sentinelSource)
    {
        if (child != null) throw new InvalidOperationException("Process helper already started.");
        name = NormalizeName(name);
        var matches = FindMatchingProcessIds(name);
        if (matches.Length > 0) throw new InvalidOperationException($"{name} is already running (PID {string.Join(", ", matches)}). Stop that session or uncheck 'Run process helper' and choose a telemetry mode.");
        if (!File.Exists(Path.Combine(sentinelSource, "MozaTelemetry.Sentinel.exe")))
            throw new FileNotFoundException("Sentinel files are missing. Run scripts/publish.ps1 and start the app from artifacts/win-x64.");
        var token = Guid.NewGuid().ToString("N");
        directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MozaTelemetryHelper", "sessions", token);
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var file in Directory.GetFiles(sentinelSource)) File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            var executable = Path.Combine(directory, name);
            var original = Path.Combine(directory, "MozaTelemetry.Sentinel.exe");
            if (!executable.Equals(original, StringComparison.OrdinalIgnoreCase)) File.Move(original, executable);
            var stopName = @"Local\MozaTelemetryStop-" + token;
            var readyName = @"Local\MozaTelemetryReady-" + token;
            stop = new EventWaitHandle(false, EventResetMode.ManualReset, stopName);
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, readyName);
            using var parent = Process.GetCurrentProcess();
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
            foreach (var argument in new[] { Environment.ProcessId.ToString(), parent.StartTime.ToUniversalTime().Ticks.ToString(), stopName, readyName }) start.ArgumentList.Add(argument);
            child = Process.Start(start) ?? throw new InvalidOperationException("Failed to start the process helper.");
            if (!ready.WaitOne(TimeSpan.FromSeconds(10)) || child.HasExited)
                throw new InvalidOperationException("Process helper failed to become ready. Check that .NET 10 Desktop Runtime is installed, or use the self-contained build.");
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        stop?.Set();
        if (child != null)
        {
            if (!child.HasExited && !child.WaitForExit(2000)) { child.Kill(); child.WaitForExit(2000); }
            child.Dispose(); child = null;
        }
        stop?.Dispose(); stop = null;
        if (directory != null)
        {
            // Only this instance's GUID directory, constructed above; never a user-supplied directory.
            try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            directory = null;
        }
    }
}
