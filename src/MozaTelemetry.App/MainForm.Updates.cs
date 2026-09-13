using System.Diagnostics;

namespace MozaTelemetry.App;

public sealed partial class MainForm
{
    private readonly CheckBox checkForUpdates = new() { Text = "Automatically check GitHub for updates", Checked = true, AutoSize = true };
    private readonly Button checkUpdate = new() { Text = "Check now", AutoSize = true };
    private readonly Button installUpdate = new() { Text = "Install update", AutoSize = true, Visible = false };
    private readonly Label updateStatus = new() { AutoSize = true, MaximumSize = new Size(530, 0), Text = "Version " + GitHubUpdates.CurrentVersion };
    private readonly ToolStripMenuItem trayCheckUpdate = new("Check for updates");
    private readonly ToolStripMenuItem trayInstallUpdate = new("Install update") { Visible = false };
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private readonly CancellationTokenSource updateCancellation = new();
    private readonly GitHubUpdates updates = new();
    private UpdateRelease? availableUpdate;
    private bool checkingUpdate;
    private bool installingUpdate;

    private Control CreateUpdateControls()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        buttons.Controls.AddRange([checkUpdate, installUpdate]);
        panel.Controls.AddRange([buttons, updateStatus]);
        return panel;
    }

    private void ConfigureUpdates()
    {
        trayMenu.Items.Insert(trayMenu.Items.Count - 1, trayCheckUpdate);
        trayMenu.Items.Insert(trayMenu.Items.Count - 1, trayInstallUpdate);
        checkUpdate.Click += async (_, _) => await CheckUpdatesAsync();
        trayCheckUpdate.Click += async (_, _) => await CheckUpdatesAsync();
        installUpdate.Click += async (_, _) => await InstallUpdateAsync();
        trayInstallUpdate.Click += async (_, _) => await InstallUpdateAsync();
        updateTimer.Tick += async (_, _) => { if (checkForUpdates.Checked) await CheckUpdatesAsync(); };
        Shown += async (_, _) =>
        {
            // Preferences are loaded before registering their change handler.
            checkForUpdates.CheckedChanged += (_, _) =>
            {
                try { ReadSettings().Save(); Append(checkForUpdates.Checked ? "Automatic update checks enabled." : "Automatic update checks disabled."); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
                { Append("Could not save update preference: " + exception.Message); }
            };
            try
            {
                if (File.Exists(UpdatePackage.ResultPath))
                {
                    Append(File.ReadAllText(UpdatePackage.ResultPath));
                    File.Delete(UpdatePackage.ResultPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { Append("Could not read the previous update result: " + exception.Message); }
            updateTimer.Start();
            if (checkForUpdates.Checked) await CheckUpdatesAsync();
        };
        FormClosed += (_, _) => { updateTimer.Dispose(); updateCancellation.Cancel(); updateCancellation.Dispose(); };
    }

    private void RefreshUpdateControls()
    {
        checkUpdate.Enabled = trayCheckUpdate.Enabled = !checkingUpdate && !installingUpdate;
        installUpdate.Visible = trayInstallUpdate.Visible = availableUpdate != null;
        installUpdate.Enabled = trayInstallUpdate.Enabled = availableUpdate != null && session == null && !changingSession && !installingUpdate && !checkingUpdate;
    }

    private async Task CheckUpdatesAsync()
    {
        if (checkingUpdate || installingUpdate || exitRequested) return;
        checkingUpdate = true;
        updateStatus.Text = "Checking GitHub…";
        RefreshUpdateControls();
        try
        {
            availableUpdate = await updates.CheckAsync(updateCancellation.Token);
            if (IsDisposed || Disposing) return;
            if (availableUpdate == null) updateStatus.Text = $"Version {GitHubUpdates.CurrentVersion} — no newer installable release.";
            else
            {
                installUpdate.Text = GitHubUpdates.IsInstalled() ? "Install " + availableUpdate.Version : "Download Setup";
                trayInstallUpdate.Text = installUpdate.Text;
                updateStatus.Text = $"Version {availableUpdate.Version} available. Stop the helper to install.";
                Append(updateStatus.Text);
            }
        }
        catch (OperationCanceledException) { if (!IsDisposed) updateStatus.Text = "Update check cancelled or timed out."; }
        catch (Exception exception)
        {
            if (IsDisposed || Disposing) return;
            updateStatus.Text = "Update check unavailable — see log.";
            Append("Update check: " + exception.Message);
        }
        finally { checkingUpdate = false; if (!IsDisposed) RefreshUpdateControls(); }
    }

    private async Task InstallUpdateAsync()
    {
        if (availableUpdate == null || installingUpdate || session != null || changingSession || exitRequested) return;
        try
        {
            if (!GitHubUpdates.IsInstalled())
            {
                // Archive/development builds remain in place. Let the user choose to install Setup.
                Process.Start(new ProcessStartInfo($"https://github.com/{UpdatePackage.Repository}/releases/tag/{availableUpdate.Tag}") { UseShellExecute = true });
                return;
            }
            installingUpdate = true;
            UpdateAvailability();
            updateStatus.Text = "Downloading and verifying installer…";
            var progress = new Progress<int>(percent => { if (!IsDisposed) updateStatus.Text = $"Downloading update… {percent}%"; });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(updateCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var installer = await updates.DownloadAsync(availableUpdate, progress, timeout.Token);
            if (IsDisposed || Disposing || updateCancellation.IsCancellationRequested) return;
            ReadSettings().Save();
            GitHubUpdates.LaunchUpdate(installer, availableUpdate, restartInTray: !Visible);
            // The separate updater waits on this process handle before changing installed files.
            exitRequested = true;
            Close();
        }
        catch (OperationCanceledException) { if (!IsDisposed) updateStatus.Text = "Update download cancelled or timed out."; }
        catch (Exception exception)
        {
            if (IsDisposed || Disposing) return;
            updateStatus.Text = "Update could not be installed — see log.";
            Append("Update: " + exception.Message);
        }
        finally { installingUpdate = false; if (!IsDisposed) UpdateAvailability(); }
    }
}
