namespace Guinevere.Tests.Interaction;

/// <summary>
/// Covers the programmable input device used by tests and by the Studio's <c>--script</c> mode.
/// </summary>
public class ScriptedInputHandlerTests
{
    [Fact]
    public void APressIsAnEdgeThatLastsExactlyOneFrame()
    {
        var input = new ScriptedInputHandler();

        input.PressButton();
        Assert.True(input.IsMouseButtonPressed(MouseButton.Left));
        Assert.True(input.IsMouseButtonDown(MouseButton.Left));

        input.NewFrame();
        Assert.False(input.IsMouseButtonPressed(MouseButton.Left));
        Assert.True(input.IsMouseButtonDown(MouseButton.Left));

        input.ReleaseButton();
        Assert.True(input.IsMouseButtonUp(MouseButton.Left));
        Assert.False(input.IsMouseButtonDown(MouseButton.Left));
    }

    [Fact]
    public void HoldingAButtonAcrossFramesDoesNotRepeatThePressEdge()
    {
        var input = new ScriptedInputHandler();

        input.PressButton();
        input.NewFrame();
        input.PressButton();

        Assert.False(input.IsMouseButtonPressed(MouseButton.Left));
    }

    [Fact]
    public void ThePointersPreviousPositionAdvancesOncePerFrame()
    {
        var input = new ScriptedInputHandler();
        input.MoveTo(10, 10);
        input.NewFrame();

        input.MoveTo(40, 10);
        Assert.Equal(new Vector2(10, 10), input.PrevMousePosition);
        Assert.Equal(new Vector2(30, 0), input.MouseDelta);

        input.NewFrame();
        Assert.Equal(new Vector2(40, 10), input.PrevMousePosition);
        Assert.Equal(Vector2.Zero, input.MouseDelta);
    }

    [Fact]
    public void KeysAndTypedTextFollowTheSameFrameRules()
    {
        var input = new ScriptedInputHandler();

        input.PressKey(KeyboardKey.A);
        input.TypeText("hi");
        Assert.True(input.IsKeyPressed(KeyboardKey.A));
        Assert.True(input.IsAnyKeyDown);
        Assert.Equal("hi", input.GetTypedCharacters());

        input.NewFrame();
        Assert.False(input.IsKeyPressed(KeyboardKey.A));
        Assert.True(input.IsKeyDown(KeyboardKey.A));
        Assert.Equal(string.Empty, input.GetTypedCharacters());

        input.ReleaseKey(KeyboardKey.A);
        Assert.True(input.IsKeyUp(KeyboardKey.A));
        Assert.False(input.IsAnyKeyDown);
    }

    [Fact]
    public void ResetLetsGoOfEverythingHeld()
    {
        var input = new ScriptedInputHandler();
        input.PressButton();
        input.PressKey(KeyboardKey.LeftControl);

        input.Reset();

        Assert.False(input.IsMouseButtonDown(MouseButton.Left));
        Assert.False(input.IsAnyKeyDown);
    }

    [Fact]
    public void ScrollAndClipboardRoundTrip()
    {
        var input = new ScriptedInputHandler();

        input.Scroll(-3f);
        Assert.Equal(-3f, input.MouseWheelDelta);
        input.NewFrame();
        Assert.Equal(0f, input.MouseWheelDelta);

        input.SetClipboardText("copied");
        Assert.Equal("copied", input.GetClipboardText());
    }

    [Fact]
    public void ItDrivesARealFrameEndToEnd()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var input = new ScriptedInputHandler();
        var gui = new Gui { Input = input };
        var clicked = false;

        input.MoveTo(50, 50);
        input.PressButton();

        void Draw()
        {
            using (gui.Node(100, 100, "target").Enter())
                if (gui.Pass == Pass.Pass2Render)
                    clicked |= gui.GetInteractable().OnClick();
        }

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.EndFrame();

        Assert.True(clicked);
    }
}
