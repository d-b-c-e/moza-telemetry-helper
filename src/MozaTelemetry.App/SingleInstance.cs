using System.Runtime.InteropServices;

namespace MozaTelemetry.App;

// One window/tray instance per Windows session. A later launch asks the first to show its window and exits,
// so a Start menu click never adds a second tray icon beside the copy started at sign-in.
public sealed class SingleInstance : IDisposable
{
    public const string WindowName = "MozaTelemetryHelper.Window";
    private readonly EventWaitHandle showRequest;
    private RegisteredWaitHandle? listener;
    private bool disposed;

    private SingleInstance(EventWaitHandle showRequest) => this.showRequest = showRequest;

    // Returns null when another instance already owns the name; it is asked to show its window unless quiet.
    public static SingleInstance? TryAcquire(string name, bool showExisting)
    {
        var handle = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + name, out var created);
        if (created) return new SingleInstance(handle);
        if (showExisting)
        {
            // The launching process owns the foreground; let the existing instance take it.
            AllowSetForegroundWindow(AnyProcess);
            handle.Set();
        }
        handle.Dispose();
        return null;
    }

    public void OnShowRequested(Action callback)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        listener ??= ThreadPool.RegisterWaitForSingleObject(showRequest, (_, _) => callback(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        listener?.Unregister(null);
        showRequest.Dispose();
    }

    private const int AnyProcess = -1;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
