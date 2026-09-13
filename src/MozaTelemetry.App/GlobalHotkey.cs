using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MozaTelemetry.App;

public interface IHotkeyApi
{
    bool Register(nint window, int id, uint modifiers, uint key, out int error);
    bool Unregister(nint window, int id);
}

// Keeps registration changes testable without reserving keys on the user's desktop.
public sealed class HotkeyRegistration(nint window, IHotkeyApi api) : IDisposable
{
    public const uint NoRepeat = 0x4000;
    private int nextId = 1;
    private bool disposed;
    public int? ActiveId { get; private set; }
    public HotkeyOptions? ActiveOptions { get; private set; }

    public bool TryApply(HotkeyOptions options, out string? error)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        error = null;
        if (!options.Enabled)
        {
            if (ActiveId is int active && !api.Unregister(window, active))
            { error = "Windows could not release the shortcut. Close and reopen the app to retry."; return false; }
            ActiveId = null; ActiveOptions = null;
            return true;
        }
        options.Validate();
        if (options == ActiveOptions) return true;
        var id = nextId++;
        if (nextId > 0xBFFF) nextId = 1;
        if (id == ActiveId) { id = nextId++; if (nextId > 0xBFFF) nextId = 1; }
        if (!api.Register(window, id, options.Modifiers | NoRepeat, options.VirtualKey, out var code))
        {
            error = code == 1409 ? $"{options.DisplayName} is already in use. Choose another shortcut or close the other app instance."
                : $"Windows could not register {options.DisplayName}: {new Win32Exception(code).Message}";
            return false;
        }
        // Reserve the new combination before releasing the old one, so a conflict preserves it.
        if (ActiveId is int previous && !api.Unregister(window, previous))
        {
            api.Unregister(window, id);
            error = "Windows could not replace the previous shortcut. Close and reopen the app to retry.";
            return false;
        }
        ActiveId = id; ActiveOptions = options;
        return true;
    }

    public bool Matches(int id, nint packedKeys)
    {
        var value = (long)packedKeys;
        return ActiveId == id && ActiveOptions is { } options
            && (value & 0x000F) == options.Modifiers && ((value >> 16) & 0xFFFF) == options.VirtualKey;
    }

    public void Dispose()
    {
        if (disposed) return;
        if (ActiveId is int id) api.Unregister(window, id);
        ActiveId = null; ActiveOptions = null; disposed = true;
    }
}

internal sealed class GlobalHotkeyWindow : NativeWindow, IDisposable
{
    public HotkeyRegistration Registration { get; }
    public event EventHandler? Pressed;

    public GlobalHotkeyWindow()
    {
        // A message-only window survives the main form being hidden or its handle recreated.
        CreateHandle(new CreateParams { Caption = "MOZA Telemetry Helper hotkey", Parent = new nint(-3) });
        Registration = new HotkeyRegistration(Handle, new WindowsHotkeyApi());
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && Registration.Matches((int)message.WParam, message.LParam))
        { Pressed?.Invoke(this, EventArgs.Empty); message.Result = 0; return; }
        base.WndProc(ref message);
    }

    public void Dispose() { Registration.Dispose(); DestroyHandle(); }

    private sealed class WindowsHotkeyApi : IHotkeyApi
    {
        public bool Register(nint window, int id, uint modifiers, uint key, out int error)
        {
            var success = RegisterHotKey(window, id, modifiers, key);
            error = success ? 0 : Marshal.GetLastWin32Error();
            return success;
        }
        public bool Unregister(nint window, int id) => UnregisterHotKey(window, id);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(nint hWnd, int id);
    }
}
