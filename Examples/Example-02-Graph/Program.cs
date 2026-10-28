using System.Diagnostics.CodeAnalysis;
using Guinevere;

namespace Example02;

/// <summary>Runs the graph-only playground through the Silk.NET host.</summary>
public static class Program
{
    /// <summary>Opens the playground without an optional styling package.</summary>
    [ExcludeFromCodeCoverage(Justification = "Native window entry; scenarios and input are tested headlessly.")]
    public static void Main()
    {
        var gui = new Gui();
        var playground = new GraphPlayground();
        using var window = new GuiWindow(gui, width: 1100, height: 800, title: "Graph Playground");
        window.RunGui(() => playground.Draw(gui));
    }
}
