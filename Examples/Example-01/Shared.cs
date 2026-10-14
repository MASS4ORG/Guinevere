using Guinevere;
using GuinevereDemos;

namespace Example01;

/// <summary>
/// The tour shell: a header, a grouped navigation (a sidebar, or a wrapping strip on narrow windows) and a stage
/// that hosts the selected demo. Every demo receives the active <see cref="ColorPalette"/>.
/// </summary>
public partial class Shared(Gui gui)
{
    const float NarrowWidth = 700f;

    sealed record Demo(string Group, string Title, string Summary, Action<Gui, ColorPalette> Draw);

    static readonly Demo[] Demos =
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
        new("Interaction", "Input Devices", "Controller buttons, axes and touch gestures.", InputDevices),
        new("Style", "Stylesheets", "PSS selectors, inheritance, variables and transitions.", Styling),
    ];

    static int _demoIndex;
    static ColorPalette _palette = ColorPalette.Dark;
    static ColorPalette? _appliedPalette;

    /// <summary>Draws one frame of the tour; call it from both passes.</summary>
    public void Draw()
    {
        ApplyPending(gui);
        ApplyPalette();
        gui.On<KeyDownEvent>(e =>
        {
            var step = e.Key switch { KeyboardKey.PageDown => 1, KeyboardKey.PageUp => -1, _ => 0 };
            if (step == 0)
            {
                return;
            }

            _demoIndex = (_demoIndex + step + Demos.Length) % Demos.Length;
            e.PreventDefault();
        });

        var narrow = gui.ScreenRect.W < NarrowWidth;
        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            gui.DrawBackgroundRect(_palette.Background);
            DemoHeader.Header(gui, "Guinevere", _palette.Text);

            using (gui.Node().Expand().Direction(narrow ? Axis.Vertical : Axis.Horizontal).Padding(12).Gap(12).Enter())
            {
                if (narrow)
                {
                    NavigationStrip();
                }
                else
                {
                    Sidebar();
                }

                Stage();
            }
        }
    }

    /// <summary>Registers the stylesheet once, then reloads it whenever the palette changes.</summary>
    void ApplyPalette()
    {
        if (ReferenceEquals(_appliedPalette, _palette))
        {
            return;
        }

        if (_appliedPalette is null)
        {
            gui.AddStyleSheet(Stylesheet);
        }
        else
        {
            Stylesheet.TryReload();
        }

        _appliedPalette = _palette;
    }

    void Sidebar()
    {
        using (gui.Node(190).ExpandHeight().Direction(Axis.Vertical).Padding(10).Gap(2).Enter())
        {
            gui.DrawBackgroundRect(_palette.Surface, 12);
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(2).Enter())
            {
                gui.ScrollY(_palette.Track, _palette.Border);
                string? group = null;
                for (var i = 0; i < Demos.Length; i++)
                {
                    if (Demos[i].Group != group)
                    {
                        group = Demos[i].Group;
                        using (gui.Node().ExpandWidth().Padding(14, 0).Margin(i == 0 ? 2 : 12, 0, 4, 0).Enter())
                        {
                            gui.DrawText(group.ToUpperInvariant(), 11, _palette.TextDim);
                        }
                    }

                    var index = i;
                    if (NavItem(gui, _palette, Demos[i].Title, i == _demoIndex, compact: false))
                    {
                        Later(() => _demoIndex = index);
                    }
                }
            }

            ThemeSwitch(gui, _palette, compact: false);
        }
    }

    void NavigationStrip()
    {
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Padding(8).Gap(6).Enter())
        {
            gui.DrawBackgroundRect(_palette.Surface, 12);
            for (var i = 0; i < Demos.Length; i++)
            {
                var index = i;
                if (NavItem(gui, _palette, Demos[i].Title, i == _demoIndex, compact: true))
                {
                    Later(() => _demoIndex = index);
                }
            }

            ThemeSwitch(gui, _palette, compact: true);
        }
    }

    void Stage()
    {
        var demo = Demos[_demoIndex];
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(20).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(_palette.Surface, 12);
            gui.DrawText(demo.Title, 24, _palette.Text);
            gui.DrawText(demo.Summary, 13, _palette.TextDim);
            using (gui.Node().ExpandWidth().Height(1).Margin(0, 4).Enter())
            {
                gui.DrawBackgroundRect(_palette.Border);
            }

            using (gui.Node().Expand().Enter())
            {
                demo.Draw(gui, _palette);
            }
        }
    }

    internal static bool NavItem(Gui gui, ColorPalette p, string title, bool active, bool compact)
    {
        using var key = gui.ItemKey(title);
        ref var hovered = ref gui.GetData(false);
        var fill = gui.AnimateColor(NavItemFill(p, active, hovered), 0.12f);
        var node = gui.Node().Height(compact ? 30 : 34).Padding(compact ? 12 : 14, 0).ContentAlignY(0.5f)
            .Cursor(PointerCursor.Hand);
        if (!compact)
        {
            node.ExpandWidth();
        }

        using (node.Enter())
        {
            gui.DrawBackgroundRect(fill, 8);
            gui.DrawText(title, 14, active ? p.AccentText : p.Text);
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            gui.RegisterFocusable();
            hovered = node.OnHover();
            return node.OnClick() || gui.HasFocus() && gui.IsAcceptPressed;
        }
    }

    static Color NavItemFill(ColorPalette p, bool active, bool hovered) =>
        active ? p.Accent : hovered ? p.SurfaceHover : new Color(p.SurfaceHover, 0f);
}
