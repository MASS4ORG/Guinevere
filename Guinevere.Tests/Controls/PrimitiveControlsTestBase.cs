namespace Guinevere.Tests.Controls;

/// <summary>
/// Base class providing helper methods and mock setup for primitive control tests.
/// </summary>
public abstract class PrimitiveControlsTestBase
{
    protected Gui CreateTestGui()
    {
        var gui = new Gui();
        var canvas = CreateTestCanvas();
        gui.Input = CreateMockInput();
        gui.BeginFrame(canvas);
        return gui;
    }

    protected SKCanvas CreateTestCanvas()
    {
        var surface = SKSurface.Create(new SKImageInfo(800, 600));
        return surface.Canvas;
    }

    protected IInputHandler CreateMockInput()
    {
        var input = Substitute.For<IInputHandler>();

        // Default values for input
        input.MousePosition.Returns(new Vector2(0, 0));
        input.PrevMousePosition.Returns(new Vector2(0, 0));
        input.MouseDelta.Returns(new Vector2(0, 0));
        input.MouseWheelDelta.Returns(0f);
        input.IsAnyKeyDown.Returns(false);

        // Default to no input
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        input.IsKeyDown(Arg.Any<KeyboardKey>()).Returns(false);
        input.IsKeyUp(Arg.Any<KeyboardKey>()).Returns(false);
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonUp(Arg.Any<MouseButton>()).Returns(false);

        return input;
    }

    protected void SetupMousePosition(IInputHandler input, Vector2 position)
    {
        input.MousePosition.Returns(position);
    }

    protected void SetupMouseClick(IInputHandler input, Vector2 position, MouseButton button = MouseButton.Left)
    {
        input.MousePosition.Returns(position);
        input.IsMouseButtonPressed(button).Returns(true);
    }

    protected void SetupMouseDown(IInputHandler input, Vector2 position, MouseButton button = MouseButton.Left)
    {
        input.MousePosition.Returns(position);
        input.IsMouseButtonDown(button).Returns(true);
    }

    protected void SetupMouseHover(IInputHandler input, Vector2 position)
    {
        input.MousePosition.Returns(position);
    }

    protected void SetupKeyPress(IInputHandler input, KeyboardKey key)
    {
        input.IsKeyPressed(key).Returns(true);
    }

    protected void SetupKeyDown(IInputHandler input, KeyboardKey key)
    {
        input.IsKeyDown(key).Returns(true);
    }

    protected void SetupMouseWheel(IInputHandler input, float delta)
    {
        input.MouseWheelDelta.Returns(delta);
    }

    protected void SimulateBuildStage(Gui gui)
    {
        gui.SetStage(Pass.Pass1Build);
    }

    protected void SimulateRenderStage(Gui gui)
    {
        gui.SetStage(Pass.Pass2Render);
    }

    protected void AssertRectValues(Rect rect, float expectedX, float expectedY, float expectedW, float expectedH)
    {
        Assert.Equal(expectedX, rect.X, 2);
        Assert.Equal(expectedY, rect.Y, 2);
        Assert.Equal(expectedW, rect.W, 2);
        Assert.Equal(expectedH, rect.H, 2);
    }

    protected void SimulateFullControlLifecycle(Gui gui, Action buildAction, Action renderAction)
    {
        // Pass1Build pass
        SimulateBuildStage(gui);
        buildAction();

        // Switch to render pass
        SimulateRenderStage(gui);
        renderAction();
    }

    protected LayoutNode GetControlNode(Gui gui, string expectedId)
    {
        var node = gui.RootNode?.FindChildById(expectedId);
        Assert.NotNull(node);
        return node;
    }

    protected void AssertNodeExists(Gui gui, string expectedId)
    {
        var node = gui.RootNode?.FindChildById(expectedId);
        Assert.NotNull(node);
    }

    protected void AssertNodeHasSize(LayoutNode node, float expectedWidth, float expectedHeight)
    {
        Assert.Equal(expectedWidth, node.Rect.W, 2);
        Assert.Equal(expectedHeight, node.Rect.H, 2);
    }

    protected void AssertNoException(Action action)
    {
        // In xUnit v3, we don't use Assert.DoesNotThrow
        // We just call the action and let any exception fail the test naturally
        action();
    }

    protected void ResetInput(IInputHandler input)
    {
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonUp(Arg.Any<MouseButton>()).Returns(false);
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        input.IsKeyDown(Arg.Any<KeyboardKey>()).Returns(false);
        input.IsKeyUp(Arg.Any<KeyboardKey>()).Returns(false);
        input.MousePosition.Returns(new Vector2(0, 0));
        input.PrevMousePosition.Returns(new Vector2(0, 0));
        input.MouseDelta.Returns(new Vector2(0, 0));
        input.MouseWheelDelta.Returns(0f);
        input.IsAnyKeyDown.Returns(false);
    }
}
