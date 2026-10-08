using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    const string Style = """
                         // tokens
                         $bg        = #12151d;
                         $card      = #202634;
                         $btn       = #ff0000;
                         $btn-hover = #00ff00;
                         $btn-down  = #0000ff;
                         $border    = #556080;

                         #root {
                             flex-direction = column;
                             gap = 16;
                             padding = 32;
                             background-color = $bg;
                         }

                         .card {
                             flex-direction = column;
                             gap = 12;
                             padding = 18;
                             background-color = $card;
                             border-radius = 10;
                         }
                         .row { flex-direction = row; gap = 12; }

                         .btn {
                             width = 150;
                             height = 46;
                             background-color = $btn;
                             border-radius = 8;
                             border-color = $border;
                             border-width = 1;
                             align-items = center;
                             justify-content = center;
                         }
                         .btn:hover  { background-color = $btn-hover; border-color = $btn-hover; }
                         .btn:active { background-color = $btn-down; }

                         #primary { background-color = $btn-hover; border-width = 0; }

                         .visuals { color = #eceef3; font-size = 14; }
                         .tile {
                             width = 120;
                             height = 64;
                             align-items = center;
                             justify-content = center;
                             background-color = #2c3446;
                             border-radius = 8;
                             cursor = pointer;
                         }
                         .tile:hover   { outline = 2px solid #88c0d0; outline-offset = 2; }
                         #corners      { border-radius = 18 0 18 0; }
                         #gradient     { background = linear-gradient(135deg, #5e81ac, #b48ead); }
                         #shadow       { box-shadow = 0 6px 14px #000000aa, inset 0 1px 0 #ffffff33; }
                         #faded        { opacity = 0.5; }
                         #bold         { font-weight = bold; font-style = italic; color = #ebcb8b; font-size = 16; }
                         """;

    /// <summary>The styling page's sheet, parsed once; its rules' source lines drive the highlight below.</summary>
    static readonly StyleSheet StylingSheet = StyleSheet.Parse(Style);

    // The element hovered in the last render pass, used by both passes of this frame so they agree.
    static StyleTarget? _hovered;
    static StyleTarget? _nextHovered;

    static void StylingContent(Gui gui)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            _hovered = _nextHovered;
            _nextHovered = null;
        }

        using (gui.StyledNode("VisualElement", id: "root").Expand().Enter())
        {
            gui.DrawText("PSS-styled widgets", 24, Color.FromArgb(255, 236, 238, 243));

            using (gui.StyledNode("VisualElement", ["card"]).Enter())
            {
                gui.DrawText(
                    "A .card panel — padding, radius and background come from the stylesheet. Hover a .btn to see the :hover rule.",
                    14, Color.FromArgb(255, 154, 160, 166));

                using (gui.StyledNode("VisualElement", ["row"]).Enter())
                {
                    StyledBtn(gui, "Normal", null);
                    StyledBtn(gui, "Primary", "primary");
                    StyledBtn(gui, "Also .btn", null);
                }

                using (gui.StyledNode("VisualElement", ["card", "visuals"]).Enter())
                {
                    gui.DrawText("Visual properties — text color and size are inherited from .visuals; hover a tile for its outline.");
                    using (gui.StyledNode("VisualElement", ["row"]).Enter())
                    {
                        StyledTile(gui, "corners", "Per-corner radius");
                        StyledTile(gui, "gradient", "Gradient");
                        StyledTile(gui, "shadow", "Shadows");
                        StyledTile(gui, "faded", "Opacity 50%");
                        StyledTile(gui, "bold", "Bold italic");
                    }
                }

                SourceView(gui);
            }
        }
    }

    /// <summary>The sheet source, scrollable, with the lines of the rules the hovered element uses highlighted.</summary>
    static void SourceView(Gui gui)
    {
        var used = UsedLines(gui);
        var caption = _hovered is { } target
            ? $"Rules used by {target.Type}{(target.Id is null ? "" : "#" + target.Id)}.{string.Join('.', target.Classes)}:hover"
            : "Hover a .btn or a tile to highlight the rules it uses.";
        gui.DrawText(caption, 13, Color.FromArgb(255, 154, 160, 166));

        using (gui.Node().ExpandWidth().Height(260).Padding(6).Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(255, 20, 23, 31), 6);
            gui.ScrollContainer(scrollY: true);
            var lines = Style.Split('\n');
            ScrollToFirst(gui, used, lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                using (gui.Node().ExpandWidth().Height(16).Enter())
                {
                    var hit = used.Contains(i + 1);
                    if (hit) gui.DrawBackgroundRect(Color.FromArgb(90, 136, 192, 208), 2);
                    gui.DrawText($"{i + 1,3}  {lines[i]}", 12,
                        hit ? Color.White : Color.FromArgb(255, 154, 160, 166), centerInRect: false);
                }
            }
        }
    }

    static string? _scrolledFor;

    /// <summary>Scrolls the source view to the first highlighted line once per newly hovered element.</summary>
    static void ScrollToFirst(Gui gui, HashSet<int> used, int lineCount)
    {
        var key = _hovered is { } target ? $"{target.Type}#{target.Id}.{string.Join('.', target.Classes)}" : null;
        if (gui.Pass != Pass.Pass2Render || used.Count == 0 || key == _scrolledFor) return;
        _scrolledFor = key;
        gui.SetScrollPercentage(gui.CurrentNode.Id, Axis.Vertical, (used.Min() - 1f) / Math.Max(1, lineCount - 1));
    }

    /// <summary>The source lines of the page-sheet rules that match the hovered element.</summary>
    static HashSet<int> UsedLines(Gui gui)
    {
        if (_hovered is not { } target) return [];
        return
        [
            .. gui.StyleSheets.MatchedRules(target)
                .Where(match => ReferenceEquals(match.Sheet, StylingSheet))
                .SelectMany(match => match.Rule.Lines),
        ];
    }

    static void StyledTile(Gui gui, string id, string label)
    {
        var node = gui.StyledNode("VisualElement", ["tile"], id);
        using (node.Enter()) gui.DrawText(label);
        NoteHover(gui, node, new StyleTarget("VisualElement", id, ["tile"], StyleState.Hover));
    }

    static void StyledBtn(Gui gui, string label, string? id)
    {
        var node = gui.StyledNode("Button", ["btn"], id);
        using (node.Enter())
        {
            _ = gui.GetInteractable().OnClick();
            gui.DrawText(label, 15, Color.FromArgb(255, 236, 238, 243));
        }
        NoteHover(gui, node, new StyleTarget("Button", id, ["btn"], StyleState.Hover));
    }

    static void NoteHover(Gui gui, LayoutNode node, StyleTarget target)
    {
        if (gui.Pass == Pass.Pass2Render && gui.GetInteractable(node).OnHover()) _nextHovered = target;
    }
}
