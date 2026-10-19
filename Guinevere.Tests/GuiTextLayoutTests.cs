using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Rich-text nodes: same-frame reflow, DPI changes, link interaction and drawing commands.</summary>
public class GuiTextLayoutTests
{
    const string Sentence = "The quick brown fox jumps over the lazy dog, again and again.";

    /// <summary>Text layout drawing queues commands only during the render pass.</summary>
    [Fact]
    public void DrawingALayoutDoesNotQueueCommandsDuringBuild()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            using var scope = gui.Node(100, 30).Enter();
            var node = gui.CurrentNode;
            var layout = gui.LayoutText("text", size: 14);
            var before = node.DrawList.Count;

            gui.DrawTextLayout(layout, Vector2.Zero, Color.Red, null, false, node);

            Assert.Equal(before + (gui.Pass == Pass.Pass2Render ? 1 : 0), node.DrawList.Count);
        });
    }

    [Fact]
    public void FillWidthTextReflowsInTheFrameItsWidthChanges()
    {
        using var harness = new FrameHarness();
        var width = 300f;
        RichTextResult result = default;
        void Draw(Gui gui)
        {
            using (gui.Node(width, 400).Enter())
            {
                result = gui.DrawRichText(Sentence, TextLayoutOptions.FillWidth, 14);
            }
        }

        harness.Frame(Draw);
        var wide = result.Layout;
        width = 120f;
        harness.Frame(Draw);

        Assert.True(result.Layout.Lines.Count > wide.Lines.Count);
        Assert.Equal(120f, result.Node.Rect.W, 1);
        Assert.Equal(result.Layout.Height, result.Node.Rect.H, 2);
        Assert.All(result.Layout.Lines, line => Assert.True(line.Width <= 120.01f));
    }

    [Fact]
    public void FillWidthTextInsideAFitRowTakesItsNaturalWidth()
    {
        using var harness = new FrameHarness();
        RichTextResult result = default;
        harness.Frame(gui =>
        {
            using (gui.Node(-1, -1).Direction(Axis.Horizontal).Enter())
            {
                result = gui.DrawRichText("short", TextLayoutOptions.FillWidth, 14);
            }
        });

        Assert.Single(result.Layout.Lines);
        Assert.Equal(result.Layout.Width, result.Node.Rect.W, 1);
    }

    [Fact]
    public void DisplayScaleChangesRelayoutTheText()
    {
        using var harness = new FrameHarness();
        var display = Substitute.For<IDisplayCapability>();
        display.ScaleFactor.Returns(1f);
        harness.Gui.Platform.Register(display);
        LayoutNode? node = null;
        harness.Frame(gui => node = gui.DrawText(Sentence, 14, wrapWidth: 200));
        var height = node!.Rect.H;

        display.ScaleFactor.Returns(2f);
        harness.Frame(gui => node = gui.DrawText(Sentence, 14, wrapWidth: 200));

        Assert.True(node.Rect.H > height * 2f - 0.01f);
    }

    [Fact]
    public void PressingALinkReportsItAndHoverUsesTheHandCursor()
    {
        using var harness = new FrameHarness();
        var text = new RichText().AppendLink("open", "target").Append(" rest");
        RichTextResult result = default;
        void Draw(Gui gui) => result = gui.DrawRichText(text, size: 14);

        harness.Frame(Draw);
        var link = result.Layout.Runs[0];
        harness.Input.MoveTo(result.Node.InnerRect.X + link.X + 2f, result.Node.InnerRect.Y + 4f);
        harness.Frame(Draw);
        Assert.Equal("target", result.Hovered?.Target);
        Assert.Null(result.Clicked);

        harness.Input.PressButton();
        harness.Frame(Draw);
        Assert.Equal("target", result.Clicked?.Target);
    }

    [Fact]
    public void PointerOutsideLinksReportsNothing()
    {
        using var harness = new FrameHarness();
        RichTextResult result = default;
        harness.Input.MoveTo(390, 290);
        harness.Frame(gui => result = gui.DrawRichText(new RichText().AppendLink("open", "target"), size: 14));

        Assert.Null(result.Hovered);
        Assert.Null(result.Clicked);
    }

    [Fact]
    public void DecorationsBackgroundsAndImagesAddDrawCommands()
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        using var image = surface.Snapshot();
        using var harness = new FrameHarness();
        int Commands(RichText text)
        {
            var count = 0;
            harness.Frame(gui => count = gui.DrawRichText(text, size: 14).Node.DrawList.Count);
            return count;
        }

        var plain = Commands("ab");
        var styled = Commands(new RichText("ab").Style(0, 2, new TextSpanStyle
        {
            Decoration = TextDecoration.Underline | TextDecoration.Strikethrough | TextDecoration.Overline,
            Background = Color.Yellow,
        }));
        var withImage = Commands(new RichText().Append("ab").AppendImage(image, 4, 4));

        Assert.Equal(plain + 4, styled);
        Assert.Equal(plain + 1, withImage);
    }

    [Fact]
    public void SpanColorsAndEffectsDrawEveryLayer()
    {
        using var harness = new FrameHarness();
        var effects = new TextEffects { Outline = new TextEffects.TextOutline(Color.Black, 1f) };
        var count = 0;
        harness.Frame(gui => count = gui.DrawRichText(new RichText().Append("a", TextSpanStyle.Colored(Color.Red))
            .Append("b"), size: 14, effects: effects).Node.DrawList.Count);

        Assert.Equal(4, count);
    }

    [Fact]
    public void SelectableTextHighlightsTheSelectedRange()
    {
        using var harness = new FrameHarness();
        LayoutNode? node = null;
        void Draw(Gui gui) => node = gui.DrawText("select me", true, 14, centerInRect: false);

        harness.Frame(Draw);
        var plain = node!.DrawList.Count;
        harness.Input.MoveTo(node.InnerRect.X + 1f, node.InnerRect.Y + 4f);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(node.InnerRect.X + node.InnerRect.W - 1f, node.InnerRect.Y + 4f);
        harness.Frame(Draw);

        Assert.Equal(plain + 1, node.DrawList.Count);
    }
}
