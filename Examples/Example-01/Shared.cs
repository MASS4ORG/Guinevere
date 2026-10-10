using Guinevere;
using GuinevereDemos;

namespace Example_01;

/// <summary>
/// The tour shell: a header, a grouped navigation (a sidebar, or a wrapping strip on narrow windows) and a stage
/// that hosts the selected demo. Every demo receives the active <see cref="ColorPalette"/>.
/// </summary>
public partial class Shared(Gui gui)
{
    const float NarrowWidth = 700f;

    sealed record Demo(string Group, string Title, string Summary, Action<Gui, ColorPalette> Draw);

    static readonly Demo[] demos =
    [
        new("Layout", "Alignment", "AlignContent, Direction and Gap: pick a cell, then reshape the container.",
            Alignment),
        new("Layout", "Sizing", "Drag the right edge: percentages, Expand, MinWidth, MaxWidth and Wrap react.", Sizing),
        new("Layout", "Responsive", "Mobile, tablet and desktop arrangements chosen from the width available.",
            Responsive),
        new("Text", "Typography", "Sizes, colors, Unicode and font fallback in one live specimen.", Typography),
        new("Text", "Wrapping", "Drag the handle to change wrapWidth and watch lines reflow.", Wrapping),
        new("Text", "Effects", "TextEffects: outline, shadows and gradients, with live edits.", Effects),
        new("Text", "Scrolling", "ScrollY over long and mixed-size content.", Scrolling),
        new("Motion", "Easing & Blend", "Composable layout units and easing curves racing on a loop.", Easings),
        new("Motion", "Springs", "Keyed springs and tweens, plus transform cards that grow, tilt and spin.", Motion),
        new("Interaction", "Drag & Drop", "Typed payloads, previews, accepted and rejected targets.", DragAndDrop),
        new("Interaction", "Events & Cursors", "Bubbling events, cursor shapes and pointer modes.", EventsAndCursors),
        new("Interaction", "Headless Controls", "Your own skin on Pressable and Toggleable behaviors.", HeadlessControls),
        new("Style", "Stylesheets", "PSS selectors, inheritance, variables and transitions.", Styling),
    ];

    static int demoIndex;
    static ColorPalette palette = ColorPalette.Dark;
    static ColorPalette? appliedPalette;

    /// <summary>Draws one frame of the tour; call it from both passes.</summary>
    public void Draw()
    {
        ApplyPending(gui);
        ApplyPalette();
        gui.On<KeyDownEvent>(e =>
        {
            var step = e.Key switch { KeyboardKey.PageDown => 1, KeyboardKey.PageUp => -1, _ => 0 };
            if (step == 0) return;
            demoIndex = (demoIndex + step + demos.Length) % demos.Length;
            e.PreventDefault();
        });

        var narrow = gui.ScreenRect.W < NarrowWidth;
        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            gui.DrawBackgroundRect(palette.Background);
            DemoHeader.Header(gui, "Guinevere", palette.Text);

            using (gui.Node().Expand().Direction(narrow ? Axis.Vertical : Axis.Horizontal).Padding(12).Gap(12).Enter())
            {
                if (narrow) NavigationStrip();
                else Sidebar();
                Stage();
            }
        }
    }

    /// <summary>Registers the stylesheet once, then reloads it whenever the palette changes.</summary>
    void ApplyPalette()
    {
        if (ReferenceEquals(appliedPalette, palette)) return;
        if (appliedPalette is null) gui.AddStyleSheet(stylesheet);
        else stylesheet.TryReload();
        appliedPalette = palette;
    }

    void Sidebar()
    {
        using (gui.Node(190).ExpandHeight().Direction(Axis.Vertical).Padding(10).Gap(2).Enter())
        {
            gui.DrawBackgroundRect(palette.Surface, 12);
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(2).Enter())
            {
                gui.ScrollY(palette.Track, palette.Border);
                string? group = null;
                for (var i = 0; i < demos.Length; i++)
                {
                    if (demos[i].Group != group)
                    {
                        group = demos[i].Group;
                        using (gui.Node().ExpandWidth().Padding(14, 0).Margin(i == 0 ? 2 : 12, 0, 4, 0).Enter())
                            gui.DrawText(group.ToUpperInvariant(), 11, palette.TextDim);
                    }

                    var index = i;
                    if (NavItem(gui, palette, demos[i].Title, i == demoIndex, compact: false))
                        Later(() => demoIndex = index);
                }
            }

            ThemeSwitch(gui, palette, compact: false);
        }
    }

    void NavigationStrip()
    {
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Padding(8).Gap(6).Enter())
        {
            gui.DrawBackgroundRect(palette.Surface, 12);
            for (var i = 0; i < demos.Length; i++)
            {
                var index = i;
                if (NavItem(gui, palette, demos[i].Title, i == demoIndex, compact: true))
                    Later(() => demoIndex = index);
            }

            ThemeSwitch(gui, palette, compact: true);
        }
    }

    void Stage()
    {
        var demo = demos[demoIndex];
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(20).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(palette.Surface, 12);
            gui.DrawText(demo.Title, 24, palette.Text);
            gui.DrawText(demo.Summary, 13, palette.TextDim);
            using (gui.Node().ExpandWidth().Height(1).Margin(0, 4).Enter())
                gui.DrawBackgroundRect(palette.Border);

            using (gui.Node().Expand().Enter())
                demo.Draw(gui, palette);
        }
    }

    static bool NavItem(Gui gui, ColorPalette p, string title, bool active, bool compact)
    {
        using var key = gui.ItemKey(title);
        ref var hovered = ref gui.GetData(false);
        var fill = gui.AnimateColor(active ? p.Accent : hovered ? p.SurfaceHover : new Color(p.SurfaceHover, 0f), 0.12f);
        var node = gui.Node().Height(compact ? 30 : 34).Padding(compact ? 12 : 14, 0).ContentAlignY(0.5f)
            .Cursor(PointerCursor.Hand);
        if (!compact) node.ExpandWidth();
        using (node.Enter())
        {
            gui.DrawBackgroundRect(fill, 8);
            gui.DrawText(title, 14, active ? p.AccentText : p.Text);
            if (gui.Pass != Pass.Pass2Render) return false;
            hovered = node.OnHover();
            return node.OnClick();
        }
    }
}
