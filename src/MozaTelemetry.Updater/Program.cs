using System.Diagnostics;
using Microsoft.Win32;
using MozaTelemetry.App;

namespace MozaTelemetry.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 7) return 2;
        string? restart = null;
        try
        {
            var parentId = int.Parse(args[0]);
            var parentTicks = long.Parse(args[1]);
            var installer = Path.GetFullPath(args[2]);
            var size = long.Parse(args[3]);
            var directory = Path.GetFullPath(args[5]);
            using var key = Registry.CurrentUser.OpenSubKey(UpdatePackage.InstallRegistryKey);
            if (key?.GetValue("Path") is not string installed || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(installed)), directory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The update target is not this user's installed app.");
            if (!installer.StartsWith(UpdatePackage.CacheRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The installer is outside the update cache.");
            // Hold the actual parent process handle to protect against PID reuse. Never terminate it.
            Process? parent = null;
            try { parent = Process.GetProcessById(parentId); }
            catch (ArgumentException) { /* It already exited. */ }
            using (parent)
            {
                if (parent != null && parent.StartTime.ToUniversalTime().Ticks == parentTicks)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    await parent.WaitForExitAsync(timeout.Token);
                }
            }
            restart = Path.Combine(directory, "MozaTelemetryHelper.exe");
            await UpdatePackage.VerifyAsync(installer, size, args[4]);
            // Other UI/headless instances may still be active. Do not let Setup shut them down.
            if (Mutex.TryOpenExisting(UpdatePackage.AppMutex, out var running))
            {
                running.Dispose();
                throw new InvalidOperationException("Another helper instance is still running. Close it before trying the update again.");
            }
            var info = new ProcessStartInfo(installer) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/NOCLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/DIR=" + directory, "/LOG=" + Path.Combine(Path.GetDirectoryName(installer)!, "install.log") }) info.ArgumentList.Add(argument);
            using var setup = Process.Start(info) ?? throw new IOException("Setup could not be started.");
            await setup.WaitForExitAsync();
            if (setup.ExitCode != 0) throw new IOException($"Setup returned exit code {setup.ExitCode}. See the install.log in the update cache.");
            WriteResult("Update installed successfully.");
            return 0;
        }
        catch (Exception exception)
        {
            WriteResult("Update was not completed: " + exception.Message);
            return 1;
        }
        finally
        {
            if (restart != null && File.Exists(restart))
            {
                try
                {
                    var info = new ProcessStartInfo(restart) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(restart)! };
                    if (args[6] == "tray") info.ArgumentList.Add("--tray");
                    using var app = Process.Start(info);
                }
                catch (Exception exception) { WriteResult("Please reopen MOZA Telemetry Helper: " + exception.Message); }
            }
        }
    }

    private static void WriteResult(string message)
    {
        try { Directory.CreateDirectory(UpdatePackage.CacheRoot); File.WriteAllText(UpdatePackage.ResultPath, message); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
