namespace Guinevere;

/// <summary>A component of an <see cref="Hsva"/> color, as edited by a one-dimensional strip.</summary>
public enum HsvChannel
{
    /// <summary>Hue, around the color circle.</summary>
    Hue,

    /// <summary>Saturation, from gray to the pure hue.</summary>
    Saturation,

    /// <summary>Value, from black to full brightness.</summary>
    Value,

    /// <summary>Alpha, from transparent to opaque.</summary>
    Alpha
}

/// <summary>
/// The two-dimensional surface of a color picker, such as a saturation/value square or a hue wheel. Implement it to
/// add a shape to <see cref="ControlsExtensions.ColorArea"/>; the area handles input, focus and the cursor ring.
/// </summary>
public interface IColorPickerShape
{
    /// <summary>Name shown wherever shapes are listed.</summary>
    string Name { get; }

    /// <summary>The channel this shape leaves out, which the default picker edits with a strip beside it.</summary>
    HsvChannel Companion { get; }

    /// <summary>Paints the surface for <paramref name="value"/>; called in the render pass only.</summary>
    /// <param name="gui">The GUI drawing into the area node; <see cref="ColorPaint"/> has helpers.</param>
    /// <param name="rect">The area's content rectangle.</param>
    /// <param name="value">The color being edited.</param>
    void Draw(Gui gui, Rect rect, Hsva value);

    /// <summary>Where <paramref name="value"/> sits on the surface, for the cursor ring and keyboard nudges.</summary>
    /// <param name="rect">The area's content rectangle.</param>
    /// <param name="value">The color being edited.</param>
    Vector2 Cursor(Rect rect, Hsva value);

    /// <summary>The color under <paramref name="point"/>, clamped onto the surface; the companion channel is kept.</summary>
    /// <param name="rect">The area's content rectangle.</param>
    /// <param name="point">Pointer position, possibly outside the surface while dragging.</param>
    /// <param name="value">The color before the pick.</param>
    Hsva Pick(Rect rect, Vector2 point, Hsva value);
}

/// <summary>The built-in <see cref="IColorPickerShape"/> implementations.</summary>
public static class ColorPickerShapes
{
    /// <summary>Saturation left to right and value bottom to top, for the current hue.</summary>
    public static IColorPickerShape Rectangle { get; } = new HsvRectangle();

    /// <summary>Hue around a disc and saturation from its center, for the current value.</summary>
    public static IColorPickerShape Wheel { get; } = new HueWheel();

    /// <summary>The shapes shipped with the picker.</summary>
    public static IReadOnlyList<IColorPickerShape> All { get; } = [Rectangle, Wheel];

    sealed class HsvRectangle : IColorPickerShape
    {
        public string Name => "Rectangle";
        public HsvChannel Companion => HsvChannel.Hue;

        public void Draw(Gui gui, Rect rect, Hsva value)
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            gui.DrawRect(rect, new Hsva(value.H, 1f, 1f).ToColor());
            ColorPaint.FillGradient(gui, rect, Color.White, new Color(Color.White, 0f));
            ColorPaint.FillGradient(gui, rect, Color.Black, new Color(Color.Black, 0f), vertical: true);
        }

        public Vector2 Cursor(Rect rect, Hsva value) =>
            new(rect.X + Math.Clamp(value.S, 0f, 1f) * rect.W, rect.Y + (1f - Math.Clamp(value.V, 0f, 1f)) * rect.H);

        public Hsva Pick(Rect rect, Vector2 point, Hsva value) =>
            value with
            {
                S = rect.W > 0 ? Math.Clamp((point.X - rect.X) / rect.W, 0f, 1f) : value.S,
                V = rect.H > 0 ? 1f - Math.Clamp((point.Y - rect.Y) / rect.H, 0f, 1f) : value.V
            };
    }

    sealed class HueWheel : IColorPickerShape
    {
        public string Name => "Wheel";
        public HsvChannel Companion => HsvChannel.Value;

        public void Draw(Gui gui, Rect rect, Hsva value)
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            var (center, radius) = Disc(rect);
            ColorPaint.FillHueWheel(gui, center, radius);
            ColorPaint.FillRadial(gui, center, radius, Color.White, new Color(Color.White, 0f));
            gui.DrawCircle(center, radius, new Color(Color.Black, 1f - Math.Clamp(value.V, 0f, 1f)));
        }

        public Vector2 Cursor(Rect rect, Hsva value)
        {
            var (center, radius) = Disc(rect);
            var angle = value.H * MathF.PI / 180f;
            var distance = Math.Clamp(value.S, 0f, 1f) * radius;
            return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }

        public Hsva Pick(Rect rect, Vector2 point, Hsva value)
        {
            var (center, radius) = Disc(rect);
            var offset = point - center;
            var distance = offset.Length();
            if (radius <= 0)
            {
                return value;
            }

            if (distance < 0.0001f)
            {
                return value with { S = 0f };
            }

            var hue = MathF.Atan2(offset.Y, offset.X) * 180f / MathF.PI;
            return value with { H = hue < 0 ? hue + 360f : hue, S = Math.Min(1f, distance / radius) };
        }

        static (Vector2 Center, float Radius) Disc(Rect rect) =>
            (new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f), Math.Max(0f, Math.Min(rect.W, rect.H) / 2f));
    }
}
