using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Draws a draggable divider between two siblings of a flow container and updates
    /// <paramref name="fraction"/> — the share of the container the sibling before it takes — as the
    /// pointer moves. Place it between the two nodes it separates, inside a container whose
    /// <see cref="LayoutNode.Direction"/> matches <paramref name="axis"/>.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="fraction">The split position, 0..1, updated in place while dragging.</param>
    /// <param name="axis">The container's layout direction: horizontal splits side by side.</param>
    /// <param name="thickness">The divider's width across the split, in pixels.</param>
    /// <param name="min">The closest either side may get to collapsing, as a fraction.</param>
    /// <param name="color">The divider color. Defaults to a mid grey.</param>
    /// <param name="hoverColor">The highlight color painted over the whole handle while hovered or
    /// dragged, so the grab zone reads as a handle. Defaults to a lighter grey.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>True if this frame moved the divider.</returns>
    public static bool Splitter(this Gui gui, ref float fraction, Axis axis, float thickness = 6f,
        float min = 0.1f, Color? color = null, Color? hoverColor = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var changed = false;
        var horizontal = axis == Axis.Horizontal;

        var node = horizontal
            ? gui.Node(thickness, filePath: filePath, lineNumber: lineNumber).ExpandHeight()
            : gui.Node(-1, thickness, filePath: filePath, lineNumber: lineNumber).ExpandWidth();

        using (node.Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            var interactable = gui.GetInteractable();
            var dragging = interactable.OnDrag(out var args);
            // Not "grabbable" while something else owns the pointer - a tab being dragged past it.
            var active = dragging || (!gui.IsPointerCaptured && interactable.OnHover());

            gui.DrawRectFilled(gui.CurrentNode.Rect, color ?? Color.FromArgb(255, 51, 56, 66));
            if (active)
                gui.DrawBackgroundRect(hoverColor ?? Color.FromArgb(255, 96, 104, 118));

            // The split position is anchored to where it was when the drag started and then offset by
            // the pointer's total travel, rather than accumulated frame by frame: summing deltas cannot
            // recover from a dropped or duplicated one, and it drifts away from the cursor.
            ref var anchor = ref gui.GetValue(float.NaN, $"{gui.CurrentNode.Id}/splitterAnchor");

            if (!dragging)
            {
                anchor = float.NaN;
                return false;
            }

            var track = gui.CurrentNode.Parent?.InnerRect;
            var span = horizontal ? track?.W ?? 0f : track?.H ?? 0f;
            if (span <= 0f) return false;

            if (float.IsNaN(anchor)) anchor = fraction;

            var travel = horizontal ? args.TotalDelta.X : args.TotalDelta.Y;
            var updated = Math.Clamp(anchor + travel / span, min, 1f - min);

            changed = Math.Abs(updated - fraction) > float.Epsilon;
            fraction = updated;
        }

        return changed;
    }
}
