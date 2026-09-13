using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MozaTelemetry.App;

namespace MozaTelemetry.Tests;

public sealed class UpdateTests
{
    private static readonly string Digest = "sha256:" + new string('a', 64);
    private static object Release(string tag, bool prerelease = false, bool draft = false, string? name = null, string? digest = null, long size = 1234, long id = 12)
        => new { tag_name = tag, prerelease, draft, assets = new[] { new { name = name ?? $"MozaTelemetryHelper-{tag.TrimStart('v')}-Setup.exe", digest = digest ?? Digest, size, id, state = "uploaded" } } };

    [Fact]
    public void FindsHighestStableInstallableVersionRegardlessOfListOrder()
    {
        var json = JsonSerializer.Serialize(new[] { Release("v0.3.0"), Release("v0.9.0", prerelease: true), Release("v0.8.0", draft: true), Release("v0.4.0"), Release("v0.2.0") });
        var update = GitHubUpdates.SelectRelease(json, new Version(0, 2, 0), useCli: true);
        Assert.Equal(new Version(0, 4, 0), update?.Version);
        Assert.True(update!.UseGitHubCli);
        Assert.Equal(12, update.AssetId);
    }

    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v0.2.0")]
    [InlineData("v0.3.0-beta.1")]
    [InlineData("v0.3.0+build")]
    [InlineData("v0.3")]
    [InlineData("v0.3.0\n")]
    [InlineData("../v0.3.0")]
    [InlineData("v999999999999999999999.0.0")]
    public void NeverDowngradesOrAcceptsNonStableTags(string tag)
        => Assert.Null(GitHubUpdates.SelectRelease(JsonSerializer.Serialize(new[] { Release(tag) }), new Version(0, 2, 0)));

    [Theory]
    [InlineData("unrelated-Setup.exe", "sha256:abc", 1234)]
    [InlineData("MozaTelemetryHelper-0.3.0-Setup.exe", "", 1234)]
    [InlineData("MozaTelemetryHelper-0.3.0-Setup.exe", "sha256:bad", 1234)]
    [InlineData("MozaTelemetryHelper-0.3.0-Setup.exe", null, 0)]
    [InlineData("MozaTelemetryHelper-0.3.0-Setup.exe", null, 536870913)]
    [InlineData("../../MozaTelemetryHelper-0.3.0-Setup.exe", null, 1234)]
    public void IgnoresIncompleteOrUnexpectedInstallerAssets(string name, string? digest, long size)
        => Assert.Null(GitHubUpdates.SelectRelease(JsonSerializer.Serialize(new[] { Release("v0.3.0", name: name, digest: digest, size: size) }), new Version(0, 2, 0)));

    [Fact]
    public void NewerReleaseWithoutInstallerDoesNotHideValidUpdate()
    {
        var json = JsonSerializer.Serialize(new[] { Release("v0.4.0", name: "source.zip"), Release("v0.3.0") });
        Assert.Equal(new Version(0, 3, 0), GitHubUpdates.SelectRelease(json, new Version(0, 2, 0))?.Version);
    }

    [Fact]
    public void EmptyReleaseFeedHasNoUpdate()
        => Assert.Null(GitHubUpdates.SelectRelease("[]", new Version(0, 2, 0)));

    [Fact]
    public async Task VerifiesContentAndRejectsCorruptionOrTruncation()
    {
        var path = Path.GetTempFileName();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("test installer content");
            await File.WriteAllBytesAsync(path, bytes);
            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            await UpdatePackage.VerifyAsync(path, bytes.Length, digest);
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdatePackage.VerifyAsync(path, bytes.Length + 1, digest));
            bytes[0] ^= 1;
            await File.WriteAllBytesAsync(path, bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdatePackage.VerifyAsync(path, bytes.Length, digest));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task BoundsDownloadEvenWhenServerSendsTooMuch()
    {
        using var input = new MemoryStream(new byte[100]);
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => GitHubUpdates.CopyDownloadAsync(input, output, 50, null, CancellationToken.None));
        Assert.Empty(output.ToArray());
    }

    [Fact]
    public async Task DownloadCanBeCancelled()
    {
        using var input = new MemoryStream(new byte[100]);
        using var output = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubUpdates.CopyDownloadAsync(input, output, 100, null, cancellation.Token));
    }

    [Fact]
    public void ExistingSettingsEnableChecksWithoutChangingStartupOrTray()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"CloseToTray\":true,\"StartWithWindows\":false}");
        Assert.True(settings!.CheckForUpdates);
        Assert.True(settings.CloseToTray);
        Assert.False(settings.StartWithWindows);
        Assert.False(JsonSerializer.Deserialize<AppSettings>("{\"CheckForUpdates\":false}")!.CheckForUpdates);
    }
}
