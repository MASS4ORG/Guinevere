using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

public sealed class DropdownKeyboardTests
{
    [Fact]
    public void NativeActivationOpensDropdownAndSelectsAPublishedOption()
    {
        using var h = new FrameHarness();
        var controls = new List<ControlSemantics>();
        var elements = new List<AccessibilityElement>();
        var sink = Substitute.For<IAccessibilityCapability, IAccessibilityTreeSink>();
        sink.When(bridge => bridge.Publish(Arg.Any<ControlSemantics>()))
            .Do(call => controls.Add(call.Arg<ControlSemantics>()));
        ((IAccessibilityTreeSink)sink).When(tree => tree.PublishElement(Arg.Any<AccessibilityElement>()))
            .Do(call => elements.Add(call.Arg<AccessibilityElement>()));
        h.Gui.Platform.Register(sink);
        var source = Substitute.For<IControlActivationSource>();
        h.Gui.ControlActivation = source;
        var selected = -1;
        void Draw(Gui gui) => gui.Dropdown(["One", "Two"], ref selected, placeholder: "Choice");
        h.Frame(Draw);
        h.Frame(Draw);
        var button = controls.Last(static item => item.Label == "Choice").Id;
        h.Gui.Focus.RequestFocus(button);
        h.Frame(Draw);
        source.IsActivated(button).Returns(true);
        h.Frame(Draw);
        source.IsActivated(button).Returns(false);
        h.Frame(Draw);
        Assert.True(elements.Last(static item => item.Name == "Choice").Expanded);
        var option = controls.Last(static item => item.Label == "Two").Id;
        h.Gui.Focus.RequestFocus(option);
        source.IsActivated(option).Returns(true);
        h.Frame(Draw);
        source.IsActivated(option).Returns(false);
        h.Frame(Draw);
        Assert.Equal(1, selected);
        Assert.False(elements.Last(static item => item.Name == "Choice").Expanded);
    }

    [Fact]
    public void DropdownKeyboardSelectionAndCancelAreDeferredAndKeepFocus()
    {
        using var h = new FrameHarness();
        var selected = -1;
        var controls = new List<ControlSemantics>();
        var sink = Substitute.For<IAccessibilityCapability>();
        sink.When(bridge => bridge.Publish(Arg.Any<ControlSemantics>())).Do(call => controls.Add(call.Arg<ControlSemantics>()));
        h.Gui.Platform.Register(sink);
        void Draw(Gui gui) => gui.Dropdown(["One", "Two", "Three"], ref selected, placeholder: "Choice");
        h.Frame(Draw);
        h.Frame(Draw);
        var button = controls.Last(static item => item.Label == "Choice").Id;
        h.Gui.Focus.RequestFocus(button);
        h.Frame(Draw);
        Key(h, Draw, KeyboardKey.Up);
        h.Frame(Draw);
        Key(h, Draw, KeyboardKey.Home);
        Key(h, Draw, KeyboardKey.Up);
        Key(h, Draw, KeyboardKey.Space);
        h.Frame(Draw);
        Assert.Equal(2, selected);
        Assert.Equal(button, h.Gui.Focus.CurrentFocusedId);
        Key(h, Draw, KeyboardKey.Enter);
        h.Frame(Draw);
        Key(h, Draw, KeyboardKey.Down);
        Key(h, Draw, KeyboardKey.Right);
        Key(h, Draw, KeyboardKey.Escape);
        h.Frame(Draw);
        Assert.Equal(2, selected);
        Assert.Equal(button, h.Gui.Focus.CurrentFocusedId);
        h.Gui.ClearDropdownStates();
        h.Frame(Draw);
        Key(h, Draw, KeyboardKey.End);
        Assert.Equal(2, selected);
    }

    [Fact]
    public void EmptyAndDisabledDropdownsStayClosedAndVectorButtonsKeepReadableLabels()
    {
        using var h = new FrameHarness();
        var controls = new List<ControlSemantics>();
        var sink = Substitute.For<IControlSemanticsSink>();
        sink.When(bridge => bridge.Publish(Arg.Any<ControlSemantics>())).Do(call => controls.Add(call.Arg<ControlSemantics>()));
        h.Gui.ControlSemantics = sink;
        var selected = 0;
        var clicked = false;
        void Draw(Gui gui)
        {
            gui.Dropdown([], ref selected, placeholder: "Empty");
            gui.Dropdown(["Disabled"], ref selected, placeholder: "Disabled choice", enabled: false);
            clicked |= gui.IconButton(Icon.FromGlyph("+"), "Add item", enabled: false, id: "vector-disabled");
        }
        h.Frame(Draw);
        var empty = controls.Last(static control => control.Label == "Empty").Id;
        h.Gui.Focus.RequestFocus(empty);
        h.Frame(Draw);
        Key(h, Draw, KeyboardKey.Down);
        Assert.False(clicked);
        Assert.Contains(controls, static control => control.Label == "Add item" && control.State.HasFlag(ControlVisualState.Disabled));
        Assert.Contains(controls, static control => control.Label == "Disabled choice" && control.State.HasFlag(ControlVisualState.Disabled));
    }

    static void Key(FrameHarness harness, Action<Gui> draw, KeyboardKey key)
    {
        harness.Input.PressKey(key);
        harness.Frame(draw);
        harness.Input.ReleaseKey(key);
        harness.Frame(draw);
    }
}
