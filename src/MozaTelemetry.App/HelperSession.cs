using MozaTelemetry.Core;

namespace MozaTelemetry.App;

public sealed class HelperSession : IAsyncDisposable
{
    public ProcessIdentity Identity { get; } = new();
    public TelemetryBridge? Bridge { get; private set; }
    public async Task StartAsync(AppSettings settings)
    {
        settings.Bridge.Validate();
        if (!settings.RunProcess && settings.Bridge.Mode == TelemetryMode.ProcessOnly)
            throw new ArgumentException("Enable the process helper or choose a telemetry mode.");
        try
        {
            // Acquire the UDP socket first so a bind failure does not leave a false game identity running.
            Bridge = new TelemetryBridge(settings.Bridge);
            Bridge.Start();
            if (settings.RunProcess)
                await Task.Run(() => Identity.Start(settings.ProcessName, Path.Combine(AppContext.BaseDirectory, "sentinel")));
        }
        catch { await DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (Bridge != null) { await Bridge.DisposeAsync(); Bridge = null; }
        Identity.Dispose();
    }
}
