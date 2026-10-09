using Autoformers;
using Guinevere;

namespace Example_76_Odin_Attributes;

/// <summary>
/// The tour's window content: a grouped sidebar of pages and, for the selected page, its source beside the live
/// form. Each page keeps its own demo object and fold state for the whole session.
/// </summary>
public sealed class AttributeTour
{
    /// <summary>Window width from which the code sits beside the result instead of below it.</summary>
    const float SideBySideWidth = 1400;

    /// <summary>Width the live form keeps when the code sits beside it.</summary>
    const float FormWidth = 420;

    static readonly string[] MonospaceFamilies =
        ["JetBrains Mono", "Cascadia Mono", "Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono"];

    static readonly Font CodeFont = Monospace();

    readonly Dictionary<DemoPage, (object Target, FormRenderContext Context, string Source)> _live = [];

    /// <summary>The index of the page on screen.</summary>
    public int Selected { get; set; }

    /// <summary>A color token of the active theme, for the tour's own drawing.</summary>
    static Color Token(Gui gui, string name) => ExcaliburStyles.TokenColor(gui, name);

    /// <summary>Draws the header, the sidebar and the selected page.</summary>
    public void Draw(Gui gui)
    {
        gui.SetTextColor(Token(gui, "text"));
        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            gui.DrawBackgroundRect(Token(gui, "base-background"));
            Header(gui);

            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(12).Padding(12).Enter())
            {
                Sidebar(gui);
                Page(gui, DemoPages.All[Math.Clamp(Selected, 0, DemoPages.All.Count - 1)]);
            }
        }
    }

    static void Header(Gui gui)
    {
        using (gui.Node().Height(48).Padding(16, 0).Direction(Axis.Horizontal).ContentAlignY(0.5f).Gap(10).Enter())
        {
            gui.DrawText("Autoformers", 20, Token(gui, "text"));
        }
    }

    void Sidebar(Gui gui)
    {
        using (gui.Node(230).ExpandHeight().Padding(10).Direction(Axis.Vertical).Gap(2).Enter())
        {
            gui.DrawBackgroundRect(Token(gui, "surface"), 8);
            gui.ScrollContainer(scrollY: true);

            string? group = null;
            for (var i = 0; i < DemoPages.All.Count; i++)
            {
                var page = DemoPages.All[i];
                if (!page.IsAvailable) continue;

                if (page.Group != group)
                {
                    group = page.Group;
                    gui.DrawText(group.ToUpperInvariant(), 11, Token(gui, "text-dim")).Margin(4, 10, 0, 4);
                }

                SidebarEntry(gui, page, i);
            }
        }
    }

    void SidebarEntry(Gui gui, DemoPage page, int index)
    {
        using (gui.Node(-1, 26, $"nav/{index}").ExpandWidth().Padding(10, 0).ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();
            var selected = index == Selected;
            if (gui.Pass == Pass.Pass2Render && (selected || interactable.OnHover()))
                gui.DrawBackgroundRect(selected ? Token(gui, "selected") : Token(gui, "surface-hover"), 4);

            var color = selected ? Token(gui, "text-on-accent")
                : page.IsAvailable ? Token(gui, "text") : Token(gui, "text-disabled");
            gui.DrawText(page.Title, 13, color, centerInRect: false);

            if (gui.Pass == Pass.Pass2Render && interactable.OnClick()) Selected = index;
        }
    }

    void Page(Gui gui, DemoPage page)
    {
        using (gui.Node().Expand().Padding(16).Direction(Axis.Vertical).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(Token(gui, "surface"), 8);
            gui.ScrollContainer(scrollY: true);

            gui.DrawText(page.Title, 24, Token(gui, "text"), centerInRect: false);
            gui.DrawText(page.Summary, 14, Token(gui, "text-dim"), centerInRect: false);

            if (!page.IsAvailable)
            {
                gui.DrawText("Coming soon.", 14, Token(gui, "warning"), centerInRect: false);
                return;
            }

            // Wide windows put the code beside the result; narrow ones move it below so lines never wrap.
            var (target, context, source) = Live(page);
            var sideBySide = gui.ScreenRect.W >= SideBySideWidth;
            using (gui.Node().Direction(sideBySide ? Axis.Horizontal : Axis.Vertical).Gap(16).Enter())
            {
                Panel(gui, "Result", sideBySide ? UnitValue.Pixels(FormWidth) : UnitValue.Expand(), "page/result", false,
                    () => gui.Form(new FormModel(target, [FormBuilder.Section(target, page.Title)]), "page/form",
                        context));
                Panel(gui, "Code", UnitValue.Expand(), "page/code", true,
                    () => gui.DrawText(source, 12, Token(gui, "text"), CodeFont, centerInRect: false, clip: true));
            }
        }
    }

    /// <summary>Draws a titled panel around a body that holds one of the page's two halves.</summary>
    /// <param name="gui">The GUI being drawn.</param>
    /// <param name="title">The header caption.</param>
    /// <param name="width">How the panel claims width from its parent.</param>
    /// <param name="id">The panel's node id.</param>
    /// <param name="pan">Whether the body pans sideways to reach content wider than the panel.</param>
    /// <param name="body">Draws the panel's content inside the padded body.</param>
    static void Panel(Gui gui, string title, UnitValue width, string id, bool pan, Action body)
    {
        using (gui.Node(-1, -1, id).Width(width).Direction(Axis.Vertical).Enter())
        {
            gui.DrawBackgroundRect(Token(gui, "base-background"), 6);

            using (gui.Node(-1, height: 25).AlignContent(.5f).ExpandWidth().Enter())
            {
                gui.DrawBackgroundRect(Token(gui, "text-dim"), 6, Corner.Top);
                gui.DrawText(title.ToUpperInvariant(), 11, Token(gui, "base-background"));
            }

            using (gui.Node().Expand().Margin(8).Padding(12).Enter())
            {
                if (pan) gui.ScrollContainer(scrollX: true, scrollY: false);
                body();
            }
        }
    }

    /// <summary>
    /// The first installed coding font. Skia resolves an unknown family to a proportional fallback, so each
    /// candidate is accepted only when the system really has it.
    /// </summary>
    static Font Monospace()
    {
        foreach (var family in MonospaceFamilies)
        {
            using var typeface = SkiaSharp.SKTypeface.FromFamilyName(family);
            if (string.Equals(typeface?.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                return Font.FromFamilyName(family);
        }

        return Font.FromFamilyName("monospace");
    }

    /// <summary>The demo object a page edits; created on first use and kept for the session.</summary>
    public object TargetOf(DemoPage page) => Live(page).Target;

    (object Target, FormRenderContext Context, string Source) Live(DemoPage page)
    {
        if (_live.TryGetValue(page, out var entry)) return entry;

        entry = (Activator.CreateInstance(page.Demo!)!, new FormRenderContext(), page.Source());
        _live[page] = entry;
        return entry;
    }
}
