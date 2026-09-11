namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Fraction of the track an indeterminate bar's travelling chunk covers.</summary>
    private const float IndeterminateChunk = 0.3f;

    /// <summary>Track widths the indeterminate chunk crosses per second.</summary>
    private const float IndeterminateSpeed = 0.7f;

    /// <summary>
    /// Draws a horizontal progress bar filling left to right. Pass a null <paramref name="fraction"/>
    /// for work of unknown length, which animates a travelling chunk instead.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="fraction">Completion in 0..1, clamped; null for indeterminate.</param>
    /// <param name="width">Bar width, or -1 to fill the parent.</param>
    /// <param name="height">Bar height.</param>
    /// <param name="trackColor">Groove colour; defaults to the palette's.</param>
    /// <param name="fillColor">Filled colour; defaults to the palette's.</param>
    public static void ProgressBar(this Gui gui, float? fraction,
        float width = -1, float height = 6,
        Color? trackColor = null, Color? fillColor = null)
    {
        ArgumentNullException.ThrowIfNull(gui);

        var palette = gui.Controls;
        var track = trackColor ?? palette.ProgressTrack;
        var fill = fillColor ?? palette.ProgressFill;
        var radius = height / 2f;

        using (gui.Node(width, height).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            gui.DrawRect(rect, track, radius);
            if (rect.W <= 0) return;

            var filled = fraction is { } value
                ? new Rect(rect.X, rect.Y, rect.W * Math.Clamp(value, 0, 1), rect.H)
                : IndeterminateChunkRect(rect, gui.Time.Elapsed);

            if (filled.W > 0) gui.DrawRect(filled, fill, radius);
        }
    }

    /// <summary>The travelling chunk, clipped to the track at both ends of its sweep.</summary>
    private static Rect IndeterminateChunkRect(Rect track, float elapsed)
    {
        var span = 1f + IndeterminateChunk;
        var head = (elapsed * IndeterminateSpeed % span) * span - IndeterminateChunk;
        var start = Math.Max(head, 0f);
        var end = Math.Min(head + IndeterminateChunk, 1f);
        return new Rect(track.X + track.W * start, track.Y, track.W * Math.Max(end - start, 0f), track.H);
    }
}
