using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Fraction of the track an indeterminate bar's traveling chunk covers.</summary>
    const float IndeterminateChunk = 0.3f;

    /// <summary>Track widths the indeterminate chunk crosses per second.</summary>
    const float IndeterminateSpeed = 0.7f;

    static readonly string[] IndeterminateModifier = ["indeterminate"];

    /// <summary>
    /// Draws a horizontal progress bar filling left to right. Pass a null <paramref name="fraction"/>
    /// for work of unknown length, which animates a traveling chunk instead. The track is styled by the
    /// <c>progress</c> rules and the filled part by its <c>fill</c> part's <c>background-color</c>.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="fraction">Completion in 0..1, clamped; null for indeterminate.</param>
    /// <param name="width">Bar width, or -1 to fill the parent.</param>
    /// <param name="height">Bar height; null uses the stylesheet's height.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for stylesheet selectors.</param>
    /// <param name="filePath">Compiler-supplied call site.</param>
    /// <param name="lineNumber">Compiler-supplied call site.</param>
    public static void ProgressBar(this Gui gui, float? fraction,
        float width = -1, float? height = null, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ExcaliburStyles.Ensure(gui);
        var node = gui.StyledNode("progress", classes, id,
            modifiers: fraction is null ? IndeterminateModifier : NoModifiers,
            filePath: filePath, lineNumber: lineNumber);
        if (width > 0) node.Width(UnitValue.Pixels(width));
        if (height is { } size) node.Height(UnitValue.Pixels(size));
        using (node.Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.InnerRect;
            if (rect.W <= 0) return;

            var filled = FillRect(rect, fraction, gui.Clock.Elapsed);
            gui.DrawStyledBox(gui.ResolvePart("fill"), filled);
        }
    }

    /// <summary>
    /// The filled part of a track: a left-anchored bar for a known fraction, or the traveling chunk
    /// of an indeterminate bar, clipped to the track at both ends of its sweep.
    /// </summary>
    internal static Rect FillRect(Rect track, float? fraction, float elapsed)
    {
        if (fraction is { } value)
            return new Rect(track.X, track.Y, track.W * Math.Clamp(value, 0, 1), track.H);

        var span = 1f + IndeterminateChunk;
        var head = elapsed * IndeterminateSpeed % span - IndeterminateChunk;
        var start = Math.Max(head, 0f);
        var end = Math.Min(head + IndeterminateChunk, 1f);
        return new Rect(track.X + track.W * start, track.Y, track.W * Math.Max(end - start, 0f), track.H);
    }
}
