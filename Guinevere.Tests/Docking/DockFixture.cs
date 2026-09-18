using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Docking;

/// <summary>
/// A four-panel dock space filling the canvas, used as the subject of the docking input scripts.
/// Deliberately plain — no chrome around it — so a script's coordinates mean what they say.
/// </summary>
public sealed class DockFixture
{
    /// <summary>Canvas width the scripts are written against.</summary>
    public const int Width = 1400;

    /// <summary>Canvas height the scripts are written against.</summary>
    public const int Height = 900;

    static readonly DockTheme FixtureTheme = new();

    /// <summary>The layout being driven, rearranged as a script drags things around.</summary>
    public DockLayout Layout { get; } = Seed();

    /// <summary>Draws the dock space. Pass to <see cref="InputScriptPlayer.Play"/>.</summary>
    public void Render(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 18, 20, 25));
        gui.DockSpace(Layout, PanelInfo, RenderPanel, FixtureTheme);
    }

    /// <summary>Creates a Gui and input pair wired to a fresh fixture.</summary>
    public static (DockFixture Fixture, Gui Gui, ScriptedInputHandler Input) Create()
    {
        var input = new ScriptedInputHandler();
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(Width, Height);
        return (new DockFixture(), gui, input);
    }

    static DockLayout Seed()
    {
        var layout = new DockLayout();
        layout.DockAtEdge("scene", DockZone.Center);
        layout.DockAtEdge("tree", DockZone.Left, 0.2f);
        layout.DockAtEdge("inspector", DockZone.Right);
        layout.DockAtEdge("output", DockZone.Bottom);
        return layout;
    }

    static DockPanelInfo? PanelInfo(string panelId) =>
        new(char.ToUpperInvariant(panelId[0]) + panelId[1..]);

    static void RenderPanel(string panelId, Gui gui)
    {
        using (gui.Node().Expand().Padding(8).Enter())
            gui.DrawText(panelId, 12, Color.FromArgb(255, 200, 200, 200), centerInRect: false);
    }
}
