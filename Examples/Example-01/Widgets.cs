using System.Numerics;
using Guinevere;

namespace Example01;

/// <summary>Small palette-driven controls built from plain nodes; Excalibur has its own showcase.</summary>
public partial class Shared
{
    static readonly List<Action> Pending = [];

    /// <summary>
    /// Queues a state change for the start of the next frame. Clicks are seen while rendering, and changing
    /// anything that shapes the tree there would make the two passes disagree.
    /// </summary>
    static void Later(Action change) => Pending.Add(change);

    static void ApplyPending(Gui gui)
    {
        if (gui.Pass != Pass.Pass1Build || Pending.Count == 0)
        {
            return;
        }

        foreach (var change in Pending)
        {
            change();
        }

        Pending.Clear();
    }

    static void Label(Gui gui, ColorPalette p, string text) => gui.DrawText(text, 12, p.TextDim);
    static bool Button(Gui gui, ColorPalette p, string label, bool selected = false, string? id = null)
    {
        using var key = gui.ItemKey(id ?? label);
        ref var hovered = ref gui.GetData(false);
        var fill = gui.AnimateColor(selected ? p.Accent : hovered ? p.SurfaceHover : p.SurfaceRaised, 0.12f);
        var node = gui.Node().Padding(14, 7).ContentAlignY(0.5f).Cursor(PointerCursor.Hand);
        using (node.Enter())
        {
            gui.DrawBackgroundRect(fill, 8);
            gui.DrawText(label, 13, selected ? p.AccentText : p.Text);
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            hovered = node.OnHover();
            return node.OnClick();
        }
    }

    /// <summary>A row of mutually exclusive buttons; <paramref name="set"/> runs at the next frame start.</summary>
    static void Choice(Gui gui, ColorPalette p, string id, string[] options, int value, Action<int> set)
    {
        using (gui.Node().Direction(Axis.Horizontal).Gap(6).Enter())
        {
            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                if (Button(gui, p, options[i], i == value, $"{id}/{i}"))
                {
                    Later(() => set(index));
                }
            }
        }
    }

    static void Stepper(Gui gui, ColorPalette p, string id, string label, int value, int min, int max,
        Action<int> set, int step = 1)
    {
        using (gui.Node().Direction(Axis.Horizontal).Gap(8).ContentAlignY(0.5f).Enter())
        {
            using (gui.Node(width: 74).ContentAlignY(0.5f).Enter())
            {
                Label(gui, p, label);
            }

            if (Button(gui, p, "-", id: $"{id}/dec"))
            {
                Later(() => set(Math.Max(min, value - step)));
            }

            using (gui.Node(width: 28).ContentAlignY(0.5f).Enter())
            {
                gui.DrawText($"{value}", 14, p.Text);
            }

            if (Button(gui, p, "+", id: $"{id}/inc"))
            {
                Later(() => set(Math.Min(max, value + step)));
            }
        }
    }

    static readonly Dictionary<string, float> DragStarts = [];

    /// <summary>
    /// A vertical grip that drags a value sideways. Drag events arrive before the frame is built, so the value can
    /// change directly.
    /// </summary>
    static void ResizeHandle(Gui gui, ColorPalette p, string id, Func<float> get, Action<float> set, float min,
        float max)
    {
        using (gui.Node(16, -1, id).ExpandHeight().Cursor(PointerCursor.ResizeHorizontal).Enter())
        {
            gui.On<DragStartEvent>(_ => DragStarts[id] = get());
            gui.On<DragMoveEvent>(e => set(Math.Clamp(DragStarts.GetValueOrDefault(id) + e.TotalDelta.X, min, max)));

            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            var rect = gui.CurrentNode.Rect;
            var color = gui.CurrentNode.OnHover() ? p.Accent : p.TextDim;
            gui.DrawRect(new Rect(rect.X + 5, rect.Y + rect.H * 0.5f - 24, 6, 48), color, 3);
        }
    }

    static void ThemeSwitch(Gui gui, ColorPalette p, bool compact)
    {
        var dark = ReferenceEquals(p, ColorPalette.Dark);
        var knob = gui.AnimateFloat(dark ? 0f : 1f, 0.2f);
        var node = gui.Node().Height(36).Padding(14, 0).Direction(Axis.Horizontal).Gap(10).ContentAlignY(0.5f)
            .Cursor(PointerCursor.Hand);
        if (compact)
        {
            node.Width(170);
        }
        else
        {
            node.ExpandWidth();
        }

        using (node.Enter())
        {
            gui.DrawText($"{p.Name} theme", 13, p.TextDim);
            gui.Node().Expand();
            using (gui.Node(40, 22).Enter())
            {
                var track = gui.CurrentNode.Rect;
                gui.DrawBackgroundRect(Color.Lerp(p.Border, p.Accent, knob), 11);
                gui.DrawCircle(new Vector2(track.X + 11 + knob * (track.W - 22), track.Y + track.H * 0.5f), 8,
                    p.AccentText);
            }

            if (gui.Pass == Pass.Pass2Render && node.OnClick())
            {
                Later(() => _palette = dark ? ColorPalette.Light : ColorPalette.Dark);
            }
        }
    }
}
