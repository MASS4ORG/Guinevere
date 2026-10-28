using Guinevere;
using Guinevere.Graphs;

namespace Example05;

/// <summary>A view-only stress sample with 10,000 tiles and no semantic graph document.</summary>
public static class Program
{
    /// <summary>Runs the core-only canvas through the Silk.NET window integration.</summary>
    public static void Main()
    {
        var gui = new Gui();
        var canvas = new GraphCanvas { ShowMinimap = true, Name = "10,000 view tiles" };
        var tiles = Enumerable.Range(0, 10000).Select(static i => new GraphElementView(
            $"tile-{i}", GraphElementRole.Node, new GraphBounds(i % 100 * 220, i / 100 * 130, 160, 80),
            $"Tile {i}")).ToArray();
        using var window = new GuiWindow(gui);
        window.RunGui(() =>
        {
            using var root = gui.Node().Expand().Direction(Axis.Vertical).Enter();
            gui.DrawText("10,000 tiles · middle-drag pan · wheel zoom · arrows navigate · Home fit all",
                16, Color.White);
            canvas.Draw(gui, tiles, static (g, tile) =>
            {
                g.DrawRectFilled(g.CurrentNode.Rect, Color.SlateBlue, 8);
                g.DrawText(tile.Name, 16, Color.White);
            });
        });
    }
}
