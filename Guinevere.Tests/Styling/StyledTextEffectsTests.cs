using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Stylesheet text shadows resolve into scoped text effects.</summary>
public class StyledTextEffectsTests
{
    static ResolvedStyle Style(string declarations) =>
        ResolveStyle(declarations);

    static ResolvedStyle ResolveStyle(string declarations)
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse($"box {{ {declarations} }}"));
        return gui.ResolveStyle("box");
    }

    /// <summary>Text shadows parse offsets, optional blur and color while preserving CSS paint order.</summary>
    [Fact]
    public void GetTextShadowsParsesLayersAndDefaults()
    {
        var defaultColor = Color.FromArgb(255, 12, 34, 56);
        var shadows = Style("color = #0c2238; text-shadow = 1px 2px, -3 4 6px #ff000080")
            .GetTextShadows();

        Assert.Equal(
        [
            new TextEffects.TextShadow(defaultColor, new Vector2(1, 2), 0),
            new TextEffects.TextShadow(Color.FromArgb(128, 255, 0, 0), new Vector2(-3, 4), 3),
        ], shadows);
        Assert.Empty(Style("text-shadow = none").GetTextShadows()!);
        Assert.Null(Style("color = #0c2238").GetTextShadows());
    }

    /// <summary>Malformed text-shadow declarations are ignored as a whole.</summary>
    [Theory]
    [InlineData("1px")]
    [InlineData("1 2 3 4")]
    [InlineData("1 2, 3px")]
    [InlineData(", 1 2")]
    [InlineData("1 2, , 3 4")]
    [InlineData("1 2 -3px")]
    [InlineData("1 2 nonsense")]
    public void GetTextShadowsRejectsMalformedDeclarations(string value) =>
        Assert.Null(Style($"text-shadow = {value}").GetTextShadows());

    /// <summary>Parsed shadow lists are reused until the effective default color changes.</summary>
    [Fact]
    public void GetTextShadowsCachesByEffectiveDefaultColor()
    {
        var style = Style("text-shadow = 1 2;");
        var first = style.GetTextShadows(Color.Red);

        Assert.Same(first, style.GetTextShadows(Color.Red));
        var changed = style.GetTextShadows(Color.Blue);
        Assert.NotSame(first, changed);
        Assert.Equal(Color.Blue, Assert.Single(changed!).Color);

        var explicitColor = Style("color = #00ff00; text-shadow = 1 2;");
        var resolved = explicitColor.GetTextShadows(Color.Red);
        Assert.Same(resolved, explicitColor.GetTextShadows(Color.Blue));
        Assert.Equal(Color.FromArgb(255, 0, 255, 0), Assert.Single(resolved!).Color);
    }

    /// <summary>Inherited effects flow into children and a local declaration replaces them.</summary>
    [Fact]
    public void StyledScopesInheritAndReplaceTextShadows()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            parent { color = #101010; text-shadow = 1 2 4px #ff0000; }
            child { text-shadow = 3 4 2px #0000ff; }
            none { text-shadow = none; }
            """));

        harness.Frame(gui =>
        {
            using (gui.StyledNode("parent").Enter())
            {
                var inherited = gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows;
                Assert.Equal(new Vector2(1, 2), Assert.Single(inherited!).Offset);
                using (gui.StyledNode("plain").Enter())
                {
                    Assert.Same(inherited, gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows);
                }

                using (gui.StyledNode("child").Enter())
                {
                    var replaced = gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows;
                    Assert.Equal(new Vector2(3, 4), Assert.Single(replaced!).Offset);
                }
                using (gui.StyledNode("none").Enter())
                {
                    Assert.Empty(gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows!);
                }
            }
        });
    }

    /// <summary>Token changes and sheet removal update resolved text effects in subsequent frames.</summary>
    [Fact]
    public void StyledScopesRefreshAfterTokenAndSheetChanges()
    {
        using var harness = new FrameHarness();
        var sheet = StyleSheet.Parse("$shadow = #ff0000; box { text-shadow = 1 2 3px $shadow; }");
        harness.Gui.StyleSheets.Add(sheet);
        static void Draw(Gui gui)
        {
            using (gui.StyledNode("box").Enter())
            {
                Assert.Single(gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows!);
            }
        }

        harness.Frame(Draw);
        harness.Gui.StyleSheets.SetToken("shadow", "#00ff00");
        harness.Frame(gui =>
        {
            using (gui.StyledNode("box").Enter())
            {
                Assert.Equal(Color.FromArgb(255, 0, 255, 0), Assert.Single(gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>()
                    .Value.DropShadows!).Color);
            }
        });
        harness.Gui.StyleSheets.Remove(sheet);
        harness.Frame(gui =>
        {
            using (gui.StyledNode("box").Enter())
            {
                Assert.Null(gui.CurrentNodeScope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows);
            }
        });
    }

    /// <summary>A real label control draws stylesheet shadows with its text.</summary>
    [Fact]
    public void LabelDrawsItsStyledTextShadow()
    {
        using var harness = new FrameHarness(240, 80);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse(
            ".shadow { text-shadow = 18 0 0 #ff0000, 18 0 0 #0000ff; }"));
        harness.Frame(gui => gui.Label("Styled", fontSize: 28, classes: ["shadow"]));

        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        var redPixels = 0;
        var bluePixels = 0;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                var color = pixels.GetPixel(x, y);
                if (color.Red > 150 && color.Green < 80 && color.Blue < 80)
                {
                    redPixels++;
                }

                if (color.Blue > 150 && color.Red < 80 && color.Green < 80)
                {
                    bluePixels++;
                }
            }
        }

        Assert.True(redPixels > 10, $"expected visible red shadow pixels, got {redPixels}");
        Assert.Equal(0, bluePixels);
    }

    /// <summary>Changing a host token updates the color of rendered text shadows on the next frame.</summary>
    [Fact]
    public void TokenChangesRefreshRenderedTextShadowPixels()
    {
        using var harness = new FrameHarness(200, 80);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            $shade = #ff0000;
            box { width = expand; height = expand; color = #000000; text-shadow = 16 0 $shade; }
            """));
        static void Draw(Gui gui)
        {
            using (gui.StyledNode("box").Enter())
            {
                gui.DrawText("A", size: 28);
            }
        }

        harness.Frame(Draw);
        using var redImage = harness.Snapshot();
        using var redPixels = SKBitmap.FromImage(redImage);
        Assert.True(CountColor(redPixels, red: true) > 10);

        harness.Gui.StyleSheets.SetToken("shade", "#0000ff");
        harness.Frame(Draw);
        using var blueImage = harness.Snapshot();
        using var bluePixels = SKBitmap.FromImage(blueImage);
        Assert.True(CountColor(bluePixels, red: false) > 10);
        Assert.Equal(0, CountColor(bluePixels, red: true));
    }

    /// <summary>Styled opacity composites the text and its shadow together.</summary>
    [Fact]
    public void OpacityCompositesTextShadowAsOneGroup()
    {
        using var harness = new FrameHarness(160, 60);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            box { width = expand; height = expand; opacity = 0.5; text-shadow = 16 0 0 #ff0000; }
            """));
        harness.Frame(gui =>
        {
            using (gui.StyledNode("box").Enter())
            {
                gui.DrawText("A", size: 28);
            }
        });

        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.InRange(MaxRedAlpha(pixels), 120, 136);
    }

    /// <summary>An ancestor clip bounds the ink from a text shadow that extends past its edge.</summary>
    [Fact]
    public void AncestorClipClipsTextShadowInk()
    {
        using var harness = new FrameHarness(120, 80);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            host { width = 60; height = 60; text-shadow = 20 0 #ff0000; }
            """));
        harness.Frame(gui =>
        {
            using (gui.StyledNode("host").Enter())
            {
                gui.ClipContent();
                using (gui.Node(30, 40).Absolute(30, 0).Enter())
                {
                    gui.DrawText("A", size: 24);
                }
            }
        });

        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.True(CountColorInXRange(pixels, 50, 60, red: true) > 0,
            "expected the shadow to remain visible up to the ancestor clip");
        Assert.Equal(0, CountColorInXRange(pixels, 60, pixels.Width, red: true));
    }

    static int CountColor(SKBitmap pixels, bool red)
        => CountColorInXRange(pixels, 0, pixels.Width, red);

    static int CountColorInXRange(SKBitmap pixels, int xStart, int xEnd, bool red)
    {
        var count = 0;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = xStart; x < xEnd; x++)
            {
                var color = pixels.GetPixel(x, y);
                if (red ? color.Red > 150 && color.Green < 80 && color.Blue < 80
                        : color.Blue > 150 && color.Red < 80 && color.Green < 80)
                {
                    count++;
                }
            }
        }

        return count;
    }

    static int MaxRedAlpha(SKBitmap pixels)
    {
        var max = 0;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                var color = pixels.GetPixel(x, y);
                if (color.Red > 80 && color.Green < 80 && color.Blue < 80)
                {
                    max = Math.Max(max, color.Alpha);
                }
            }
        }

        return max;
    }

    /// <summary>A hover rule can clear a shadow applied by the base rule.</summary>
    [Fact]
    public void StyledScopesHoverNoneClearsTheBaseShadow()
    {
        using var harness = new FrameHarness(160, 80);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            .label { width = 120; height = 60; text-shadow = 2 3 #ff0000; }
            .label:hover { text-shadow = none; }
            """));
        harness.Input.MoveTo(new Vector2(80, 40));

        harness.Frame(gui =>
        {
            using (gui.StyledNode("label", ["label"]).Enter())
            {
                gui.DrawText("Hover", size: 20);
            }
        });

        var label = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Empty(label.Scope.Get<LayoutNodeScopeTextEffects>().Value.DropShadows!);
    }
}
