using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Examples;

public class InputDevicesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectionalGridEdgesKeepFocusWhenAnotherControlIsBelow(bool stick)
    {
        using var harness = new FrameHarness(800, 900);
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        harness.Input.ConnectController("pad");
        void Draw(Gui gui)
        {
            Example_01.Shared.InputDevices(gui, Example_01.ColorPalette.Dark);
            using (gui.Node(100, 30, "outside").Absolute(0, 350).Enter()) gui.Pressable();
        }
        void Press(GameButton button, float x, float y)
        {
            if (stick)
            {
                harness.Input.SetControllerAxis("pad", 0, x);
                harness.Input.SetControllerAxis("pad", 1, y);
            }
            else harness.Input.SetControllerButton("pad", button, true);
            harness.Frame(Draw);
            harness.Input.SetControllerAxis("pad", 0, 0);
            harness.Input.SetControllerAxis("pad", 1, 0);
            harness.Input.SetControllerButton("pad", button, false);
            harness.Frame(Draw);
        }
        harness.Frame(Draw);
        var grid = harness.Gui.RootNode!.Children.First().Children
            .Single(node => node.Id == "controller-navigation-grid");
        var fourth = grid.Children[3].Id;
        var fifth = grid.Children[4].Id;
        harness.Gui.RequestFocus(fourth);
        harness.Frame(Draw);
        Press(GameButton.Down, 0, 1);
        Assert.Equal(fourth, harness.Gui.Focus.CurrentFocusedId);
        Press(GameButton.Right, 1, 0);
        Assert.Equal(fifth, harness.Gui.Focus.CurrentFocusedId);
        Press(GameButton.Down, 0, 1);
        Assert.Equal(fifth, harness.Gui.Focus.CurrentFocusedId);
        Press(GameButton.Left, -1, 0);
        Assert.Equal(fourth, harness.Gui.Focus.CurrentFocusedId);
    }

    [Fact]
    public void GalleryShowsDevicesAndReceivesNavigationAndTouch()
    {
        using var harness = new FrameHarness(800, 900);
        void Draw(Gui gui) => Example_01.Shared.InputDevices(gui, Example_01.ColorPalette.Dark);
        harness.Frame(Draw);
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        harness.Gui.Platform.Register<ITouchCapability>(harness.Input);
        harness.Frame(Draw);
        harness.Input.ConnectController("pad");
        harness.Input.SetControllerButton("pad", GameButton.South, true);
        harness.Input.SetControllerAxis("pad", 0, 0.7f);
        harness.Frame(Draw);
        var surface = harness.Gui.RootNode!.Children.Single().Children.Last();
        var position = FrameHarness.Center(surface);
        harness.Input.Touch(new TouchContact(1, position, 0.6f, TouchPhase.Began));
        harness.Frame(Draw);
        Assert.Equal(surface.Id, harness.Gui.TouchCapture(1));
        harness.Input.Touch(new TouchContact(1, position, null, TouchPhase.Ended));
        harness.Frame(Draw);
        Assert.Null(harness.Gui.TouchCapture(1));
        var grid = harness.Gui.RootNode!.Children.Single().Children.Single(node => node.Id == "controller-navigation-grid");
        var firstControl = grid.Children.First();
        harness.Gui.RequestFocus(firstControl.Id);
        harness.Input.SetControllerButton("pad", GameButton.South, false);
        harness.Input.SetControllerAxis("pad", 0, 0);
        harness.Frame(Draw);
        harness.Input.SetControllerButton("pad", GameButton.South, true);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(firstControl.Id, harness.Gui.Focus.CurrentFocusedId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GalleryNavigationCanBeActivatedByController(bool active, bool compact)
    {
        using var harness = new FrameHarness();
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        var activated = 0;
        void Draw(Gui gui)
        {
            if (Example_01.Shared.NavItem(gui, Example_01.ColorPalette.Dark, "Input", active, compact)) activated++;
        }
        harness.Frame(Draw);
        var node = Assert.Single(harness.Gui.RootNode!.Children);
        harness.Gui.RequestFocus(node.Id);
        harness.Input.MoveTo(FrameHarness.Center(node));
        harness.Frame(Draw);
        harness.Input.ConnectController("pad");
        harness.Input.SetControllerButton("pad", GameButton.South, true);
        harness.Frame(Draw);
        Assert.Equal(1, activated);
    }
}
