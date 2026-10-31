using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Graphs;

public sealed class GraphStyleTests
{
    [Theory]
    [InlineData(0, 0, "#0a1e31")]
    [InlineData(0, 1, "#0a1e31")]
    [InlineData(1, 0, "#faf6ec")]
    [InlineData(1, 1, "#faf6ec")]
    [InlineData(2, 0, "#000000")]
    [InlineData(2, 1, "#000000")]
    public async Task ApplicationSheetsDrawBothScenariosAndCanBeCaptured(int style, int scenario, string background)
    {
        using var harness = new FrameHarness(1280, 900);
        var playground = new GraphPlayground();
        playground.SelectStyle(style);
        playground.SelectScenario(scenario);
        harness.Frame(playground.Draw);
        await playground.Flow.Flow.Completion;
        harness.Frame(playground.Draw);
        harness.Frame(playground.Draw);
        Assert.Contains(GraphDemoStyles.Sheets[style], harness.Gui.StyleSheets);
        Assert.Equal(Color.ParseHex(background), harness.Gui.ResolveStyle("graph-canvas").GetColor("background-color"));
        var scene = scenario == 0 ? playground.Sandbox : playground.Flow.Sandbox;
        Assert.True(scene.Canvas.VisibleCount > 0);
        var at = scene.Canvas.WorldToScreen(new GraphPoint(500, 370));
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        Assert.Equal(SKColor.Parse(background), pixels.GetPixel((int)at.X, (int)at.Y));
        if (scenario == 1)
        {
            Assert.NotEmpty(playground.Flow.Flow.Values);
        }
        if (Environment.GetEnvironmentVariable("MASS4_GRAPH_CAPTURE_DIR") is { } directory)
        {
            Directory.CreateDirectory(directory);
            using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(directory, $"graph-{style}-{scenario}.png"));
            png.SaveTo(file);
        }
    }

    [Fact]
    public void ThirdPartySheetRestylesPartsAndDomainClassesWithoutGraphCode()
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.View.StyleClasses = ["plugin"];
        scene.View.NodeClasses = static _ => ["domain"];
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            .plugin graph-node.domain { background-color = #e03080; border-radius = 0; width = 260; }
            .plugin graph-header { background-color = #7030e0; height = 44; }
            .plugin graph-link { stroke = #00ff00; stroke-width = 8; stroke-dasharray = 12; }
            .plugin graph-badge { background-color = #ffff00; color = #000000; font-size = 15; }
            .plugin graph-port { background-color = #ff0000; width = 20; }
            """));
        GraphLinkStyle stroke = default;
        scene.View.LinkRenderer = (gui, link) =>
        {
            stroke = link.Style;
            link.Connector.Draw(gui, link.Style);
        };
        void Draw(Gui gui) => scene.View.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.All(scene.View.Nodes, static node => Assert.Equal(260, node.Bounds.Width));
        Assert.Equal(Color.Lime, stroke.Color);
        Assert.Equal(8, stroke.Thickness);
        Assert.Equal(12, stroke.Dash);
        var point = scene.Canvas.WorldToScreen(new GraphPoint(100, 110));
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColor.Parse("#e03080"), pixels.GetPixel((int)point.X, (int)point.Y));
    }

    [Theory]
    [InlineData(GraphPortShape.Circle)]
    [InlineData(GraphPortShape.Square)]
    [InlineData(GraphPortShape.Diamond)]
    [InlineData(GraphPortShape.Triangle)]
    public void PresenterShapesUsePssGradientBrushes(GraphPortShape shape)
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        scene.View.PortShape = _ => shape;
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            graph-port { width = 30; background = linear-gradient(90deg, #ff0000, #0000ff); border-radius = 0; }
            """));
        void Draw(Gui gui) => scene.View.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        var port = scene.View.Editor.Document.Nodes.First().Ports[0].Id;
        var center = scene.Canvas.WorldToScreen(scene.View.PortPosition(port));
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        var left = pixels.GetPixel((int)center.X - 4, (int)center.Y);
        var right = pixels.GetPixel((int)center.X + 4, (int)center.Y);
        Assert.True(left.Red > left.Blue, $"Left gradient sample was {left}.");
        Assert.True(right.Blue > right.Red, $"Right gradient sample was {right}.");
    }

    [Theory]
    [InlineData("9 3 2", true)]
    [InlineData("0 0", false)]
    [InlineData("bad 3", false)]
    [InlineData("-1 2", false)]
    [InlineData("10% 2", false)]
    public void DashListsResolveThroughTheSamePssStroke(string pattern, bool expected)
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse($"graph-link {{ stroke-dasharray = {pattern}; }}"));
        GraphLinkStyle stroke = default;
        scene.View.LinkRenderer = (gui, visual) =>
        {
            stroke = visual.Style;
            visual.Connector.Draw(gui, visual.Style);
        };
        void Draw(Gui gui) => scene.View.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(expected, stroke.DashPattern is not null);
    }
}
