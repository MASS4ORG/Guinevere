using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

public class GamepadNavigationTests
{
    [Fact]
    public void ControllerEntersNestedMenuAndActivatesTheLeaf()
    {
        using var harness = new FrameHarness(700, 400);
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        var open = true;
        var activated = 0;
        void Draw(Gui gui) => gui.CascadeMenu(ref open, new Vector2(20, 20), menu => menu
            .Item("Disabled", enabled: false).Submenu("More", submenu => submenu.Item("Run", () => activated++)));
        void Press(GameButton button)
        {
            harness.Input.SetControllerButton("pad", button, true);
            harness.Frame(Draw);
            harness.Input.SetControllerButton("pad", button, false);
            harness.Frame(Draw);
        }
        harness.Frame(Draw);
        harness.Input.ConnectController("pad");
        Press(GameButton.Down);
        Press(GameButton.Right);
        Press(GameButton.South);
        Assert.Equal(1, activated);
        Assert.False(open);
    }

    [Theory]
    [InlineData(GameButton.Left, FocusDirection.Left, 0, 100)]
    [InlineData(GameButton.Right, FocusDirection.Right, 200, 100)]
    [InlineData(GameButton.Up, FocusDirection.Up, 100, 0)]
    [InlineData(GameButton.Down, FocusDirection.Down, 100, 200)]
    public void GuiRoutesAllFourDirectionsAndRetainsControlsThatClaimThem(GameButton button,
        FocusDirection direction, float x, float y)
    {
        using var harness = new FrameHarness();
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        var claims = false;
        void Draw(Gui gui)
        {
            using (gui.Node(40, 40, "origin").Absolute(100, 100).Enter())
                if (gui.Pass == Pass.Pass2Render) gui.RegisterFocusable(claimsArrowKeys: claims);
            using (gui.Node(40, 40, "target").Absolute(x, y).Enter()) gui.Pressable();
        }
        harness.Frame(Draw);
        harness.Gui.RequestFocus("origin");
        harness.Frame(Draw);
        harness.Input.ConnectController("pad");
        harness.Input.SetControllerButton("pad", button, true);
        harness.Frame(Draw);
        Assert.True(harness.Gui.IsNavigationPressed(direction));
        harness.Input.SetControllerButton("pad", button, false);
        harness.Frame(Draw);
        Assert.Equal("target", harness.Gui.Focus.CurrentFocusedId);
        claims = true;
        harness.Gui.RequestFocus("origin");
        harness.Frame(Draw);
        harness.Input.SetControllerButton("pad", button, true);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal("origin", harness.Gui.Focus.CurrentFocusedId);
        Assert.False(harness.Gui.IsNavigationPressed(FocusDirection.Next));
    }

    [Theory]
    [InlineData(GameButton.Left, SemanticAction.Left)]
    [InlineData(GameButton.Right, SemanticAction.Right)]
    [InlineData(GameButton.Up, SemanticAction.Up)]
    [InlineData(GameButton.Down, SemanticAction.Down)]
    public void DpadRepeatsAfterSharedDelayAndStopsOnDisconnect(GameButton button, SemanticAction action)
    {
        var input = new ScriptedInputHandler();
        var navigation = new GamepadNavigation();
        input.ConnectController("pad");
        input.SetControllerButton("pad", button, true);
        navigation.Update(input, 0);
        Assert.True(navigation.IsPressed(action));
        Assert.True(navigation.HasPendingRepeat);
        input.NewFrame();
        navigation.Update(input, 0.39);
        Assert.False(navigation.IsPressed(action));
        navigation.Update(input, 0.4);
        Assert.True(navigation.IsPressed(action));
        navigation.Update(input, 0.47);
        Assert.False(navigation.IsPressed(action));
        navigation.Update(input, 0.48);
        Assert.True(navigation.IsPressed(action));
        input.DisconnectController("pad");
        navigation.Update(input, 1);
        Assert.False(navigation.IsPressed(action));
        Assert.Equal(InputDeviceKind.KeyboardMouse, navigation.ActiveDevice.Kind);
        Assert.False(navigation.HasPendingRepeat);
        input.ConnectController("pad");
        input.SetControllerButton("pad", button, true);
        navigation.Update(input, 1.01);
        Assert.True(navigation.IsPressed(action));
    }

    [Theory]
    [InlineData(-0.8f, 0, SemanticAction.Left)]
    [InlineData(0.8f, 0, SemanticAction.Right)]
    [InlineData(0, -0.8f, SemanticAction.Up)]
    [InlineData(0, 0.8f, SemanticAction.Down)]
    public void StickUsesDeadZoneAndDominantAxis(float x, float y, SemanticAction action)
    {
        var input = new ScriptedInputHandler();
        var navigation = new GamepadNavigation();
        input.ConnectController("pad");
        input.SetControllerAxis("pad", 0, 0.24f);
        input.SetControllerAxis("pad", 1, -0.24f);
        navigation.Update(input, 0);
        Assert.All(Enum.GetValues<SemanticAction>(), item => Assert.False(navigation.IsPressed(item)));
        input.SetControllerAxis("pad", 0, x);
        input.SetControllerAxis("pad", 1, y);
        navigation.Update(input, 0.1);
        Assert.True(navigation.IsPressed(action));
        input.SetControllerButton("pad", GameButton.Down, true);
        navigation.Update(input, 0.2);
        Assert.False(navigation.IsPressed(SemanticAction.Left));
        Assert.False(navigation.IsPressed(SemanticAction.Right));
        input.SetControllerButton("pad", GameButton.Down, false);
        input.SetControllerAxis("pad", 0, 0);
        input.SetControllerAxis("pad", 1, 0);
        navigation.Update(input, 1);
        Assert.False(navigation.IsPressed(action));
    }

