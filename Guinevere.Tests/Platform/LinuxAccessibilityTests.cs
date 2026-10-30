using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Platform;

public sealed class LinuxAccessibilityTests
{
    [Fact]
    public void ControlRequestsApplyAtTheFrameBoundaryAndOtherScopesRemainQueued()
    {
        using var harness = new FrameHarness();
        using var bridge = new LinuxAccessibility(harness.Gui, "Test", static () => default, false);
        harness.Gui.Platform.Register<IAccessibilityCapability>(bridge);
        harness.Gui.Platform.Register<IControlActivationSource>(bridge);
        var activations = 0;
        void Draw(Gui gui)
        {
            using var node = gui.Node(100, 30, "button").Enter();
            if (gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button, Label: "Command")).Activated)
            {
                activations++;
            }
        }
        harness.Frame(Draw);
        bridge.Enqueue("another-graph", new AccessibilityAction("Focus", "node"));
        bridge.Enqueue("", new AccessibilityAction("Activate", "button"));
        bridge.Enqueue("", new AccessibilityAction("Focus"));
        harness.Frame(Draw);
        Assert.True(bridge.IsActivated("button"));
        Assert.True(bridge.IsActivated("button"));
        harness.Frame(Draw);
        Assert.Equal(1, activations);
        Assert.False(bridge.IsActivated("button"));
        Assert.False(bridge.TryDequeue("missing", out _));
        Assert.True(bridge.TryDequeue("another-graph", out var action));
        Assert.Equal("node", action.Key);
        Assert.False(bridge.TryDequeue("another-graph", out _));
    }

    [Fact]
    public void DisposalPreservesReplacementServices()
    {
        var gui = new Gui();
        using var bridge = new LinuxAccessibility(gui, "Test", static () => default, false);
        gui.Platform.Register<IAccessibilityCapability>(bridge);
        gui.Platform.Register<IAccessibilityTreeSink>(bridge);
        gui.Platform.Register<IAccessibilityActionSource>(bridge);
        gui.Platform.Register<IControlActivationSource>(bridge);
        var replacement = Substitute.For<IAccessibilityCapability>();
        gui.Platform.Register(replacement);
        Assert.Null(LinuxAccessibility.TryRegister(gui, "Test", static () => default));
        bridge.Dispose();
        Assert.Same(replacement, gui.Platform.Require<IAccessibilityCapability>());
        Assert.False(gui.Platform.Supports<IAccessibilityTreeSink>());
        Assert.False(gui.Platform.Supports<IAccessibilityActionSource>());
        Assert.False(gui.Platform.Supports<IControlActivationSource>());
    }
}
