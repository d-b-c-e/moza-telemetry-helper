using MozaTelemetry.Core;

namespace MozaTelemetry.App;

public sealed class MainForm : Form
{
    private readonly TextBox processName = new() { Text = "ForzaHorizon5.exe" };
    private readonly CheckBox runProcess = new() { Text = "Run process helper", Checked = true, AutoSize = true };
    private readonly CheckBox startWithWindows = new() { Text = "Start app with Windows (in tray)", AutoSize = true };
    private readonly CheckBox closeToTray = new() { Text = "Close window to tray (keeps helper running)", AutoSize = true };
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 780 };
    private readonly ComboBox listenAddress = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown listenPort = Port(20777);
    private readonly TextBox outputAddress = new() { Text = "127.0.0.1" };
    private readonly NumericUpDown outputPort = Port(20055);
    private readonly NumericUpDown rpmScale = new() { Minimum = 0.01m, Maximum = 1000, DecimalPlaces = 2, Value = 10 };
    private readonly Button start = new() { Text = "Start helper", AutoSize = true, Padding = new Padding(18, 7, 18, 7) };
    private readonly Button stop = new() { Text = "Stop", AutoSize = true, Enabled = false, Padding = new Padding(18, 7, 18, 7) };
    private readonly Label guidance = new() { AutoSize = true, MaximumSize = new Size(780, 0) };
    private readonly Label processAvailability = new() { AutoSize = true, MaximumSize = new Size(590, 0), Padding = new Padding(0, 2, 0, 4) };
    private readonly Label state = new() { AutoSize = true, Text = "Stopped", Font = new Font("Segoe UI", 16, FontStyle.Bold) };
    private readonly Label stats = new() { AutoSize = true, Text = "Received 0    Sent 0    Ignored / invalid 0" };
    private readonly Label dashboard = new() { AutoSize = true, Text = "— RPM     — km/h     Gear —", Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer availabilityTimer = new() { Interval = 1000 };
    private readonly NotifyIcon trayIcon = new() { Text = "MOZA Telemetry Helper", Icon = SystemIcons.Application };
    private readonly ContextMenuStrip trayMenu = new();
    private readonly ToolStripMenuItem trayStart = new("Start helper");
    private readonly ToolStripMenuItem trayStop = new("Stop helper") { Enabled = false };
    private readonly List<Control> settingsControls = [];
    private HelperSession? session;
    private bool closing;
    private bool changingSession;
    private bool exitRequested;
    private bool loadingPreferences;
    private bool explainedTray;

    public MainForm(bool startInTray = false)
    {
        Text = "MOZA Telemetry Helper";
        ClientSize = new Size(900, 860);
        MinimumSize = new Size(880, 890);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(24, 27, 32);
        ForeColor = Color.FromArgb(235, 238, 243);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 9 };
        for (var i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "MOZA TELEMETRY HELPER", Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true }, 0, 0);
        layout.Controls.Add(new Label { Text = "Make a game visible. Give its telemetry a familiar format.", AutoSize = true, Margin = new Padding(3, 0, 3, 18) }, 0, 1);

        var grid = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mode.Items.AddRange(["Process only — existing FH5 telemetry goes straight to Pit House", "FH5 relay — forward 324-byte packets unchanged", "Codemasters legacy → FH5 (experimental, 264 bytes)", "Project CARS 2 UDP → FH5 (experimental, physics v2)", "Demo → FH5 (generated RPM, speed and gears)"]);
        listenAddress.Items.AddRange(["127.0.0.1", "0.0.0.0"]);
        mode.SelectedIndex = 0; listenAddress.SelectedIndex = 0;
        AddRow(grid, "Executable name", processName);
        AddRow(grid, "", runProcess);
        AddRow(grid, "Process status", processAvailability);
        AddRow(grid, "Telemetry mode", mode);
        var input = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        listenAddress.Width = 160; listenPort.Width = 100;
        input.Controls.AddRange([listenAddress, new Label { Text = "Port", AutoSize = true, Padding = new Padding(8, 5, 0, 0) }, listenPort]);
        AddRow(grid, "Receive UDP", input);
        var output = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        outputAddress.Width = 160; outputPort.Width = 100;
        output.Controls.AddRange([outputAddress, new Label { Text = "Port", AutoSize = true, Padding = new Padding(8, 5, 0, 0) }, outputPort]);
        AddRow(grid, "Send FH5 to", output);
        AddRow(grid, "CM RPM multiplier", rpmScale);
        var behavior = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        behavior.Controls.AddRange([startWithWindows, closeToTray]);
        AddRow(grid, "App behavior", behavior);
        layout.Controls.Add(grid, 0, 2);
        guidance.Margin = new Padding(3, 12, 3, 12);
        layout.Controls.Add(guidance, 0, 3);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        start.BackColor = Color.FromArgb(238, 168, 56); start.ForeColor = Color.Black;
        buttons.Controls.AddRange([start, stop]);
        layout.Controls.Add(buttons, 0, 4);
        state.Margin = new Padding(3, 14, 3, 3);
        layout.Controls.Add(state, 0, 5);
        layout.Controls.Add(dashboard, 0, 6);
        stats.Margin = new Padding(3, 5, 3, 10);
        layout.Controls.Add(stats, 0, 7);
        layout.Controls.Add(log, 0, 8);
        log.BackColor = Color.FromArgb(33, 37, 43); log.ForeColor = ForeColor;
        settingsControls.AddRange([processName, runProcess, mode, listenAddress, listenPort, outputAddress, outputPort, rpmScale]);
        start.Click += async (_, _) => await StartSessionAsync();
        stop.Click += async (_, _) => await StopSessionAsync();
        mode.SelectedIndexChanged += (_, _) =>
        {
            if ((TelemetryMode)mode.SelectedIndex == TelemetryMode.ProjectCars2) { listenPort.Value = 5606; listenAddress.SelectedIndex = 1; }
            else { listenPort.Value = 20777; listenAddress.SelectedIndex = 0; }
            UpdateGuidance();
        };
        runProcess.CheckedChanged += (_, _) => { UpdateGuidance(); UpdateAvailability(); };
        processName.TextChanged += (_, _) => UpdateAvailability();
        availabilityTimer.Tick += (_, _) => UpdateAvailability();
        timer.Tick += async (_, _) => await RefreshStatusAsync();
        trayMenu.Items.Add("Show MOZA Telemetry Helper", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.AddRange([trayStart, trayStop]);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => { exitRequested = true; Close(); });
        trayStart.Click += async (_, _) => await StartSessionAsync();
        trayStop.Click += async (_, _) => await StopSessionAsync();
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.DoubleClick += (_, _) => ShowFromTray();
        trayIcon.Visible = true;
        FormClosing += async (_, eventArgs) =>
        {
            if (closing) return;
            if (!exitRequested && closeToTray.Checked && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                HideToTray();
                return;
            }
            eventArgs.Cancel = true;
            if (changingSession) return; // Let startup/stop finish before closing.
            Enabled = false;
            await StopSessionAsync();
            closing = true;
            Close();
        };
        FormClosed += (_, _) => { timer.Dispose(); availabilityTimer.Dispose(); trayIcon.Visible = false; trayIcon.Dispose(); trayMenu.Dispose(); };
        LoadSettings();
        startWithWindows.CheckedChanged += (_, _) => SavePreferences(updateStartup: true);
        closeToTray.CheckedChanged += (_, _) => SavePreferences(updateStartup: false);
        UpdateGuidance();
        UpdateAvailability();
        availabilityTimer.Start();
        Append("Ready. Process status shows whether the selected name is available. Start runs your own helper session; Stop ends only that session.");
        Shown += (_, _) => { if (startInTray) HideToTray(showNotification: false); };
    }

    private static NumericUpDown Port(int value) => new() { Minimum = 1, Maximum = 65535, Value = value };
    private static void AddRow(TableLayoutPanel grid, string title, Control control)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = title, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 3, 3, 5);
        grid.Controls.Add(control, 1, row);
    }

    private void LoadSettings()
    {
        try
        {
            var saved = AppSettings.Load();
            processName.Text = saved.ProcessName; runProcess.Checked = saved.RunProcess;
            closeToTray.Checked = saved.CloseToTray;
            startWithWindows.Checked = WindowsStartup.IsEnabled();
            mode.SelectedIndex = (int)saved.Bridge.Mode;
            listenAddress.SelectedItem = saved.Bridge.ListenAddress;
            if (listenAddress.SelectedIndex < 0) listenAddress.SelectedIndex = 0;
            listenPort.Value = Math.Clamp(saved.Bridge.ListenPort, 1, 65535);
            outputAddress.Text = saved.Bridge.OutputAddress; outputPort.Value = Math.Clamp(saved.Bridge.OutputPort, 1, 65535);
            rpmScale.Value = Math.Clamp((decimal)saved.Bridge.RpmScale, 0.01m, 1000m);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or OverflowException)
        { Append("Could not load saved settings; using defaults. " + exception.Message); }
    }

    private AppSettings ReadSettings() => new()
    {
        ProcessName = ProcessIdentity.NormalizeName(processName.Text), RunProcess = runProcess.Checked,
        StartWithWindows = startWithWindows.Checked, CloseToTray = closeToTray.Checked,
        Bridge = new BridgeOptions { Mode = (TelemetryMode)mode.SelectedIndex, ListenAddress = listenAddress.Text,
            ListenPort = (int)listenPort.Value, OutputAddress = outputAddress.Text.Trim(), OutputPort = (int)outputPort.Value, RpmScale = (float)rpmScale.Value }
    };

    private void UpdateGuidance()
    {
        var selected = (TelemetryMode)mode.SelectedIndex;
        guidance.Text = selected switch
        {
            TelemetryMode.ProcessOnly => "Use when your game already sends FH5 telemetry directly to Pit House (normally 127.0.0.1:20055). This mode opens no telemetry input port.",
            TelemetryMode.Fh5Relay => "Point the game's FH5 output at the Receive UDP port. This helper forwards it to Pit House. Input and output ports must differ.",
            TelemetryMode.Codemasters => "Requires the 264-byte legacy extradata=3 format. DiRT RPM commonly needs ×10. GRID and DiRT editions need individual validation; modern F1/EA WRC packets differ.",
            TelemetryMode.ProjectCars2 => "In Project CARS 2: select Project CARS 2 UDP protocol and enable UDP frequency. Broadcast reception uses 0.0.0.0:5606. Native Pit House telemetry is also available.",
            _ => "Test signal: cycles RPM, speed and gears without a game. Check Pit House detection and your physical display. Stop ends the signal."
        };
        processName.Enabled = session == null && runProcess.Checked;
        var telemetry = session == null && selected != TelemetryMode.ProcessOnly;
        listenAddress.Enabled = listenPort.Enabled = telemetry && selected != TelemetryMode.Demo;
        outputAddress.Enabled = outputPort.Enabled = telemetry;
        rpmScale.Enabled = session == null && selected == TelemetryMode.Codemasters;
        UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        if (changingSession) { trayStart.Enabled = false; trayStop.Enabled = false; return; }
        if (session != null)
        {
            processAvailability.ForeColor = Color.LightGreen;
            processAvailability.Text = session.Identity.ProcessId is int pid ? $"This session's helper is running (PID {pid})." : "Telemetry only — using your existing game/process.";
            start.Enabled = false;
            UpdateTrayMenu();
            return;
        }
        if (!runProcess.Checked)
        {
            processAvailability.ForeColor = ForeColor;
            processAvailability.Text = "Telemetry only — no additional process will be started.";
            start.Enabled = (TelemetryMode)mode.SelectedIndex != TelemetryMode.ProcessOnly;
            start.Text = start.Enabled ? "Start telemetry" : "Choose telemetry mode";
            UpdateTrayMenu();
            return;
        }
        try
        {
            var name = ProcessIdentity.NormalizeName(processName.Text);
            var existing = ProcessIdentity.FindMatchingProcessIds(name);
            start.Enabled = existing.Length == 0;
            start.Text = existing.Length == 0 ? "Start helper" : "Process already running";
            processAvailability.ForeColor = existing.Length == 0 ? Color.LightGreen : Color.FromArgb(255, 194, 91);
            processAvailability.Text = existing.Length == 0 ? $"{name} is available — ready to start." :
                $"{name} is already running (PID {string.Join(", ", existing)}). Stop that helper/game first, or uncheck Run process helper and choose a telemetry mode.";
        }
        catch (ArgumentException exception)
        {
            processAvailability.ForeColor = Color.FromArgb(255, 194, 91);
            processAvailability.Text = exception.Message;
            start.Text = "Enter a valid name";
            start.Enabled = false;
        }
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu()
    {
        trayStart.Enabled = start.Enabled;
        trayStart.Text = session != null ? "Helper session running" : start.Text;
        trayStop.Enabled = stop.Enabled;
        trayStop.Text = runProcess.Checked ? "Stop helper" : "Stop telemetry";
        trayIcon.Text = session != null ? "MOZA Telemetry Helper — running" : "MOZA Telemetry Helper — stopped";
    }

    private void HideToTray(bool showNotification = true)
    {
        Hide();
        ShowInTaskbar = false;
        if (showNotification && !explainedTray)
        {
            trayIcon.ShowBalloonTip(3000, "MOZA Telemetry Helper is in the tray", "Right-click its tray icon to start or stop the helper, show the window, or exit.", ToolTipIcon.Info);
            explainedTray = true;
        }
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void SavePreferences(bool updateStartup)
    {
        if (loadingPreferences) return;
        try
        {
            var settings = ReadSettings();
            settings.Bridge.Validate();
            if (updateStartup) WindowsStartup.SetEnabled(startWithWindows.Checked);
            settings.Save();
            Append(updateStartup ? (startWithWindows.Checked ? "Windows sign-in will open this app in the tray. Use its menu to start the helper." : "Windows startup disabled.") : (closeToTray.Checked ? "Closing the window will keep the app and any active helper in the tray. Use tray → Exit to stop everything." : "Closing the window will stop the helper and exit."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            loadingPreferences = true;
            try { if (updateStartup) startWithWindows.Checked = WindowsStartup.IsEnabled(); }
            finally { loadingPreferences = false; }
            Append("Could not save preference: " + exception.Message);
            MessageBox.Show(this, exception.Message, "Could not save preference", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartSessionAsync()
    {
        changingSession = true;
        trayStart.Enabled = trayStop.Enabled = false;
        start.Enabled = false;
        foreach (var control in settingsControls) control.Enabled = false;
        try
        {
            var settings = ReadSettings();
            session = new HelperSession();
            state.Text = "Starting…";
            await session.StartAsync(settings);
            try { settings.Save(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Append("Settings could not be saved: " + ex.Message); }
            Append(settings.RunProcess ? $"Started {settings.ProcessName}, PID {session.Identity.ProcessId}." : "Telemetry only; no process helper.");
            if (settings.Bridge.Mode != TelemetryMode.ProcessOnly) Append($"{settings.Bridge.Mode}: {settings.Bridge.ListenAddress}:{settings.Bridge.ListenPort} → {settings.Bridge.OutputAddress}:{settings.Bridge.OutputPort}.");
            stop.Enabled = true; timer.Start();
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            Append("Start not completed: " + exception.Message);
            await StopSessionAsync();
            state.Text = "Could not start — see log";
            if (!Visible) trayIcon.ShowBalloonTip(4000, "Could not start helper", exception.Message, ToolTipIcon.Warning);
        }
        finally { changingSession = false; UpdateAvailability(); if (exitRequested && !closing) Close(); }
    }

    private async Task StopSessionAsync()
    {
        changingSession = true;
        trayStart.Enabled = trayStop.Enabled = false;
        timer.Stop(); stop.Enabled = false;
        var active = session;
        session = null;
        if (active != null)
        {
            var ownedProcess = active.Identity.ProcessId;
            var telemetryWasStarted = active.Bridge != null && (TelemetryMode)mode.SelectedIndex != TelemetryMode.ProcessOnly;
            await active.DisposeAsync();
            if (ownedProcess != null) Append($"Stopped this session's process helper (PID {ownedProcess})" + (telemetryWasStarted ? " and telemetry." : "."));
            else if (telemetryWasStarted) Append("Stopped this session's telemetry bridge.");
        }
        state.Text = "Stopped"; dashboard.Text = "— RPM     — km/h     Gear —";
        start.Enabled = true;
        foreach (var control in settingsControls) control.Enabled = true;
        changingSession = false;
        UpdateGuidance();
    }

    private async Task RefreshStatusAsync()
    {
        if (session == null) return;
        var current = session.Bridge!.Status;
        if (current.Error != null || (runProcess.Checked && !session.Identity.IsRunning))
        { Append("Session ended: " + (current.Error ?? "process helper exited")); await StopSessionAsync(); return; }
        state.Text = current.Stale ? "Telemetry stale — race-off sent" : current.Dashboard != null ? "Sending telemetry — verify wheel display" : "Helper running — waiting for telemetry";
        if ((TelemetryMode)mode.SelectedIndex == TelemetryMode.ProcessOnly) state.Text = "Process helper running — verify Pit House detection";
        stats.Text = $"Received {current.Received:N0}    Sent {current.Sent:N0}    Ignored / invalid {current.Rejected:N0}    Send errors {current.SendErrors:N0}";
        var frame = current.Dashboard;
        dashboard.Text = frame == null ? "— RPM     — km/h     Gear —" : $"{frame.Rpm:N0} RPM     {frame.Speed * 3.6:N0} km/h     Gear {(frame.Gear < 0 ? "R" : frame.Gear == 0 ? "N" : frame.Gear)}";
    }

    private void Append(string message) => log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
}
