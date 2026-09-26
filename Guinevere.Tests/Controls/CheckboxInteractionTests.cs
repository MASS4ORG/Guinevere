using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

public class CheckboxInteractionTests
{
    bool _checked;

    [Fact]
    public void ClickTogglesAnEnabledCheckboxTwice()
    {
        using var harness = new FrameHarness();
        void Draw(Gui gui) => gui.Checkbox(ref _checked, "Enable notifications");

        harness.Frame(Draw);
        var center = FrameHarness.Center(Assert.Single(harness.Gui.RootNode!.Children));
        harness.Click(Draw, center);
        Assert.True(_checked);

        harness.Click(Draw, center);
        Assert.False(_checked);
    }

    [Fact]
    public void DisabledCheckboxIgnoresClickAndKeepsItsValue()
    {
        using var harness = new FrameHarness();
        _checked = true;
        void Draw(Gui gui) => gui.Checkbox(ref _checked, "Locked", enabled: false);

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(Assert.Single(harness.Gui.RootNode!.Children)));

        Assert.True(_checked);
    }
}