    [Fact]
    public void ButtonsCanBeRemappedAndSourcesAreOptional()
    {
        var input = new ScriptedInputHandler();
        var navigation = new GamepadNavigation();
        navigation.Buttons[SemanticAction.Accept] = GameButton.North;
        input.ConnectController("one");
        input.ConnectController("two");
        input.SetControllerButton("two", GameButton.North, true);
        input.SetControllerButton("one", GameButton.East, true);
        input.SetControllerButton("one", GameButton.West, true);
        input.SetControllerButton("one", GameButton.LeftShoulder, true);
        input.SetControllerButton("one", GameButton.RightShoulder, true);
        navigation.Update(input, 0);
        Assert.True(navigation.IsPressed(SemanticAction.Accept));
        Assert.True(navigation.IsPressed(SemanticAction.Cancel));
        Assert.True(navigation.IsPressed(SemanticAction.Context));
        Assert.True(navigation.IsPressed(SemanticAction.PreviousTab));
        Assert.True(navigation.IsPressed(SemanticAction.NextTab));
        Assert.Equal("two", navigation.ActiveDevice.Id);
        input.NewFrame();
        navigation.Update(input, 1);
        Assert.False(navigation.IsPressed(SemanticAction.Accept));
        navigation.Enabled = false;
        input.SetControllerButton("one", GameButton.Down, true);
        navigation.Update(input, 2);
        Assert.False(navigation.IsPressed(SemanticAction.Down));
    }

    [Theory]
    [InlineData(float.NaN, 0.4, 0.08)]
    [InlineData(1, 0.4, 0.08)]
    [InlineData(0.25, -1, 0.08)]
    [InlineData(0.25, double.NaN, 0.08)]
    [InlineData(0.25, 0.4, 0)]
    [InlineData(0.25, 0.4, double.PositiveInfinity)]
    public void InvalidSettingsAreRejected(float deadZone, double delay, double interval)
    {
        var navigation = new GamepadNavigation { Options = new GamepadNavigationOptions(deadZone, delay, interval) };
        Assert.Throws<ArgumentOutOfRangeException>(() => navigation.Update(new GameInputState(), 0));
    }

    [Fact]
    public void ScriptedControllerNavigatesActivatesAndCancelsDialog()
    {
        using var harness = new FrameHarness(600, 400);
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        var activations = 0;
        var open = true;
        void Draw(Gui gui)
        {
            gui.Dialog(ref open, "Controller", () =>
            {
                using (gui.Node(80, 30, "first").Enter()) gui.Pressable();
                using (gui.Node(80, 30, "second").Enter())
                {
                    gui.SetFocusDirectionalNeighbor(FocusDirection.Down, "first");
                    if (gui.Pressable().Activated) activations++;
                }
            }, width: 200, height: 150, id: "dialog");
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Input.ConnectController("xbox");
        harness.Input.SetControllerButton("xbox", GameButton.Down, true);
        harness.Frame(Draw);
        harness.Input.SetControllerButton("xbox", GameButton.Down, false);
        harness.Input.SetControllerButton("xbox", GameButton.South, true);
        harness.Frame(Draw);
        harness.Input.SetControllerButton("xbox", GameButton.South, false);
        harness.Frame(Draw);
        Assert.Equal("second", harness.Gui.Focus.CurrentFocusedId);
        Assert.Equal(1, activations);
        harness.Input.SetControllerButton("xbox", GameButton.East, true);
        harness.Frame(Draw);
        Assert.False(open);
        harness.Input.DisconnectController("xbox");
        harness.Frame(Draw);
        Assert.Null(harness.Gui.Focus.ActiveScopeId);
        Assert.False(harness.Gui.IsSemanticActionPressed(SemanticAction.Accept));
        Assert.Null(harness.Gui.PointerCapture);
    }

    [Fact]
    public void ExcaliburButtonAcceptsControllerAndKeyboardStillNavigatesWithoutOne()
    {
        using var harness = new FrameHarness();
        harness.Gui.Platform.Register<IGameInputCapability>(harness.Input);
        var activations = 0;
        void Draw(Gui gui)
        {
            if (gui.Button("First", id: "first")) activations++;
            gui.Button("Second", id: "second");
        }
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.Tab);
        harness.Frame(Draw);
        harness.Input.ReleaseKey(KeyboardKey.Tab);
        harness.Input.ConnectController("pad");
        harness.Input.SetControllerButton("pad", GameButton.South, true);
        harness.Frame(Draw);
        Assert.Equal(1, activations);
        Assert.True(harness.Gui.IsAcceptPressed);
        harness.Input.DisconnectController("pad");
        harness.Input.PressKey(KeyboardKey.Tab);
        harness.Frame(Draw);
        harness.Input.ReleaseKey(KeyboardKey.Tab);
        harness.Frame(Draw);
        Assert.Equal("second", harness.Gui.Focus.CurrentFocusedId);
        Assert.False(harness.Gui.IsCancelPressed);
    }
}
