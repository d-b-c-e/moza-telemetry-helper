using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using MozaTelemetry.App;

namespace MozaTelemetry.Tests;

public class ProcessNameSelectorTests
{
    [Theory]
    [InlineData(16)] [InlineData(32)]
    public void EmbeddedIconLoadsAtTrayAndTaskbarSizes(int size)
    {
        using var icon = AppIcon.Load(size);
        Assert.Equal(size, icon.Width);
        Assert.Equal(size, icon.Height);
    }

    // Create only an unshown control: no desktop interaction, forms, tray icons, or saved settings.
    private static void WithSelector(Action<ProcessNameSelector, ComboBox, TextBox> check)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var selector = new ProcessNameSelector();
                check(selector, selector.Controls.OfType<ComboBox>().Single(), selector.Controls.OfType<TextBox>().Single());
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Selector check did not complete.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void DefaultIsFh5AndCustomIsLastWithTextboxHidden() => WithSelector((selector, choices, custom) =>
    {
        Assert.Equal("ForzaHorizon5.exe", selector.ExecutableName);
        Assert.Equal(0, choices.SelectedIndex);
        Assert.Equal(ComboBoxStyle.DropDownList, choices.DropDownStyle);
        Assert.Equal("Custom", choices.Items[^1]);
        Assert.False(custom.Visible);
    });

    [Fact]
    public void SwitchingToCustomRevealsTextboxAndPreservesItAcrossPresetChanges() => WithSelector((selector, choices, custom) =>
    {
        var changes = 0;
        selector.ExecutableNameChanged += (_, _) => changes++;
        choices.SelectedItem = "Custom";
        Assert.True(custom.Visible);
        Assert.Throws<ArgumentException>(() => selector.ExecutableName);
        custom.Text = "MyCustomGame";
        Assert.Equal("MyCustomGame.exe", selector.ExecutableName);
        choices.SelectedIndex = 1;
        Assert.False(custom.Visible);
        Assert.Equal("ForzaHorizon4.exe", selector.ExecutableName);
        choices.SelectedItem = "Custom";
        Assert.Equal("MyCustomGame.exe", selector.ExecutableName);
        Assert.Equal(4, changes);
    });

    [Theory]
    [InlineData("forzahorizon5.EXE", "ForzaHorizon5.exe", false)]
    [InlineData("dirt4", "dirt4.exe", false)]
    [InlineData("MyCustomGame.exe", "MyCustomGame.exe", true)]
    public void ExistingSavedNamesRestoreToPresetOrCustom(string saved, string expected, bool isCustom) => WithSelector((selector, choices, custom) =>
    {
        selector.ExecutableName = saved;
        Assert.Equal(expected, selector.ExecutableName);
        Assert.Equal(isCustom, custom.Visible);
        Assert.Equal(isCustom, choices.SelectedItem as string == "Custom");
    });
}
