using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

public class SemanticInputTests
{
    [Fact]
    public void SemanticActionsNavigateAndActivateInTheNextBuildPass()
    {
        using var harness = new FrameHarness();
        var source = Substitute.For<ISemanticInputCapability>();
        source.ActiveDevice.Returns(new InputDevice("pad", InputDeviceKind.Controller));
        harness.Gui.Platform.Register(source);
        var activated = 0;
        void Draw(Gui gui)
        {
            using (gui.Node(80, 30, "first").Enter())
            {
                gui.Pressable();
            }

            using (gui.Node(80, 30, "second").Enter())
            {
                if (gui.Pressable().Activated)
                {
                    activated++;
                }
            }
        }
        harness.Frame(Draw);
        harness.Gui.RequestFocus("first");
        harness.Frame(Draw);
        source.IsPressed(SemanticAction.Down).Returns(true);
        harness.Frame(Draw);
        source.IsPressed(SemanticAction.Down).Returns(false);
        source.IsPressed(SemanticAction.Accept).Returns(true);
        harness.Frame(Draw);
        Assert.Equal("second", harness.Gui.Focus.CurrentFocusedId);
        Assert.Equal(0, activated);
        source.IsPressed(SemanticAction.Accept).Returns(false);
        harness.Frame(Draw);
        Assert.Equal(1, activated);
    }

    [Fact]
    public void PromptsFollowDeviceAndProviderChanges()
    {
        using var harness = new FrameHarness();
        var source = Substitute.For<ISemanticInputCapability>();
        var prompts = Substitute.For<IInputPromptProvider>();
        var pad = new InputDevice("pad", InputDeviceKind.Controller);
        source.ActiveDevice.Returns(pad);
        prompts.GetPrompt(SemanticAction.Accept, pad).Returns(new InputPrompt("A"));
        harness.Gui.Platform.Register(source);
        harness.Gui.Platform.Register(prompts);
        harness.Frame(_ => { });
        Assert.Equal("A", harness.Gui.GetInputPrompt(SemanticAction.Accept)?.Label);
        prompts.GetPrompt(SemanticAction.Accept, pad).Returns(new InputPrompt("Cross"));
        Assert.Equal("Cross", harness.Gui.GetInputPrompt(SemanticAction.Accept)?.Label);
        harness.Input.PressKey(KeyboardKey.Tab);
        harness.Frame(_ => { });
        Assert.Equal(InputDeviceKind.KeyboardMouse, harness.Gui.ActiveInputDevice.Kind);
        Assert.Null(harness.Gui.GetInputPrompt(SemanticAction.Accept));
        harness.Frame(_ => { });
        Assert.Equal(InputDeviceKind.KeyboardMouse, harness.Gui.ActiveInputDevice.Kind);
        harness.Gui.Platform.Remove<ISemanticInputCapability>();
        harness.Frame(_ => { });
        Assert.Equal(InputDeviceKind.KeyboardMouse, harness.Gui.ActiveInputDevice.Kind);
    }

    [Theory]
    [InlineData(FocusDirection.Left, -10, 0)]
    [InlineData(FocusDirection.Right, 10, 0)]
    [InlineData(FocusDirection.Up, 0, -10)]
    [InlineData(FocusDirection.Down, 0, 10)]
    public void DirectionalOverrideFallsBackWhenTargetIsDisabled(FocusDirection direction, float x, float y)
    {
        var focus = new FocusManager();
        void Register()
        {
            focus.RegisterFocusableControl("origin", Vector2.Zero);
            focus.RegisterFocusableControl("near", new Vector2(x, y));
            focus.RegisterFocusableControl("override", new Vector2(x * 4, y * 4));
        }
        focus.BeginFrame();
        Register();
        focus.RequestFocus("origin");
        focus.EndFrame();
        focus.BeginFrame();
        Register();
        focus.SetDirectionalNeighbor("origin", direction, "override");
        focus.Navigate(direction);
        focus.EndFrame();
        focus.BeginFrame();
        Assert.Equal("override", focus.CurrentFocusedId);
        Register();
        focus.RequestFocus("origin");
        focus.EndFrame();
        focus.BeginFrame();
        Register();
        focus.RegisterFocusableControl("override", canReceiveFocus: false);
        focus.SetDirectionalNeighbor("origin", direction, "override");
        focus.Navigate(direction);
        focus.EndFrame();
        focus.BeginFrame();
        Assert.Equal("near", focus.CurrentFocusedId);
    }

    [Fact]
    public void ClosingNestedScopesTogetherRestoresTheSurvivingOpener()
    {
        var focus = new FocusManager();
        focus.BeginFrame();
        focus.RegisterFocusableControl("opener");
        focus.RequestFocus("opener");
        focus.EndFrame();
        focus.BeginFrame();
        focus.RegisterFocusableControl("opener");
        using (var outer = focus.EnterScope("outer"))
        {
            outer.SetActive();
            focus.RegisterFocusableControl("outer-button");
            focus.RequestFocus("outer-button");
            using var inner = focus.EnterScope("inner");
            inner.SetActive();
            focus.RegisterFocusableControl("inner-button");
            focus.RequestFocus("inner-button");
        }
        focus.EndFrame();
        focus.BeginFrame();
        focus.RegisterFocusableControl("opener");
        focus.EndFrame();
        focus.BeginFrame();
        Assert.Null(focus.ActiveScopeId);
        Assert.Equal("opener", focus.CurrentFocusedId);
    }

    [Fact]
    public void ReopenedScopeRestoresRememberedControlOrFirstSurvivor()
    {
        var focus = new FocusManager();
        void Register(bool second = true)
        {
            using var scope = focus.EnterScope("screen");
            scope.SetActive();
            focus.RegisterFocusableControl("first");
            if (second)
            {
                focus.RegisterFocusableControl("second");
            }
        }
        focus.BeginFrame();
        Register();
        focus.RequestFocus("second");
        focus.EndFrame();
        focus.BeginFrame();
        Register();
        focus.EndFrame();
        focus.BeginFrame();
        focus.EndFrame();
        focus.BeginFrame();
        Register();
        focus.EndFrame();
        focus.BeginFrame();
        Assert.Equal("second", focus.CurrentFocusedId);
        Register(false);
        focus.EndFrame();
        focus.BeginFrame();
        Assert.Equal("first", focus.CurrentFocusedId);
    }
}
