using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the color picker parts and the assembled picker and field: pointer and keyboard editing of one color, hex
/// typing and clipboard round trips, swatches, the popup lifecycle, recents, and the shape and body extension points.
/// </summary>
public class ColorPickerTests
{
    static LayoutNode? Find(LayoutNode node, string type)
    {
        if (node.StyleTarget?.Type == type) return node;
        foreach (var child in node.Children)
            if (Find(child, type) is { } found)
                return found;
        return null;
    }

    static LayoutNode Require(Gui gui, string type) =>
        Find(gui.RootNode!, type) ?? throw new InvalidOperationException($"No {type} node.");

    /// <summary>A click on the rectangle picks saturation from x and value from y inside the border.</summary>
    [Fact]
    public void AreaClickPicksSaturationAndValue()
    {
        using var harness = new FrameHarness(300, 200);
        var value = new Hsva(120f, 0f, 0f);
        void Draw(Gui g) => g.ColorArea(ref value, width: 202, height: 102);
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(151, 26));

        Assert.Equal(120f, value.H);
        Assert.Equal(0.75f, value.S, 3);
        Assert.Equal(0.75f, value.V, 3);
    }

    /// <summary>A held drag keeps picking outside the area, clamped to its edges.</summary>
    [Fact]
    public void AreaDragClampsOutsideTheSurface()
    {
        using var harness = new FrameHarness(300, 200);
        var value = new Hsva(10f, 0.5f, 0.5f);
        void Draw(Gui g) => g.ColorArea(ref value, width: 202, height: 102);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(50, 50));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(500, 500));
        harness.Frame(Draw);

        Assert.Equal(1f, value.S);
        Assert.Equal(0f, value.V);
    }

    /// <summary>Once focused, arrows move the cursor by one percent of the surface.</summary>
    [Fact]
    public void ArrowsNudgeTheFocusedArea()
    {
        using var harness = new FrameHarness(300, 200);
        var value = new Hsva(0f, 0.5f, 0.5f);
        var changed = false;
        void Draw(Gui g) => changed = g.ColorArea(ref value, width: 202, height: 102, id: "area");
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(101, 51));
        harness.Input.PressKey(KeyboardKey.Right);
        harness.Frame(Draw);
        Assert.True(changed);
        harness.Input.ReleaseKey(KeyboardKey.Right);
        harness.Input.PressKey(KeyboardKey.Up);
        harness.Frame(Draw);

        Assert.Equal(0.51f, value.S, 3);
        Assert.Equal(0.51f, value.V, 3);
    }

    /// <summary>Disabled parts ignore the pointer.</summary>
    [Fact]
    public void DisabledAreaIgnoresInput()
    {
        using var harness = new FrameHarness(300, 200);
        var value = new Hsva(0f, 0.5f, 0.5f);
        void Draw(Gui g) => g.ColorArea(ref value, width: 202, height: 102, enabled: false);
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(10, 10));

        Assert.Equal(new Hsva(0f, 0.5f, 0.5f), value);
    }

    /// <summary>Strips map their channel along their axis; vertical strips grow upward and hue stops short of 360.</summary>
    [Theory]
    [InlineData(HsvChannel.Hue, true, 1f, 359.9f)]
    [InlineData(HsvChannel.Saturation, false, 0.25f, 0.25f)]
    [InlineData(HsvChannel.Value, true, 0.5f, 0.5f)]
    [InlineData(HsvChannel.Alpha, false, 1f, 1f)]
    public void StripClickSetsItsChannel(HsvChannel channel, bool vertical, float fraction, float expected)
    {
        using var harness = new FrameHarness(300);
        var value = new Hsva(0f, 0f, 0f, 0f);
        void Draw(Gui g) => g.ColorStrip(ref value, channel, vertical, length: 202, thickness: 16);
        harness.Frame(Draw);
        var at = vertical ? new Vector2(8, 1 + (1 - fraction) * 200) : new Vector2(1 + fraction * 200, 8);
        harness.Click(Draw, at);

        Assert.Equal(expected, ControlsExtensions.ChannelFraction(value, channel) * (channel == HsvChannel.Hue ? 360f : 1f), 2);
    }

    /// <summary>Arrows step a focused strip by one percent; vertical strips use up and down.</summary>
    [Fact]
    public void ArrowsStepTheFocusedStrip()
    {
        using var harness = new FrameHarness(300);
        var value = new Hsva(0f, 0f, 0.5f);
        void Draw(Gui g) => g.ColorStrip(ref value, HsvChannel.Value, vertical: true, length: 202);
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(7, 101));
        harness.Input.PressKey(KeyboardKey.Up);
        harness.Frame(Draw);

        Assert.Equal(0.51f, value.V, 3);
    }

    /// <summary>Typing a full eight-digit hex applies live; a short form applies on Enter.</summary>
    [Fact]
    public void HexTypingAppliesFullFormsLiveAndShortFormsOnEnter()
    {
        using var harness = new FrameHarness(300, 100);
        var value = Hsva.FromColor(Color.ParseHex("#000000ff"));
        void Draw(Gui g) => g.ColorHex(ref value, width: 280, id: "hex");
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(60, 12));
        SelectAll(harness, Draw);
        harness.Input.TypeText("#ff000080");
        harness.Frame(Draw);
        Assert.Equal(Color.ParseHex("#ff000080"), value.ToColor());

        SelectAll(harness, Draw);
        harness.Input.TypeText("0f0");
        harness.Frame(Draw);
        Assert.Equal(Color.ParseHex("#ff000080"), value.ToColor());
        harness.Input.PressKey(KeyboardKey.Enter);
        harness.Frame(Draw);

        Assert.Equal(Color.ParseHex("#00ff00ff"), value.ToColor());
    }

    /// <summary>Leaving the field with invalid text keeps the color and shows it again.</summary>
    [Fact]
    public void InvalidHexRevertsOnBlur()
    {
        using var harness = new FrameHarness(300, 100);
        var value = Hsva.FromColor(Color.ParseHex("#336699ff"));
        void Draw(Gui g)
        {
            g.ColorHex(ref value, width: 280, id: "hex");
            g.Node(50, 50);
        }
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(60, 12));
        SelectAll(harness, Draw);
        harness.Input.TypeText("nope");
        harness.Frame(Draw);
        harness.Gui.ClearFocus();
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.Equal(Color.ParseHex("#336699ff"), value.ToColor());
        Assert.False(harness.Gui.HasFocus("hex/text"));
    }

    /// <summary>The copy button writes #rrggbbaa and the paste button reads any accepted form.</summary>
    [Fact]
    public void ClipboardButtonsCopyAndPasteHex()
    {
        using var harness = new FrameHarness(300, 100);
        var value = Hsva.FromColor(Color.ParseHex("#a855f780"));
        void Draw(Gui g) => g.ColorHex(ref value, width: 280, height: 24, id: "hex");
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(244, 12));
        Assert.Equal("#a855f780", harness.Input.GetClipboardText());

        harness.Input.SetClipboardText("  \"0x11223344\" ");
        harness.Click(Draw, new Vector2(270, 12));

        Assert.Equal(Color.ParseHex("#11223344"), value.ToColor());
    }

    /// <summary>Without alpha the hex is six digits and a pasted color keeps the current alpha.</summary>
    [Fact]
    public void HexWithoutAlphaKeepsTheCurrentAlpha()
    {
        using var harness = new FrameHarness(300, 100);
        var value = Hsva.FromColor(Color.ParseHex("#a855f780"));
        void Draw(Gui g) => g.ColorHex(ref value, alpha: false, width: 280, height: 24, id: "hex");
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(244, 12));
        Assert.Equal("#a855f7", harness.Input.GetClipboardText());
        harness.Input.SetClipboardText("#102030");
        harness.Click(Draw, new Vector2(270, 12));

        Assert.Equal(Color.ParseHex("#10203080"), value.ToColor());
    }

    /// <summary>Clipboard text parses with or without '#', 0x, quotes and whitespace; other text is rejected.</summary>
    [Theory]
    [InlineData("#abc", "#aabbccff")]
    [InlineData("abcd", "#aabbccdd")]
    [InlineData(" 0x112233 ", "#112233ff")]
    [InlineData("'#11223344'", "#11223344")]
    [InlineData("rgb(1,2,3)", null)]
    [InlineData("", null)]
    public void ParsesColorText(string text, string? expected)
    {
        var parsed = ControlsExtensions.TryParseColorText(text, out var color);
        Assert.Equal(expected is not null, parsed);
        if (expected is not null) Assert.Equal(expected, color.ToHex());
    }

    /// <summary>A committed channel edit rebuilds the color through the mode.</summary>
    [Fact]
    public void ChannelFieldCommitRebuildsTheColor()
    {
        using var harness = new FrameHarness(400, 100);
        var value = Hsva.FromColor(Color.ParseHex("#000000ff"));
        void Draw(Gui g) => g.ColorChannels(ref value, ColorPickerModes.Rgb, id: "channels");
        harness.Frame(Draw);
        var red = Require(harness.Gui, "color-channel").Children[1];
        harness.Click(Draw, FrameHarness.Center(red));
        SelectAll(harness, Draw);
        harness.Input.TypeText("255");
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.Enter);
        harness.Frame(Draw);

        Assert.Equal(Color.ParseHex("#ff0000ff"), value.ToColor());
    }

    /// <summary>Clicking a swatch picks its color and marks it selected.</summary>
    [Fact]
    public void SwatchClickPicksTheColor()
    {
        using var harness = new FrameHarness(300, 100);
        var value = new Hsva(200f, 0f, 0.5f);
        var picked = false;
        Color[] colors = [Color.ParseHex("#ff0000"), Color.ParseHex("#00ff00")];
        void Draw(Gui g) => picked |= g.ColorSwatches(ref value, colors);
        harness.Frame(Draw);
        var second = Require(harness.Gui, "swatches").Children[1];
        harness.Click(Draw, FrameHarness.Center(second));

        Assert.True(picked);
        Assert.Equal(colors[1], value.ToColor());
    }

    /// <summary>Clicking the "before" half of the preview reports a restore request; the other half does not.</summary>
    [Fact]
    public void PreviewReportsClicksOnTheBeforeHalf()
    {
        using var harness = new FrameHarness(200, 100);
        var restore = false;
        void Draw(Gui g) => restore |= g.ColorPreview(Color.Red, Color.Blue, width: 100, height: 20);
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(75, 10));
        Assert.False(restore);
        harness.Click(Draw, new Vector2(25, 10));
        Assert.True(restore);
    }

    /// <summary>The Color overload keeps hue and saturation while the color passes through black.</summary>
    [Fact]
    public void PickerKeepsHueThroughBlack()
    {
        using var harness = new FrameHarness(400, 500);
        var color = Color.ParseHex("#ff8000");
        void Draw(Gui g) => g.ColorPicker(ref color, id: "picker");
        harness.Frame(Draw);
        var area = Require(harness.Gui, "color-area");
        harness.Input.MoveTo(new Vector2(area.Rect.X + area.Rect.W - 2, area.Rect.Y + area.Rect.H - 2));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(area.Rect.X + area.Rect.W - 2, area.Rect.Y + area.Rect.H + 20));
        harness.Frame(Draw);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        Assert.Equal(Color.ParseHex("#000000"), color);
        harness.Input.MoveTo(new Vector2(area.Rect.X + area.Rect.W - 2, area.Rect.Y + 2));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(area.Rect.X + area.Rect.W + 20, area.Rect.Y - 20));
        harness.Frame(Draw);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);

        Assert.Equal(Color.ParseHex("#ff8000"), color);
    }

    /// <summary>A color field opens its popup on the next frame, edits through it, and records the result on close.</summary>
    [Fact]
    public void FieldPopupEditsAndRecordsRecents()
    {
        using var harness = new FrameHarness(500, 600);
        var color = Color.ParseHex("#ff0000");
        var changed = false;
        void Draw(Gui g) => changed |= g.ColorField(ref color, id: "field");
        harness.Frame(Draw);
        var swatch = Require(harness.Gui, "swatch");
        harness.Click(Draw, FrameHarness.Center(swatch));
        Assert.NotNull(Find(harness.Gui.RootNode!, "color-picker"));

        var area = Require(harness.Gui, "color-area");
        harness.Click(Draw, new Vector2(area.Rect.X + 1, area.Rect.Y + 1));
        Assert.True(changed);
        Assert.Equal(Color.ParseHex("#ffffff"), color);

        harness.Input.PressKey(KeyboardKey.Escape);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Null(Find(harness.Gui.RootNode!, "color-picker"));
        Assert.Equal([Color.ParseHex("#ffffff")], ColorRecents.For(harness.Gui).Items);
    }

    /// <summary>A second click on the swatch closes the popup without recording an unchanged color.</summary>
    [Fact]
    public void SwatchTogglesThePopup()
    {
        using var harness = new FrameHarness(500, 600);
        var color = Color.ParseHex("#ff0000");
        void Draw(Gui g) => g.ColorField(ref color, id: "field");
        harness.Frame(Draw);
        var center = FrameHarness.Center(Require(harness.Gui, "swatch"));
        harness.Click(Draw, center);
        Assert.NotNull(Find(harness.Gui.RootNode!, "color-picker"));
        harness.Click(Draw, center);
        harness.Frame(Draw);

        Assert.Null(Find(harness.Gui.RootNode!, "color-picker"));
        Assert.Empty(ColorRecents.For(harness.Gui).Items);
    }

    /// <summary>A third-party shape plugs into the area: it draws, places the cursor and maps the pointer.</summary>
    [Fact]
    public void ThirdPartyShapeDrivesTheArea()
    {
        using var harness = new FrameHarness(300, 200);
        var shape = new HueBarShape();
        var value = new Hsva(0f, 1f, 1f);
        void Draw(Gui g) => g.ColorArea(ref value, shape, width: 202, height: 52);
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(101, 20));

        Assert.True(shape.Draws > 0);
        Assert.Equal(180f, value.H, 1);
    }

    /// <summary>A body replaces the default layout and still reports changes through the picker.</summary>
    [Fact]
    public void BodyReplacesTheDefaultLayout()
    {
        using var harness = new FrameHarness(300);
        var options = new ColorPickerOptions
        {
            Body = (Gui g, ref Hsva v, Color? _) => g.ColorStrip(ref v, HsvChannel.Hue, length: 202)
        };
        var value = new Hsva(0f, 1f, 1f);
        var changed = false;
        void Draw(Gui g) => changed |= g.ColorPicker(ref value, options);
        harness.Frame(Draw);
        Assert.Null(Find(harness.Gui.RootNode!, "color-area"));
        var strip = Require(harness.Gui, "color-strip");
        harness.Click(Draw, new Vector2(strip.Rect.X + 101, strip.Rect.Y + 7));

        Assert.True(changed);
        Assert.Equal(180f, value.H, 0);
    }

    /// <summary>The rectangle shows white, the hue and black at its corners; the wheel shows red at three o'clock.</summary>
    [Fact]
    public void ShapesPaintTheirSurfaces()
    {
        using var harness = new FrameHarness(420, 210);
        var value = new Hsva(240f, 1f, 1f);
        harness.Frame(g =>
        {
            using (g.Node().Expand().Direction(Axis.Horizontal).Enter())
            {
                g.ColorArea(ref value, ColorPickerShapes.Rectangle, width: 202, height: 202);
                g.ColorArea(ref value, ColorPickerShapes.Wheel, width: 202, height: 202);
            }
        });
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);

        AssertNear(SKColors.White, pixels.GetPixel(2, 2));
        AssertNear(SKColors.Blue, pixels.GetPixel(199, 2));
        AssertNear(SKColors.Black, pixels.GetPixel(100, 200));
        AssertNear(SKColors.Red, pixels.GetPixel(202 + 196, 101));
        AssertNear(SKColors.White, pixels.GetPixel(202 + 101, 101));
    }

    static void AssertNear(SKColor expected, SKColor actual, int tolerance = 24)
    {
        Assert.InRange(actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
        Assert.InRange(actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
        Assert.InRange(actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
    }

    /// <summary>Modes and shapes expose names for switches and menus.</summary>
    [Fact]
    public void ExtensionPointsHaveNames()
    {
        Assert.Equal(["RGB", "HSV", "HSL"], ColorPickerModes.Default.Select(mode => mode.Name));
        Assert.Equal("Linear", ColorPickerModes.Linear.Name);
        Assert.Equal(["Rectangle", "Wheel"], ColorPickerShapes.All.Select(shape => shape.Name));
        Assert.Equal([HsvChannel.Hue, HsvChannel.Value], ColorPickerShapes.All.Select(shape => shape.Companion));
    }

    /// <summary>Per-GUI defaults apply when no options are passed and can be reset.</summary>
    [Fact]
    public void DefaultsArePerGui()
    {
        using var harness = new FrameHarness();
        var custom = new ColorPickerOptions { Alpha = false };
        ColorPickerOptions.SetDefault(harness.Gui, custom);
        Assert.Same(custom, ColorPickerOptions.For(harness.Gui));
        Assert.Same(ColorPickerOptions.Default, ColorPickerOptions.For(new Gui()));
        ColorPickerOptions.SetDefault(harness.Gui, null);
        Assert.Same(ColorPickerOptions.Default, ColorPickerOptions.For(harness.Gui));
    }

    /// <summary>Recents keep the newest first, move repeats to the front and drop the oldest.</summary>
    [Fact]
    public void RecentsAreNewestFirstAndBounded()
    {
        var recents = new ColorRecents(2);
        recents.Add(Color.Red);
        recents.Add(Color.Blue);
        recents.Add(Color.Red);
        recents.Add(Color.Lime);

        Assert.Equal([Color.Lime, Color.Red], recents.Items);
        recents.Clear();
        Assert.Empty(recents.Items);
    }

    /// <summary>An eyedropper button appears with a sampler and applies its result once it completes.</summary>
    [Fact]
    public void EyedropperAppliesTheSampledColor()
    {
        using var harness = new FrameHarness(400, 500);
        var sampler = new Sampler();
        harness.Gui.Platform.Register<IColorSamplerCapability>(sampler);
        var color = Color.ParseHex("#ff0000");
        void Draw(Gui g) => g.ColorPicker(ref color, id: "picker");
        harness.Frame(Draw);
        var hex = Require(harness.Gui, "color-hex");
        var row = hex.Parent!;
        var eyedropper = row.Children[^1];
        harness.Click(Draw, FrameHarness.Center(eyedropper));
        Assert.Equal(1, sampler.Calls);
        sampler.Result.SetResult(Color.ParseHex("#0000ff"));
        harness.Frame(Draw);

        Assert.Equal(Color.ParseHex("#0000ff"), color);
    }

    /// <summary>The popup height estimate grows with mode, channel and swatch rows.</summary>
    [Fact]
    public void EstimatesThePopupHeight()
    {
        var bare = new ColorPickerOptions { Modes = [], Swatches = [], ShowRecent = false, AreaHeight = 100 };
        Assert.Equal(100f + 8f + ControlMetrics.CompactHeight, ControlsExtensions.EstimatePickerHeight(bare, 0));
        var full = ColorPickerOptions.Default;
        Assert.True(ControlsExtensions.EstimatePickerHeight(full, 3) >
                    ControlsExtensions.EstimatePickerHeight(full, 0));
    }

    static void SelectAll(FrameHarness harness, Action<Gui> draw)
    {
        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Input.PressKey(KeyboardKey.A);
        harness.Frame(draw);
        harness.Input.ReleaseKey(KeyboardKey.A);
        harness.Input.ReleaseKey(KeyboardKey.LeftControl);
    }

    sealed class HueBarShape : IColorPickerShape
    {
        public int Draws;
        public string Name => "Hue bar";
        public HsvChannel Companion => HsvChannel.Value;
        public void Draw(Gui gui, Rect rect, Hsva value) => Draws++;
        public Vector2 Cursor(Rect rect, Hsva value) => new(rect.X + value.H / 360f * rect.W, rect.Y + rect.H / 2f);
        public Hsva Pick(Rect rect, Vector2 point, Hsva value) =>
            value with { H = Math.Clamp((point.X - rect.X) / rect.W, 0f, 1f) * 360f };
    }

    sealed class Sampler : IColorSamplerCapability
    {
        public readonly TaskCompletionSource<Color?> Result = new();
        public int Calls;

        public ValueTask<Color?> SampleAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return new ValueTask<Color?>(Result.Task);
        }
    }
}
