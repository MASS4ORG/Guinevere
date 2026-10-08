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
    /// <see cref="LayoutNode.Direction"/> matches <paramref name="axis"/>.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="fraction">The split position, 0..1, updated in place while dragging.</param>
    /// <param name="axis">The container's layout direction: horizontal splits side by side.</param>
    /// <param name="thickness">The divider's width across the split; null uses the stylesheet's size.</param>
    /// <param name="min">The closest either side may get to collapsing, as a fraction.</param>
    /// <param name="classes">Extra classes for the sheet; the handle is styled by the <c>splitter</c> rules, with
    /// <c>:hover</c> and <c>:active</c> (dragging) marking the grab zone.</param>
    /// <param name="id">Element id for stylesheet selectors and persistent drag state.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>True if this frame moved the divider.</returns>
    public static bool Splitter(this Gui gui, ref float fraction, Axis axis, float? thickness = null,
        float min = 0.1f, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        bool changed;
        var horizontal = axis == Axis.Horizontal;

        ExcaliburStyles.Ensure(gui);
        var node = gui.StyledNode("splitter", classes, id,
            modifiers: horizontal ? HorizontalSplitterModifier : VerticalSplitterModifier,
            filePath: filePath, lineNumber: lineNumber);
        if (thickness is { } size)
            node = horizontal ? node.Width(UnitValue.Pixels(size)) : node.Height(UnitValue.Pixels(size));

        using (node.Enter())
        {
            ref var anchor = ref gui.GetValue(float.NaN, $"{node.Id}/splitterAnchor");
            if (gui.Pass != Pass.Pass2Render) return false;

            var interactable = gui.GetInteractable();
            var dragging = interactable.OnDrag(out var args);

            if (!dragging)
            {
                anchor = float.NaN;
                return false;
            }

            var track = gui.CurrentNode.Parent?.InnerRect;
            var span = horizontal ? track?.W ?? 0f : track?.H ?? 0f;
            if (span <= 0f) return false;

            // Total travel keeps the divider anchored to the press position even when the pointer stops.
            if (float.IsNaN(anchor)) anchor = fraction;

            var travel = horizontal ? args.TotalDelta.X : args.TotalDelta.Y;
            var updated = Math.Clamp(anchor + travel / span, min, 1f - min);

            changed = Math.Abs(updated - fraction) > float.Epsilon;
            fraction = updated;
        }

        return changed;
    }
}
