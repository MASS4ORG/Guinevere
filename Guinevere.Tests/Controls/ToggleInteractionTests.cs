using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

public class ToggleInteractionTests
{
    bool _on;

    [Fact]
    public void ClickingEnabledToggleChangesItsValue()
    {
        using var harness = new FrameHarness();
        void Draw(Gui gui) => gui.Toggle(ref _on, "Notifications");

        harness.Frame(Draw);
        var center = FrameHarness.Center(Assert.Single(harness.Gui.RootNode!.Children));
        harness.Click(Draw, center);
        Assert.True(_on);

        harness.Click(Draw, center);
        Assert.False(_on);
    }

    [Fact]
    public void DisabledToggleKeepsItsValue()
    {
        using var harness = new FrameHarness();
        _on = true;
        void Draw(Gui gui) => gui.Toggle(ref _on, "Locked", enabled: false);

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(Assert.Single(harness.Gui.RootNode!.Children)));

        Assert.True(_on);
    }
}
