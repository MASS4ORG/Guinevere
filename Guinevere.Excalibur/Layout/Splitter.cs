using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] HorizontalSplitterModifier = ["horizontal"];
    static readonly string[] VerticalSplitterModifier = ["vertical"];

    /// <summary>
    /// Draws a draggable divider between two siblings of a flow container and updates
    /// <paramref name="fraction"/> — the share of the container the sibling before it takes — as the
    /// pointer moves. Place it between the two nodes it separates, inside a container whose
    /// <see cref="LayoutNode.Direction"/> matches <paramref name="axis"/>. Its thickness comes from the
    /// <c>splitter</c> rules; set <c>.Width(...)</c> or <c>.Height(...)</c> on the returned node to override it.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="fraction">The split position, 0..1, updated in place while dragging.</param>
    /// <param name="axis">The container's layout direction: horizontal splits side by side.</param>
    /// <param name="min">The closest either side may get to collapsing, as a fraction.</param>
    /// <param name="enabled">When false the divider matches <c>:disabled</c> and cannot be dragged.</param>
    /// <param name="classes">Extra classes for the sheet; the handle is styled by the <c>splitter</c> rules, with
    /// <c>:hover</c> and <c>:active</c> (dragging) marking the grab zone.</param>
    /// <param name="id">Optional id for stylesheet selectors and persistent drag state; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The divider's node, <see cref="LayoutNode.Changed"/> (and as a <c>bool</c>) on a frame it moved.</returns>
    public static ControlNode Splitter(this Gui gui, ref float fraction, Axis axis, float min = 0.1f,
        bool enabled = true, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var horizontal = axis == Axis.Horizontal;

        ExcaliburStyles.Ensure(gui);
        var node = gui.StyledControl("splitter", classes, id,
            modifiers: horizontal ? HorizontalSplitterModifier : VerticalSplitterModifier, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber);

        using (node.Enter())
        {
            ref var anchor = ref gui.GetValue(float.NaN, $"{node.Id}/splitterAnchor");
            if (gui.Pass == Pass.Pass2Render && DragSplitter(gui, ref fraction, ref anchor, horizontal, min, enabled))
            {
                node.Raise(NodeSignals.Changed);
            }
        }

        return node;
    }

    /// <summary>Moves <paramref name="fraction"/> with the drag in progress on the current node; true when it moved.</summary>
    static bool DragSplitter(Gui gui, ref float fraction, ref float anchor, bool horizontal, float min, bool enabled)
    {
        if (!enabled || !gui.GetInteractable().OnDrag(out var args))
        {
            anchor = float.NaN;
            return false;
        }

        var span = SplitterSpan(gui, horizontal);
        if (span <= 0f)
        {
            return false;
        }

        // Total travel keeps the divider anchored to the press position even when the pointer stops.
        if (float.IsNaN(anchor))
        {
            anchor = fraction;
        }

        var travel = horizontal ? args.TotalDelta.X : args.TotalDelta.Y;
        var updated = Math.Clamp(anchor + travel / span, min, 1f - min);

        var moved = Math.Abs(updated - fraction) > float.Epsilon;
        fraction = updated;
        return moved;
    }

    /// <summary>The track length along the split axis, or zero when the parent is not laid out yet.</summary>
    static float SplitterSpan(Gui gui, bool horizontal)
    {
        var track = gui.CurrentNode.Parent?.InnerRect;
        return horizontal ? track?.W ?? 0f : track?.H ?? 0f;
    }
}
