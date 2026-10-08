using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the slider: clicking the track jumps the thumb, dragging keeps scrubbing beyond the widget,
/// steps snap to multiples, and the keyboard adjusts by one step once the slider has focus.
/// </summary>
public class SliderTests
{
    static readonly Font TestFont = Font.FromFamilyName("serif");

    sealed class Harness
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(400, 60));
        readonly IInputHandler _input = Substitute.For<IInputHandler>();
        readonly TestableGui _gui;

        public Harness(float initial = 5f)
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(400, 60);
            Value = initial;

            _input.MousePosition.Returns(new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(new Vector2(-100, -100));
            _input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public float Value { get; private set; }

        public void Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            KeyboardKey? key = null, float step = 0f, bool enabled = true)
        {
            _input.MousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            _input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => key is not null && call.Arg<KeyboardKey>() == key);

            var value = Value;

            void Draw() => _gui.Slider(ref value, 0f, 10f, step: step, width: 300, height: 30, enabled: enabled);

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, TestFont);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            Value = value;
        }
    }

    /// <summary>A track click changes the value to the pointer's position.</summary>
    [Fact]
    public void ClickingOnTheTrackJumpsTheThumb()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(225, 15), pressed: true, down: true);
        h.Frame(mouse: new Vector2(225, 15)); // release

        Assert.Equal(7.5f, h.Value, precision: 3);
    }

    /// <summary>A captured pointer keeps scrubbing outside the track.</summary>
    [Fact]
    public void DraggingBeyondTheTrackKeepsScrubbing()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(150, 15), pressed: true, down: true);
        h.Frame(mouse: new Vector2(1000, 15), down: true);
        h.Frame(mouse: new Vector2(1000, 15)); // release

        // The pointer is far off the right edge of the widget; the thumb still follows.
        Assert.Equal(10f, h.Value, precision: 3);
    }

    /// <summary>Pointer edits snap to the requested step.</summary>
    [Fact]
    public void StepSnapsToMultiples()
    {
        var h = new Harness(0f);

        h.Frame(mouse: new Vector2(219, 15), pressed: true, down: true, step: 0.5f);
        h.Frame(mouse: new Vector2(219, 15), step: 0.5f);

        // 219/300 of the 0..10 range is 7.3 which rounds to the nearest 0.5 multiple.
        Assert.Equal(7.5f, h.Value, precision: 3);
    }

    /// <summary>The focused slider consumes arrow keys for value edits.</summary>
    [Fact]
    public void ArrowsAdjustByOneStepWhenFocused()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(150, 15), pressed: true, down: true);
        h.Frame(mouse: new Vector2(150, 15)); // release; focus lands this frame

        Assert.Equal(5f, h.Value, precision: 3);

        h.Frame(key: KeyboardKey.Right);
        Assert.Equal(6f, h.Value, precision: 3);

        h.Frame(key: KeyboardKey.Left);
        Assert.Equal(5f, h.Value, precision: 3);
    }

    /// <summary>Disabled sliders leave the value unchanged.</summary>
    [Fact]
    public void DisabledSliderIgnoresInput()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(225, 15), pressed: true, down: true, enabled: false);
        h.Frame(mouse: new Vector2(225, 15), enabled: false);

        Assert.Equal(5f, h.Value, precision: 3);
    }

    /// <summary>Classes and ids reach directly selected parts, including geometry and rounded corners.</summary>
    [Fact]
    public void StylesControlPartGeometryAndColors()
    {
        using var harness = new FrameHarness(240, 60);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            slider.custom#volume { width = expand; height = 80; }
            slider.custom#volume > track { height = 10; background-color = #0000ff; }
            slider.custom#volume > fill { background-color = #ff0000; border-radius = 0; }
            slider.custom#volume > thumb {
                width = 22; height = 16; border-radius = 0; border-width = 0; background-color = #00ff00;
            }
            """));
        var value = 0.5f;
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            g.Slider(ref value, 0, 1, width: 200, height: 40, classes: ["custom"], id: "volume");
        });
        var node = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Equal(new Rect(0, 0, 200, 40), node.Rect);
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, pixels.GetPixel(10, 16));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(180, 16));
        Assert.Equal(new SKColor(0, 255, 0), pixels.GetPixel(90, 13));
        Assert.Equal(SKColors.White, pixels.GetPixel(180, 14));
    }

    /// <summary>Fill and thumb move in the same frame as a pointer edit.</summary>
    [Fact]
    public void PointerEditIsDrawnInTheSameFrame()
    {
        using var harness = new FrameHarness(200, 40);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("slider > fill { background-color = #ff0000; }"));
        var value = 0.25f;
        void Draw(Gui g)
        {
            g.DrawBackgroundRect(Color.White);
            g.Slider(ref value, 0, 1, width: 200, height: 40, id: "volume");
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(150, 20));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        Assert.Equal(0.75f, value);
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, pixels.GetPixel(130, 20));
        Assert.Equal(SKColors.White, pixels.GetPixel(150, 20));
    }

    /// <summary>Hover, press, focus and disabled rules reach the root and its parts.</summary>
    [Fact]
    public void InteractionStatesReachParts()
    {
        using var harness = new FrameHarness(200, 40);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            slider:hover > track { background-color = #0000ff; }
            slider:active > fill { background-color = #ff0000; }
            slider:focus > thumb { background-color = #00ff00; }
            slider:disabled > track { background-color = #ffff00; }
            """));
        var value = 0.5f;
        var enabled = true;
        void Draw(Gui g)
        {
            g.DrawBackgroundRect(Color.White);
            g.Slider(ref value, 0, 1, width: 200, height: 40, enabled: enabled, id: "volume");
        }
        SKColor Pixel(int x)
        {
            using var image = harness.Snapshot();
            using var pixels = SKBitmap.FromImage(image);
            return pixels.GetPixel(x, 20);
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(100, 20));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        Assert.Equal(SKColors.Blue, Pixel(180));
        Assert.Equal(SKColors.Red, Pixel(20));
        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.True(harness.Gui.HasFocus("volume"));
        Assert.Equal(new SKColor(0, 255, 0), Pixel(100));
        enabled = false;
        harness.Input.PressKey(KeyboardKey.End);
        harness.Frame(Draw);
        Assert.Equal(SKColors.Yellow, Pixel(180));
        Assert.Equal(0.5f, value);
    }

    /// <summary>Home and End use the range bounds, including reversed and collapsed ranges.</summary>
    [Theory]
    [InlineData(0f, 10f, KeyboardKey.Home, 0f)]
    [InlineData(0f, 10f, KeyboardKey.End, 10f)]
    [InlineData(10f, 0f, KeyboardKey.End, 10f)]
    [InlineData(5f, 5f, KeyboardKey.Right, 5f)]
    public void KeyboardUsesNormalizedBounds(float min, float max, KeyboardKey key, float expected)
    {
        using var harness = new FrameHarness(200, 40);
        var value = 5f;
        void Draw(Gui g) => g.Slider(ref value, min, max, width: 200, height: 40, step: 0.5f, id: "volume");
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(100, 20));
        harness.Input.PressKey(key);
        harness.Frame(Draw);
        Assert.Equal(expected, value);
    }

    /// <summary>A disabled slider cannot resume an earlier drag when enabled again.</summary>
    [Fact]
    public void DisablingCancelsTheDrag()
    {
        using var harness = new FrameHarness(200, 40);
        var value = 0.5f;
        var enabled = true;
        void Draw(Gui g) => g.Slider(ref value, 0, 1, width: 200, height: 40, enabled: enabled, id: "volume");
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(100, 20));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        enabled = false;
        harness.Input.MoveTo(new Vector2(180, 20));
        harness.Frame(Draw);
        enabled = true;
        harness.Frame(Draw);
        Assert.Equal(0.5f, value);
    }

    /// <summary>Value labels inherit stylesheet typography and allow an explicit font size.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(16f)]
    public void ValueLabelUsesTheScopeFont(float? fontSize)
    {
        using var harness = new FrameHarness(300, 60);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("slider { font-size = 22; gap = 12; }"));
        var value = 2.5f;
        harness.Frame(g => g.Slider(ref value, 0, 10, showValue: true, fontSize: fontSize));
        var node = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Equal(22f, node.Scope.Get<LayoutNodeScopeTextSize>().Value);
        Assert.Equal(12f, node.Style.Gap);
        Assert.Equal(2, node.Children.Count);
    }
}
