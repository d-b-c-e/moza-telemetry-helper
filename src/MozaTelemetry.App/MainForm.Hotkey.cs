namespace MozaTelemetry.App;

public sealed partial class MainForm
{
    private readonly CheckBox enableHotkey = new() { Text = "Enable global hotkey", AutoSize = true };
    private readonly Button changeHotkey = new() { Text = "Ctrl+Alt+F10…", AutoSize = true };
    private readonly Label hotkeyStatus = new() { AutoSize = true, MaximumSize = new Size(570, 0) };
    private HotkeyOptions hotkeyOptions = new();
    private GlobalHotkeyWindow? hotkeyWindow;
    private SessionToggle? hotkeyToggle;
    private bool updatingHotkeyControls;
    private bool editingHotkey;

    private Control CreateHotkeyControls()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var choices = new FlowLayoutPanel { AutoSize = true };
        choices.Controls.AddRange([enableHotkey, changeHotkey]);
        panel.Controls.AddRange([choices, hotkeyStatus]);
        return panel;
    }

    private void ConfigureHotkey()
    {
        try { hotkeyOptions.Validate(); }
        catch (ArgumentException exception) { hotkeyOptions = new(); Append("Saved hotkey was reset: " + exception.Message); }
        RefreshHotkeyControls();
        hotkeyToggle = new SessionToggle(
            () => changingSession || installingUpdate || exitRequested || closing || editingHotkey,
            () => session != null,
            () => { UpdateAvailability(); return start.Enabled; },
            StartSessionAsync, StopSessionAsync);
        enableHotkey.CheckedChanged += (_, _) =>
        {
            if (!updatingHotkeyControls) ApplyHotkey(hotkeyOptions with { Enabled = enableHotkey.Checked });
        };
        changeHotkey.Click += (_, _) =>
        {
            editingHotkey = true;
            try
            {
                using var dialog = new HotkeyDialog(hotkeyOptions) { Icon = appImage };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ApplyHotkey(dialog.Selection with { Enabled = hotkeyOptions.Enabled });
            }
            finally { editingHotkey = false; }
        };
        Shown += (_, _) =>
        {
            if (hotkeyOptions.Enabled)
            {
                try
                {
                    if (!EnsureHotkeyWindow().Registration.TryApply(hotkeyOptions, out var error))
                    { RefreshHotkeyControls(error); Append("Hotkey inactive: " + error); return; }
                }
                catch (Exception exception) { RefreshHotkeyControls(exception.Message); Append("Hotkey inactive: " + exception.Message); return; }
            }
            RefreshHotkeyControls();
        };
        FormClosed += (_, _) => hotkeyWindow?.Dispose();
    }

    private GlobalHotkeyWindow EnsureHotkeyWindow()
    {
        if (hotkeyWindow != null) return hotkeyWindow;
        hotkeyWindow = new GlobalHotkeyWindow();
        hotkeyWindow.Pressed += async (_, _) =>
        {
            try
            {
                var result = await hotkeyToggle!.ToggleAsync();
                if (result == ToggleResult.Unavailable) Append("Hotkey could not start/stop the helper. " + processAvailability.Text);
            }
            catch (Exception exception) { Append("Hotkey toggle failed: " + exception.Message); }
        };
        return hotkeyWindow;
    }

    private void ApplyHotkey(HotkeyOptions proposed)
    {
        var previousRegistration = hotkeyWindow?.Registration.ActiveOptions;
        var changedRegistration = false;
        try
        {
            proposed.Validate();
            var settings = ReadSettings() with { Hotkey = proposed };
            if (proposed.Enabled || hotkeyWindow != null)
            {
                if (!EnsureHotkeyWindow().Registration.TryApply(proposed, out var error))
                { RefreshHotkeyControls(error); Append("Hotkey unchanged: " + error); return; }
                changedRegistration = true;
            }
            settings.Save();
            hotkeyOptions = proposed;
            RefreshHotkeyControls();
            Append(proposed.Enabled ? $"{proposed.DisplayName} toggles this helper session, including its selected telemetry mode." : "Global hotkey disabled.");
        }
        catch (Exception exception)
        {
            if (changedRegistration)
                hotkeyWindow!.Registration.TryApply(previousRegistration ?? new HotkeyOptions(), out _);
            RefreshHotkeyControls(exception.Message);
            Append("Could not save hotkey: " + exception.Message);
        }
    }

    private void RefreshHotkeyControls(string? error = null)
    {
        updatingHotkeyControls = true;
        enableHotkey.Checked = hotkeyOptions.Enabled;
        changeHotkey.Text = hotkeyOptions.DisplayName + "…";
        updatingHotkeyControls = false;
        var active = hotkeyWindow?.Registration.ActiveOptions;
        hotkeyStatus.ForeColor = error != null ? Color.FromArgb(255, 194, 91) : ForeColor;
        hotkeyStatus.Text = error != null ? (active != null ? active.DisplayName + " remains active. " : "Hotkey inactive. ") + error
            : active != null ? "Active in the tray — toggles Start/Stop, including telemetry."
            : "Optional shortcut for Start/Stop while the app is in the tray.";
        trayStart.ShortcutKeyDisplayString = trayStop.ShortcutKeyDisplayString = active?.DisplayName ?? "";
    }
}
