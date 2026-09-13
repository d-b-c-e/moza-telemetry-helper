namespace MozaTelemetry.App;

public sealed class HotkeyDialog : Form
{
    private readonly CheckBox control = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox shift = new() { Text = "Shift", AutoSize = true };
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 85 };
    private readonly Label validation = new() { AutoSize = true, MaximumSize = new Size(440, 0) };
    private readonly Button save = new() { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public HotkeyOptions Selection => new() { Control = control.Checked, Alt = alt.Checked, Shift = shift.Checked, Key = key.Text };

    public HotkeyDialog(HotkeyOptions current)
    {
        Text = "Toggle helper shortcut";
        ClientSize = new Size(500, 190);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = "Choose a shortcut to start or stop the helper session.", AutoSize = true });
        var choices = new FlowLayoutPanel { AutoSize = true };
        choices.Controls.AddRange([control, alt, shift, key]);
        key.Items.AddRange(HotkeyOptions.AllowedKeys.Cast<object>().ToArray());
        control.Checked = current.Control; alt.Checked = current.Alt; shift.Checked = current.Shift; key.SelectedItem = current.Key;
        layout.Controls.Add(choices);
        layout.Controls.Add(validation);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([save, cancel]);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = save; CancelButton = cancel;
        control.CheckedChanged += (_, _) => ValidateSelection();
        alt.CheckedChanged += (_, _) => ValidateSelection();
        shift.CheckedChanged += (_, _) => ValidateSelection();
        key.SelectedIndexChanged += (_, _) => ValidateSelection();
        ValidateSelection();
    }

    private void ValidateSelection()
    {
        try { Selection.Validate(); validation.Text = Selection.DisplayName; validation.ForeColor = SystemColors.ControlText; save.Enabled = true; }
        catch (ArgumentException exception) { validation.Text = exception.Message; validation.ForeColor = Color.Firebrick; save.Enabled = false; }
    }
}
