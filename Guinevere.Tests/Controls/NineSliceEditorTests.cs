using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Checks slice authoring input, instance isolation and visual previews.</summary>
public sealed class NineSliceEditorTests
{
    static SKImage Texture()
    {
        using var bitmap = new SKBitmap(24, 24);
        bitmap.Erase(SKColors.Green);
        for (var y = 4; y < 20; y++)
        {
            for (var x = 4; x < 20; x++)
            {
                bitmap.SetPixel(x, y, SKColors.Blue);
            }
        }

        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>Each guide edits its own source-pixel inset, captures the pointer and stops on release.</summary>
    [Theory]
    [InlineData(28, 60, 12, 0, 7, 4, 4, 4)]
    [InlineData(60, 28, 0, 12, 4, 7, 4, 4)]
    [InlineData(92, 60, -12, 0, 4, 4, 7, 4)]
    [InlineData(60, 92, 0, -12, 4, 4, 4, 7)]
    public void GuidesEditInsets(float x, float y, float dx, float dy, float left, float top, float right, float bottom)
    {
        using var harness = new FrameHarness(400, 400);
        using var image = Texture();
        var border = new Insets(4);
        var changes = 0;
        void Draw(Gui g)
        {
            if (g.NineSliceEditor(image, ref border, 120, 120, id: "editor"))
            {
                changes++;
            }
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(x, y));
        harness.Input.PressButton();
        harness.Frame(Draw);
        Assert.True(harness.Gui.IsPointerCaptured);
        harness.Input.MoveTo(new Vector2(x + dx, y + dy));
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(new Insets(left, top, right, bottom), border);
        Assert.Equal(1, changes);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(200, 200));
        harness.Frame(Draw);
        Assert.Equal(new Insets(left, top, right, bottom), border);
        Assert.False(harness.Gui.IsPointerCaptured);
    }

    /// <summary>Dragging beyond the source stops at the opposite guide and never changes its inset.</summary>
    [Theory]
    [InlineData(28, 60, 300, 60, 20, 4, 4, 4)]
    [InlineData(60, 28, 60, 300, 4, 20, 4, 4)]
    [InlineData(92, 60, -100, 60, 4, 4, 20, 4)]
    [InlineData(60, 92, 60, -100, 4, 4, 4, 20)]
    public void GuideClampsAtOppositeBorder(float x, float y, float endX, float endY,
        float left, float top, float right, float bottom)
    {
        using var harness = new FrameHarness(400, 400);
        using var image = Texture();
        var border = new Insets(4);
        void Draw(Gui g) => g.NineSliceEditor(image, ref border, 120, 120);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(x, y));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(endX, endY));
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(new Insets(left, top, right, bottom), border);
    }

    /// <summary>Disabled guides do not capture or edit, and separate editors retain separate input identities.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisabledAndMultipleEditors(bool enabled)
    {
        using var harness = new FrameHarness(400, 400);
        using var image = Texture();
        var first = new Insets(4);
        var second = new Insets(4);
        void Draw(Gui g)
        {
            using (g.Node().Direction(Axis.Horizontal).Enter())
            {
                g.NineSliceEditor(image, ref first, 120, 120, enabled: enabled, id: "first");
                g.NineSliceEditor(image, ref second, 120, 120, id: "second");
            }
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(28, 60));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(40, 60));
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(enabled ? new Insets(7, 4, 4, 4) : new Insets(4), first);
        Assert.Equal(new Insets(4), second);
    }

    /// <summary>Numeric commits edit the selected edge and remain bounded by the opposite border.</summary>
    [Theory]
    [InlineData(0, 9, 9, 4, 4, 4)]
    [InlineData(1, 9, 4, 9, 4, 4)]
    [InlineData(2, 9, 4, 4, 9, 4)]
    [InlineData(3, 99, 4, 4, 4, 20)]
    public void NumericFieldsEditEdges(int edge, int typed, float left, float top, float right, float bottom)
    {
        using var harness = new FrameHarness(400, 400);
        using var image = Texture();
        var border = new Insets(4);
        void Draw(Gui g) => g.NineSliceEditor(image, ref border, 256, 120);
        harness.Frame(Draw);
        var field = harness.Gui.RootNode!.Children[0].Children[edge + 1].Children[1];
        harness.Click(Draw, FrameHarness.Center(field));
        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Input.PressKey(KeyboardKey.A);
        harness.Frame(Draw);
        harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        harness.Input.ReleaseKey(KeyboardKey.A);
        harness.Input.TypeText(typed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.Enter);
        harness.Frame(Draw);
        Assert.Equal(new Insets(left, top, right, bottom), border);
    }

    /// <summary>Guide movement is measured in source pixels even when an ancestor scales the editor.</summary>
    [Fact]
    public void TransformedGuideUsesLogicalPixels()
    {
        using var harness = new FrameHarness(500, 700);
        using var image = Texture();
        var border = new Insets(4);
        void Draw(Gui g)
        {
            using (g.Node().Enter())
            {
                g.SetTransform(Matrix3x2.CreateScale(2));
                g.NineSliceEditor(image, ref border, 120, 120);
            }
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(56, 120));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(80, 120));
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(new Insets(7, 4, 4, 4), border);
    }

    /// <summary>External insets are normalized and invalid authoring surface dimensions are rejected.</summary>
    [Fact]
    public void NormalizesExternalBorders()
    {
        using var harness = new FrameHarness();
        using var image = Texture();
        var border = new Insets(-2, 40, 50, float.NaN);
        harness.Frame(g => g.NineSliceEditor(image, ref border));
        Assert.Equal(new Insets(0, 24, 24, 0), border);
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Gui.NineSliceEditor(image, ref border, width: 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Gui.NineSliceEditor(image, ref border, height: 24));
    }

    /// <summary>Previews use the same border-preserving renderer as runtime images, with optional guide overlays.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreviewRendersCornersAndCenter(bool guides)
    {
        using var harness = new FrameHarness(120, 80);
        using var image = Texture();
        harness.Frame(g => g.NineSlicePreview(image, new Insets(4), 100, 60, guides));
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        Assert.Equal(SKColors.Green, pixels.GetPixel(2, 30));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(50, 30));
        if (guides)
        {
            Assert.NotEqual(SKColors.Blue, pixels.GetPixel(4, 30));
        }
        else
        {
            Assert.Equal(SKColors.Blue, pixels.GetPixel(10, 30));
        }
    }

    /// <summary>The gallery authoring panel builds and renders through the full two-pass frame.</summary>
    [Fact]
    public void GalleryRenders()
    {
        using var harness = new FrameHarness(900, 600);
        harness.Frame(Controls01.Program.NineSliceContent);
        using var snapshot = harness.Snapshot();
        Assert.True(harness.Gui.RootNode!.Children.Count > 0);
    }
}
