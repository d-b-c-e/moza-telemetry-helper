using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MozaTelemetry.Core;

/// <summary>A process that already uses an identity name. It may be a real game, another helper or an orphan;
/// the owner is never assumed.</summary>
public sealed record ExistingProcess(int ProcessId, string? Path, DateTime? StartTime)
{
    public override string ToString() => $"PID {ProcessId}" + (Path is null ? "" : $", {Path}") + (StartTime is { } started ? $", started {started:yyyy-MM-dd HH:mm:ss}" : "");
}

/// <summary>An owned, idle child process running under a chosen executable name. Only the exact child this
/// instance started is ever stopped; it also exits by itself when its parent dies.</summary>
public sealed class ProcessIdentity : IDisposable
{
    public const string SentinelFileName = "MozaTelemetry.Sentinel.exe";
    private readonly string sessionsRoot;
    private Process? child;
    private EventWaitHandle? stop;
    private string? directory;
    public int? ProcessId => child?.Id;
    public DateTime? StartTime { get; private set; }
    public bool IsRunning => child is { HasExited: false };

    /// <param name="sessionsRoot">Folder owned by the caller where per-session copies of the sentinel are made.</param>
    public ProcessIdentity(string sessionsRoot)
    {
        if (!Path.IsPathFullyQualified(sessionsRoot)) throw new ArgumentException("The sessions folder must be a full path.", nameof(sessionsRoot));
        this.sessionsRoot = sessionsRoot;
    }

    public static int[] FindMatchingProcessIds(string name) => FindExisting(name).Select(process => process.ProcessId).ToArray();

    public static ExistingProcess[] FindExisting(string name)
    {
        var matches = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(NormalizeName(name)));
        try { return matches.Select(Describe).OrderBy(process => process.ProcessId).ToArray(); }
        finally { foreach (var process in matches) process.Dispose(); }
    }

    private static ExistingProcess Describe(Process process)
    {
        string? path = null; DateTime? started = null;
        // Another user's or an elevated process may refuse these; the PID alone still identifies it.
        try { path = process.MainModule?.FileName; } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
        try { started = process.StartTime; } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
        return new ExistingProcess(process.Id, path, started);
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
        var existing = FindExisting(name);
        if (existing.Length > 0) throw new InvalidOperationException($"{name} is already running ({string.Join("; ", existing)}). Stop that process or session first.");
        if (!File.Exists(Path.Combine(sentinelSource, SentinelFileName)))
            throw new FileNotFoundException($"{SentinelFileName} is missing from {sentinelSource}.");
        var token = Guid.NewGuid().ToString("N");
        directory = Path.Combine(sessionsRoot, token);
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var file in Directory.GetFiles(sentinelSource)) File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            var executable = Path.Combine(directory, name);
            var original = Path.Combine(directory, SentinelFileName);
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
                throw new InvalidOperationException("Process helper failed to become ready.");
            StartTime = child.StartTime;
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
        StartTime = null;
        if (directory != null)
        {
            // Only this instance's GUID directory, constructed above; never a user-supplied directory.
            try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            directory = null;
        }
    }
}
