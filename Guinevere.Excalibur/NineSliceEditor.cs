using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class SliceGuideState
    {
        internal bool Active;
        internal float Initial;
    }

    /// <summary>Edits caller-owned source-pixel insets with four draggable guides and numeric fields.</summary>
    /// <remarks>The caller owns the image and persists the insets; edits are clamped to its dimensions.</remarks>
    public static bool NineSliceEditor(this Gui gui, SKImage image, ref Insets border,
        float width = 256, float height = 256, bool enabled = true, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 24);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 24);
        ExcaliburStyles.Ensure(gui);
        var before = border;
        border = new NineSliceLayout(image.Width, image.Height, default, border).Border;
        using (gui.Node(width, id: id ?? gui.AutomaticId(filePath, lineNumber)).Gap(8).Enter())
        {
            SourceEditor(gui, image, ref border, width, height, enabled);
            SliceField(gui, "Left", ref border, 0, image.Width - border.Right, enabled);
            SliceField(gui, "Top", ref border, 1, image.Height - border.Bottom, enabled);
            SliceField(gui, "Right", ref border, 2, image.Width - border.Left, enabled);
            SliceField(gui, "Bottom", ref border, 3, image.Height - border.Top, enabled);
        }
        return border != before;
    }

    /// <summary>Draws a scaled nine-slice preview with optional guides showing the preserved corners.</summary>
    public static LayoutNode NineSlicePreview(this Gui gui, SKImage image, Insets border,
        float width = 256, float height = 128, bool guides = true, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(image);
        ExcaliburStyles.Ensure(gui);
        var node = gui.Node(width, height, id ?? gui.AutomaticId(filePath, lineNumber));
        using (node.Enter())
        {
            SliceChecker(gui, node.Rect);
            gui.DrawImageNineSlice(image, node.Rect, border);
            if (guides)
            {
                SliceGuides(gui, node.Rect,
                    new NineSliceLayout(image.Width, image.Height, node.Rect, border).DestinationCenter);
            }
        }
        return node;
    }

    static void SourceEditor(Gui gui, SKImage image, ref Insets border, float width, float height, bool enabled)
    {
        var scale = Math.Min((width - 24) / image.Width, (height - 24) / image.Height);
        var source = new Rect((width - image.Width * scale) / 2, (height - image.Height * scale) / 2,
            image.Width * scale, image.Height * scale);
        using (gui.Node(width, height, "source").Enter())
        {
            for (var edge = 0; edge < 4; edge++)
            {
                using var key = gui.ItemKey(edge);
                SliceHandle(gui, source, scale, image, ref border, edge, enabled);
            }
            var offset = gui.CurrentNode.Rect;
            var target = new Rect(source.X + offset.X, source.Y + offset.Y, source.W, source.H);
            SliceChecker(gui, target);
            gui.DrawImage(image, target);
            var center = new NineSliceLayout(image.Width, image.Height, default, border).SourceCenter;
            SliceGuides(gui, target, new Rect(target.X + center.X * scale, target.Y + center.Y * scale,
                center.W * scale, center.H * scale));
        }
    }

    static void SliceHandle(Gui gui, Rect source, float scale, SKImage image, ref Insets border,
        int edge, bool enabled)
    {
        var horizontal = edge % 2 == 0;
        var handle = SliceHandleRect(source, scale, border, edge);
        using (gui.Node(handle.W, handle.H, "guide").Absolute(handle.X, handle.Y)
                   .Cursor(horizontal ? PointerCursor.ResizeHorizontal : PointerCursor.ResizeVertical).Enter())
        {
            var result = gui.Draggable(new ControlBehaviorOptions(Enabled: enabled, Focusable: false));
            var state = gui.ControlState(gui.CurrentNode.Id, () => new SliceGuideState());
            if (gui.Pass != Pass.Pass1Build)
            {
                return;
            }

            if (enabled && result.Drag is { } drag)
            {
                MoveSliceGuide(gui.CurrentNode, state, drag, image, ref border, edge, scale);
            }
            else
            {
                state.Active = false;
            }
        }
    }

    static Rect SliceHandleRect(Rect source, float scale, Insets border, int edge)
    {
        var coordinate = edge switch
        {
            0 => source.X + border.Left * scale,
            1 => source.Y + border.Top * scale,
            2 => source.X + source.W - border.Right * scale,
            _ => source.Y + source.H - border.Bottom * scale,
        };
        return edge % 2 == 0
            ? new Rect(coordinate - 4, source.Y, 8, source.H)
            : new Rect(source.X, coordinate - 4, source.W, 8);
    }

    static void MoveSliceGuide(LayoutNode node, SliceGuideState state, DragArgs drag, SKImage image,
        ref Insets border, int edge, float scale)
    {
        if (!state.Active)
        {
            state.Initial = SliceEdge(border, edge);
        }

        state.Active = true;
        var delta = node.ScreenToLocal(drag.CurrentPosition) - node.ScreenToLocal(drag.Origin);
        var change = (edge % 2 == 0 ? delta.X : delta.Y) / scale;
        if (edge >= 2)
        {
            change = -change;
        }

        var maximum = edge switch
        {
            0 => image.Width - border.Right,
            1 => image.Height - border.Bottom,
            2 => image.Width - border.Left,
            _ => image.Height - border.Top,
        };
        border = SetSliceEdge(border, edge, Math.Clamp(state.Initial + change, 0, maximum));
    }

    static void SliceField(Gui gui, string label, ref Insets border, int edge, float maximum, bool enabled)
    {
        using var key = gui.ItemKey(edge);
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
        {
            gui.DrawText(label, color: ExcaliburStyles.TokenColor(gui, "text")).Width(64);
            double value = SliceEdge(border, edge);
            if (gui.NumberField(ref value, min: 0, max: maximum, width: 120, enabled: enabled))
            {
                border = SetSliceEdge(border, edge, (float)value);
            }
        }
    }

    static float SliceEdge(Insets border, int edge) => edge switch
    {
        0 => border.Left,
        1 => border.Top,
        2 => border.Right,
        _ => border.Bottom,
    };

    static Insets SetSliceEdge(Insets border, int edge, float value) => edge switch
    {
        0 => border with { Left = value },
        1 => border with { Top = value },
        2 => border with { Right = value },
        _ => border with { Bottom = value },
    };

    static void SliceGuides(Gui gui, Rect outer, Rect center)
    {
        var color = ExcaliburStyles.TokenColor(gui, "accent");
        gui.DrawLine(new Vector2(center.X, outer.Y), new Vector2(center.X, outer.Y + outer.H), color);
        gui.DrawLine(new Vector2(center.X + center.W, outer.Y),
            new Vector2(center.X + center.W, outer.Y + outer.H), color);
        gui.DrawLine(new Vector2(outer.X, center.Y), new Vector2(outer.X + outer.W, center.Y), color);
        gui.DrawLine(new Vector2(outer.X, center.Y + center.H),
            new Vector2(outer.X + outer.W, center.Y + center.H), color);
        gui.DrawRectBorder(outer, color);
    }

    static void SliceChecker(Gui gui, Rect rect)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        var light = ExcaliburStyles.TokenColor(gui, "surface");
        var dark = ExcaliburStyles.TokenColor(gui, "surface-hover");
        const float cell = 8;
        for (var y = 0; y < rect.H / cell; y++)
        {
            for (var x = 0; x < rect.W / cell; x++)
            {
                gui.DrawRectFilled(new Rect(rect.X + x * cell, rect.Y + y * cell,
                    Math.Min(cell, rect.W - x * cell), Math.Min(cell, rect.H - y * cell)),
                    (x + y) % 2 == 0 ? light : dark);
            }
        }
    }
}
