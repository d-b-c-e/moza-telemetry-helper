using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MozaTelemetry.App;

public sealed record UpdateRelease(Version Version, string Tag, string AssetName, long AssetId, long Size, string Sha256, bool UseGitHubCli);

public sealed class GitHubUpdates
{
    public static Version CurrentVersion { get; } = new(typeof(GitHubUpdates).Assembly.GetName().Version!.ToString(3));
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MozaTelemetryHelper/" + CurrentVersion);
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        const string endpoint = "repos/" + UpdatePackage.Repository + "/releases?per_page=100";
        using var response = await Client.GetAsync("https://api.github.com/" + endpoint, timeout.Token);
        var useCli = response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;
        string json;
        if (useCli)
        {
            // gh owns its credentials; never retrieve, persist or bundle a user's token.
            json = await RunGhAsync(["api", "--hostname", "github.com", endpoint], null, timeout.Token);
        }
        else
        {
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(timeout.Token);
        }
        return SelectRelease(json, CurrentVersion, useCli);
    }

    public static UpdateRelease? SelectRelease(string json, Version current, bool useCli = false)
    {
        using var document = JsonDocument.Parse(json);
        UpdateRelease? selected = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!UpdatePackage.TryVersion(tag, out var version) || version <= current || (selected != null && version <= selected.Version)) continue;
            var name = UpdatePackage.InstallerName(version);
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != name || asset.GetProperty("state").GetString() != "uploaded") continue;
                var size = asset.GetProperty("size").GetInt64();
                var id = asset.GetProperty("id").GetInt64();
                var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() ?? "" : "";
                if (size is <= 0 or > UpdatePackage.MaximumSize || id <= 0 || !Regex.IsMatch(digest, @"\Asha256:[a-fA-F0-9]{64}\z")) continue;
                selected = new(version, tag, name, id, size, digest[7..], useCli);
                break;
            }
        }
        return selected;
    }

    public async Task<string> DownloadAsync(UpdateRelease release, IProgress<int> progress, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(UpdatePackage.CacheRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, UpdatePackage.InstallerName(release.Version));
        try
        {
            if (release.UseGitHubCli)
            {
                await RunGhAsync(["api", "--hostname", "github.com", $"repos/{UpdatePackage.Repository}/releases/assets/{release.AssetId}", "-H", "Accept: application/octet-stream"], path, cancellationToken);
            }
            else
            {
                // Construct the URL from validated tags and names, never execute an arbitrary asset URL.
                var url = $"https://github.com/{UpdatePackage.Repository}/releases/download/{release.Tag}/{release.AssetName}";
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await CopyDownloadAsync(input, output, release.Size, progress, cancellationToken);
            }
            await UpdatePackage.VerifyAsync(path, release.Size, release.Sha256, cancellationToken);
            progress.Report(100);
            return path;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            throw;
        }
    }

    public static async Task CopyDownloadAsync(Stream input, Stream output, long expectedSize, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            total += count;
            if (total > expectedSize || total > UpdatePackage.MaximumSize) throw new InvalidDataException("The download exceeded its advertised size.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            progress?.Report((int)(total * 100 / expectedSize));
        }
    }

    private static async Task<string> RunGhAsync(string[] arguments, string? destination, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("gh.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
        info.Environment["GH_PROMPT_DISABLED"] = "1";
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception)
        { throw new InvalidOperationException("GitHub access requires authentication while this repository is private. Install GitHub CLI and run 'gh auth login' with an account that has repository access, or download from the release page. Public releases need no login."); }
        try
        {
            // Drain stderr without surfacing potentially sensitive CLI diagnostics in the app log.
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            string json = "";
            if (destination == null) json = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            else
            {
                await using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await CopyDownloadAsync(process.StandardOutput.BaseStream, file, UpdatePackage.MaximumSize, null, cancellationToken);
            }
            await process.WaitForExitAsync(cancellationToken);
            await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException("GitHub could not be accessed. Check your GitHub CLI login ('gh auth login'), repository access, and network connection. Public releases need no login.");
            return json;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
        }
    }

    public static bool IsInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UpdatePackage.InstallRegistryKey);
        return key?.GetValue("Path") is string installed && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(installed)),
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase);
    }

    public static void LaunchUpdate(string installer, UpdateRelease release, bool restartInTray)
    {
        if (!IsInstalled()) throw new InvalidOperationException("Install the app with Setup first to enable in-app installation of updates.");
        var directory = Path.GetDirectoryName(installer)!;
        // Run a copy outside the installation so its own executable can be replaced by Setup.
        var runner = Path.Combine(directory, "MozaTelemetryUpdater.exe");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "updater", "MozaTelemetryUpdater.exe"), runner, true);
        using var parent = Process.GetCurrentProcess();
        var info = new ProcessStartInfo(runner) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
        foreach (var argument in new[] { parent.Id.ToString(), parent.StartTime.ToUniversalTime().Ticks.ToString(), installer, release.Size.ToString(), release.Sha256,
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), restartInTray ? "tray" : "window" }) info.ArgumentList.Add(argument);
        using var started = Process.Start(info) ?? throw new IOException("Could not start the update installer helper.");
    }
}
