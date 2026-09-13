using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace MozaTelemetry.App;

public static class UpdatePackage
{
    public const string Repository = "d-b-c-e/moza-telemetry-helper";
    public const string AppMutex = "MozaTelemetryHelper.Running";
    public const string InstallRegistryKey = @"Software\MozaTelemetryHelper\Installation";
    public const long MaximumSize = 512L * 1024 * 1024;
    public static string CacheRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MozaTelemetryHelper", "updates");
    public static string ResultPath => Path.Combine(CacheRoot, "last-result.txt");

    // Only stable, canonical major.minor.patch tags enter the automatic update channel.
    public static bool TryVersion(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        return Regex.IsMatch(tag, @"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z")
            && Version.TryParse(tag.TrimStart('v'), out version!);
    }

    public static string InstallerName(Version version) => $"MozaTelemetryHelper-{version.ToString(3)}-Setup.exe";

    public static async Task VerifyAsync(string path, long size, string sha256, CancellationToken cancellationToken = default)
    {
        if (size is <= 0 or > MaximumSize || !Regex.IsMatch(sha256, @"\A[a-fA-F0-9]{64}\z"))
            throw new InvalidDataException("The release is missing a valid size or SHA-256 digest.");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length != size) throw new InvalidDataException("The installer download is incomplete or has an unexpected size.");
        var actual = await SHA256.HashDataAsync(file, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(sha256)))
            throw new InvalidDataException("The installer SHA-256 does not match GitHub. It will not be run.");
    }
}
