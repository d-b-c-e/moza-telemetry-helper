using System.Text.Json;
using MozaTelemetry.App;

namespace MozaTelemetry.Tests;

public sealed class TrayTests
{
    private static string UniqueName() => "MozaTelemetryTest." + Guid.NewGuid().ToString("N");

    [Fact]
    public void NewUsersCloseToTrayButSavedChoicesAreKept()
    {
        Assert.True(new AppSettings().CloseToTray);
        Assert.True(JsonSerializer.Deserialize<AppSettings>("{\"CheckForUpdates\":true}")!.CloseToTray);
        Assert.False(JsonSerializer.Deserialize<AppSettings>("{\"CloseToTray\":false}")!.CloseToTray);
    }

    [Fact]
    public void SecondInstanceIsRefusedAndAsksTheFirstToShow()
    {
        var name = UniqueName();
        using var first = SingleInstance.TryAcquire(name, showExisting: false);
        Assert.NotNull(first);
        using var shown = new ManualResetEventSlim();
        first.OnShowRequested(shown.Set);

        Assert.Null(SingleInstance.TryAcquire(name, showExisting: true));
        Assert.True(shown.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void QuietSecondLaunchDoesNotShowTheWindow()
    {
        var name = UniqueName();
        using var first = SingleInstance.TryAcquire(name, showExisting: false);
        using var shown = new ManualResetEventSlim();
        first!.OnShowRequested(shown.Set);

        Assert.Null(SingleInstance.TryAcquire(name, showExisting: false));
        Assert.False(shown.Wait(TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void NameIsAvailableAgainAfterTheFirstInstanceExits()
    {
        var name = UniqueName();
        SingleInstance.TryAcquire(name, showExisting: false)!.Dispose();
        using var next = SingleInstance.TryAcquire(name, showExisting: true);
        Assert.NotNull(next);
    }
}
