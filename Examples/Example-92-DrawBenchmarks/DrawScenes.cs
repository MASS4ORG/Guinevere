using System.Numerics;
using System.Reflection;

namespace Guinevere;

/// <summary>Representative controls, text, clipping, effects and the dashboard sample's drawing methods.</summary>
static class DrawScenes
{
    /// <summary>Builds ordinary buttons and independent sliders.</summary>
    public static void Controls(Gui gui)
    {
        for (var i = 0; i < 20; i++)
        {
            using var row = gui.Node(1000, 32).Direction(Axis.Horizontal).Enter();
            gui.Button("Action");
            var value = .5f;
            gui.Slider(ref value, 0, 1);
        }
    }

    /// <summary>Builds a list whose drawing cost is dominated by text.</summary>
    public static void TextHeavy(Gui gui)
    {
        for (var i = 0; i < 30; i++) gui.DrawText("Text-heavy drawing: measured glyphs, layout and dispatch.");
    }

    /// <summary>Draws nested clipped primitive panels.</summary>
    public static void Clipped(Gui gui)
    {
        for (var i = 0; i < 20; i++)
        {
            using var row = gui.Node(600, 30).Enter();
            gui.ClipContent();
            gui.DrawBackgroundRect(Color.Blue, 4);
            gui.DrawRect(new Rect(gui.CurrentNode.Rect.X + 10, gui.CurrentNode.Rect.Y + 5, 900, 15), Color.Red);
            gui.DrawCircle(gui.CurrentNode.Rect.Center, 30, Color.White);
        }
    }

    /// <summary>Builds layered shadows using mutable fluent shapes.</summary>
    public static void Shadows(Gui gui)
    {
        for (var i = 0; i < 10; i++)
        {
            var rect = new Rect(20 + i * 90, 30, 70, 40);
            gui.DrawRect(rect, 6).SolidColor(Color.Blue).OuterShadow(Color.Black, new Vector2(2, 3), 4, 1)
                .InnerShadow(Color.White, new Vector2(0, 1), 2);
        }
    }

    /// <summary>Invokes the actual dashboard sample without creating a window or graphics context.</summary>
    public static Action<Gui> Dashboard(Gui gui)
    {
        var type = typeof(Example_75_PaperUI_Dashboard.Program);
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        type.GetField("_gui", flags)!.SetValue(null, gui);
        var initialize = type.GetMethod("InitializeTheme", flags)!.CreateDelegate<Action>();
        var top = type.GetMethod("RenderTopNavBar", flags)!.CreateDelegate<Action>();
        var sidebar = type.GetMethod("RenderSidebar", flags)!.CreateDelegate<Action>();
        var content = type.GetMethod("RenderMainContent", flags)!.CreateDelegate<Action>();
        var footer = type.GetMethod("RenderFooter", flags)!.CreateDelegate<Action>();
        initialize();
        return current =>
        {
            current.DrawBackgroundRect(Color.FromArgb(18, 18, 23));
            using var panel = current.Node().Expand().Margin(15).Gap(10).Enter();
            top();
            using (current.Node().Expand().Direction(Axis.Horizontal).Gap(15).Enter())
            {
                sidebar();
                content();
            }
            footer();
        };
    }
}
