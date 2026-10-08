using SkiaSharp;

namespace Guinevere;

/// <summary>Draws stylesheet boxes for control parts whose geometry is supplied by the control.</summary>
public static class StyleDrawingExtensions
{
    /// <summary>
    /// Draws a resolved style's background, border, radii, shadows, outline and opacity in the supplied rectangle.
    /// Call from both passes; drawing is queued only in the render pass and adds no layout node.
    /// </summary>
    /// <param name="gui">The GUI drawing the part.</param>
    /// <param name="style">The part's resolved style.</param>
    /// <param name="rect">The part's bounds in screen coordinates.</param>
    public static void DrawStyledBox(this Gui gui, ResolvedStyle style, Rect rect)
    {
        if (gui.Pass != Pass.Pass2Render || rect.W <= 0 || rect.H <= 0) return;
        if (StyleBox.From(style, rect) is not { } box) return;
        var opacity = StyleBoxValues.Opacity(style.Get("opacity")) ?? 1f;
        gui.CurrentNode.DrawList.Add(opacity == 1f ? box : new PartBox(box, opacity));
    }

    sealed class PartBox(StyleBox box, float opacity) : IDrawable, IInkBounds
    {
        /// <inheritdoc/>
        public SKPaint? Paint => null;

        /// <inheritdoc/>
        public SKRect? InkBounds(LayoutNode node) => box.InkBounds(node);

        /// <inheritdoc/>
        public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
        {
            if (opacity <= 0f) return;
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(opacity * 255f + 0.5f)) };
            canvas.SaveLayer(paint);
            try
            {
                box.Render(gui, node, canvas);
            }
            finally
            {
                canvas.Restore();
            }
        }
    }
}
