using System.ComponentModel;

namespace MozaTelemetry.App;

public sealed class ProcessNameSelector : TableLayoutPanel
{
    public const string DefaultExecutable = "ForzaHorizon5.exe";
    public const string CustomChoice = "Custom";

    // Executable identities from Pit House's game configuration; these do not change the wire protocol.
    private static readonly string[] presets =
    [
        DefaultExecutable, "ForzaHorizon4.exe", "forza_steamworks_release_final.exe",
        "pCARS2AVX.exe", "pCARS64.exe", "dirt4.exe", "dirtrally2.exe",
        "AssettoCorsa.exe", "acc.exe", "AMS2AVX.exe", "iRacingSim64DX11.exe"
    ];

    private readonly ComboBox choices = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill,
        AccessibleName = "Executable name", Margin = Padding.Empty
    };
    private readonly TextBox customName = new()
    {
        Dock = DockStyle.Fill, Visible = false, AccessibleName = "Custom executable name",
        PlaceholderText = "Enter an executable name, with or without .exe", Margin = new Padding(0, 6, 0, 0)
    };

    public event EventHandler? ExecutableNameChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ExecutableName
    {
        get => ProcessIdentity.NormalizeName(choices.SelectedItem as string == CustomChoice
            ? customName.Text : choices.SelectedItem as string ?? DefaultExecutable);
        set
        {
            var normalized = ProcessIdentity.NormalizeName(value);
            var preset = Array.FindIndex(presets, name => name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (preset >= 0) choices.SelectedIndex = preset;
            else
            {
                customName.Text = normalized;
                choices.SelectedItem = CustomChoice;
            }
        }
    }

    public ProcessNameSelector()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 1;
        RowCount = 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        choices.Items.AddRange(presets);
        choices.Items.Add(CustomChoice);
        Controls.Add(choices, 0, 0);
        Controls.Add(customName, 0, 1);
        choices.SelectedIndex = 0;
        choices.SelectedIndexChanged += (_, _) =>
        {
            customName.Visible = choices.SelectedItem as string == CustomChoice;
            ExecutableNameChanged?.Invoke(this, EventArgs.Empty);
        };
        customName.TextChanged += (_, _) =>
        {
            if (choices.SelectedItem as string == CustomChoice) ExecutableNameChanged?.Invoke(this, EventArgs.Empty);
        };
    }
}
