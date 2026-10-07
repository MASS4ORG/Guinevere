using System.Globalization;
using SkiaSharp;

namespace Guinevere;

/// <summary>One <c>box-shadow</c> layer, with CSS lengths: the blur radius is twice the Gaussian sigma.</summary>
readonly record struct BoxShadow(bool Inset, float X, float Y, float Blur, float Spread, Color Color);

/// <summary>A resolved <c>outline</c>: a ring drawn <paramref name="Offset"/> pixels outside the box.</summary>
readonly record struct BoxOutline(float Width, float Offset, Color Color);

/// <summary>Parsers for the CSS-shaped visual values a styled box reads.</summary>
static class StyleBoxValues
{
    /// <summary>CSS <c>medium</c> outline width, used when only a color is given.</summary>
    const float DefaultOutlineWidth = 3f;

    static readonly Dictionary<string, float> SideAngles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["to top"] = 0f,
        ["to top right"] = 45f,
        ["to right top"] = 45f,
        ["to right"] = 90f,
        ["to bottom right"] = 135f,
        ["to right bottom"] = 135f,
        ["to bottom"] = 180f,
        ["to bottom left"] = 225f,
        ["to left bottom"] = 225f,
        ["to left"] = 270f,
        ["to top left"] = 315f,
        ["to left top"] = 315f,
    };

    static readonly Dictionary<string, PointerCursor> Cursors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["auto"] = PointerCursor.Default,
        ["default"] = PointerCursor.Default,
        ["arrow"] = PointerCursor.Arrow,
        ["text"] = PointerCursor.Text,
        ["pointer"] = PointerCursor.Hand,
        ["hand"] = PointerCursor.Hand,
        ["crosshair"] = PointerCursor.Crosshair,
        ["ew-resize"] = PointerCursor.ResizeHorizontal,
        ["col-resize"] = PointerCursor.ResizeHorizontal,
        ["ns-resize"] = PointerCursor.ResizeVertical,
        ["row-resize"] = PointerCursor.ResizeVertical,
        ["nwse-resize"] = PointerCursor.ResizeDiagonalNorthWestSouthEast,
        ["nesw-resize"] = PointerCursor.ResizeDiagonalNorthEastSouthWest,
        ["not-allowed"] = PointerCursor.NotAllowed,
    };

    /// <summary>Splits on <paramref name="separator"/> (or any whitespace for <c>' '</c>) outside parentheses and quotes.</summary>
    public static List<string> Split(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '"' or '\'') i = SkipQuoted(text, i);
            else depth += Nesting(text[i]);
            if (depth != 0 || !IsSeparator(text[i], separator)) continue;
            Add(parts, text[start..i]);
            start = i + 1;
        }
        Add(parts, text[start..]);
        return parts;
    }

    static int Nesting(char c) => c == '(' ? 1 : c == ')' ? -1 : 0;

    static bool IsSeparator(char c, char separator) => separator == ' ' ? char.IsWhiteSpace(c) : c == separator;

    static void Add(List<string> parts, string part)
    {
        if (!string.IsNullOrWhiteSpace(part)) parts.Add(part.Trim());
    }

    static int SkipQuoted(string text, int start)
    {
        var end = text.IndexOf(text[start], start + 1);
        return end < 0 ? text.Length - 1 : end;
    }

    /// <summary>Every valid layer of a comma-separated <c>box-shadow</c>, first (top-most) first; <c>none</c> is empty.</summary>
    public static List<BoxShadow> Shadows(string? value)
    {
        var shadows = new List<BoxShadow>();
        if (string.IsNullOrWhiteSpace(value) || IsNone(value)) return shadows;
        foreach (var layer in Split(value, ','))
            if (TryShadow(layer) is { } shadow) shadows.Add(shadow);
        return shadows;
    }

    /// <summary><c>[inset] x y [blur [spread]] [color]</c> in any order of color and keyword; black by default.</summary>
    static BoxShadow? TryShadow(string layer)
    {
        var tokens = Split(layer, ' ');
        var inset = tokens.RemoveAll(t => t.Equals("inset", StringComparison.OrdinalIgnoreCase)) > 0;
        var lengths = new List<float>(4);
        Color? color = null;
        foreach (var token in tokens)
        {
            if (lengths.Count < 4 && TryPixels(token, out var length)) lengths.Add(length);
            else if (StyleValue.TryColor(token, out var parsed)) color = parsed;
            else return null;
        }

        if (lengths.Count < 2) return null;
        return new BoxShadow(inset, lengths[0], lengths[1], Math.Max(0f, lengths.ElementAtOrDefault(2)),
            lengths.ElementAtOrDefault(3), color ?? Color.Black);
    }

    static bool TryPixels(string? text, out float pixels) =>
        StyleValue.TryLength(text, out pixels, out var percent) && !percent;

    /// <summary>
    /// CSS <c>border-radius</c> shorthand with 1–4 values (top-left, top-right, bottom-right, bottom-left); a
    /// percentage is of the box's shorter side. Elliptical <c>/</c> radii keep only the horizontal part.
    /// </summary>
    public static SKPoint[] Radii(string? value, Rect box)
    {
        var radii = new SKPoint[4];
        if (Corners(value, Math.Min(box.W, box.H)) is not { } corners) return radii;

        // CSS fills missing corners from their opposite: 1 → all, 2 → (TL/BR, TR/BL), 3 → (TL, TR/BL, BR).
        int[] source = corners.Length switch { 1 => [0, 0, 0, 0], 2 => [0, 1, 0, 1], 3 => [0, 1, 2, 1], _ => [0, 1, 2, 3] };
        for (var i = 0; i < 4; i++) radii[i] = new SKPoint(corners[source[i]], corners[source[i]]);
        return radii;
    }

    /// <summary>The 1–4 listed radii, or <c>null</c> when the value is missing or malformed.</summary>
    static float[]? Corners(string? value, float shorter)
    {
        var tokens = string.IsNullOrWhiteSpace(value) ? [] : Split(value.Split('/')[0], ' ');
        if (tokens.Count is 0 or > 4) return null;

        var corners = new float[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
            if (!TryCorner(tokens[i], shorter, out corners[i])) return null;
        return corners;
    }

    static bool TryCorner(string token, float shorter, out float radius)
    {
        var parsed = StyleValue.TryLength(token, out var length, out var percent);
        radius = Math.Max(0f, percent ? length * shorter : length);
        return parsed;
    }

    /// <summary>
    /// <c>outline</c> (<c>width [style] color</c> in any order, or <c>none</c>) overridden by <c>outline-width</c>,
    /// <c>outline-color</c> and <c>outline-offset</c>; <c>null</c> when no visible outline results.
    /// </summary>
    public static BoxOutline? Outline(ResolvedStyle style)
    {
        var (width, color) = OutlineShorthand(style.Get("outline"));
        width = style.GetLength("outline-width") ?? width ?? DefaultOutlineWidth;
        color = style.GetColor("outline-color") ?? color;
        return color is { } c && width > 0f ? new BoxOutline(width.Value, style.GetLength("outline-offset") ?? 0f, c) : null;
    }

    static (float? Width, Color? Color) OutlineShorthand(string? value)
    {
        float? width = null;
        Color? color = null;
        if (value is null || IsNone(value)) return (width, color);
        foreach (var token in Split(value, ' '))
        {
            if (TryPixels(token, out var length)) width = length;
            else if (StyleValue.TryColor(token, out var parsed)) color = parsed;
        }
        return (width, color);
    }

    /// <summary>
    /// A <c>linear-gradient([angle | to side], color [pos%], color [pos%], …)</c> shader over <paramref name="box"/>
    /// with CSS geometry (0deg points up, angles turn clockwise); <c>null</c> for anything else.
    /// </summary>
    public static SKShader? Gradient(string? value, SKRect box)
    {
        const string function = "linear-gradient(";
        if (value is null || !value.StartsWith(function, StringComparison.OrdinalIgnoreCase) || !value.EndsWith(')'))
            return null;

        var args = Split(value[function.Length..^1], ',');
        var angle = 0f;
        var hasAngle = args.Count > 0 && TryAngle(args[0], out angle);
        var degrees = hasAngle ? angle : 180f;
        if (!TryStops([.. args.Skip(hasAngle ? 1 : 0)], out var colors, out var positions)) return null;

        var radians = degrees * MathF.PI / 180f;
        var direction = new SKPoint(MathF.Sin(radians), -MathF.Cos(radians));
        var half = (MathF.Abs(box.Width * direction.X) + MathF.Abs(box.Height * direction.Y)) * 0.5f;
        var offset = new SKPoint(direction.X * half, direction.Y * half);
        var center = new SKPoint(box.MidX, box.MidY);
        return SKShader.CreateLinearGradient(center - offset, center + offset, colors, positions, SKShaderTileMode.Clamp);
    }

    static bool TryStops(List<string> stops, out SKColor[] colors, out float[] positions)
    {
        colors = new SKColor[stops.Count];
        positions = new float[stops.Count];
        if (stops.Count < 2) return false;
        for (var i = 0; i < stops.Count; i++)
            if (!TryStop(stops[i], i / (float)(stops.Count - 1), out colors[i], out positions[i])) return false;
        return true;
    }

    /// <summary>A gradient angle: <c>deg</c>, <c>rad</c>, <c>turn</c>, or <c>to top|right|bottom|left</c> (corners too).</summary>
    static bool TryAngle(string text, out float degrees)
    {
        var t = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return SideAngles.TryGetValue(t, out degrees)
               || TryUnit(t, "deg", 1f, out degrees) || TryUnit(t, "turn", 360f, out degrees)
               || TryUnit(t, "rad", 180f / MathF.PI, out degrees);
    }

    static bool TryUnit(string text, string unit, float scale, out float degrees)
    {
        degrees = 0f;
        if (!text.EndsWith(unit, StringComparison.OrdinalIgnoreCase)
            || !float.TryParse(text[..^unit.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return false;
        degrees = value * scale;
        return true;
    }

    /// <summary>A color stop, its color evaluated like any other value (so <c>shade(…)</c> works).</summary>
    static bool TryStop(string stop, float fallback, out SKColor color, out float position)
    {
        color = SKColors.Transparent;
        position = fallback;
        var parts = Split(stop, ' ');
        if (parts.Count == 2 && StyleValue.TryLength(parts[1], out var at, out var percent) && percent)
            position = Math.Clamp(at, 0f, 1f);
        else if (parts.Count != 1) return false;

        if (!StyleValue.TryColor(StyleExpression.Evaluate(parts[0], static _ => null), out var parsed)) return false;
        color = parsed;
        return true;
    }

    /// <summary>A CSS <c>cursor</c> keyword mapped to the closest <see cref="PointerCursor"/>.</summary>
    public static PointerCursor? Cursor(string? value) =>
        value is not null && Cursors.TryGetValue(value.Trim(), out var cursor) ? cursor : null;

    /// <summary>The <c>opacity</c> as a 0..1 fraction (a number or a percentage); <c>null</c> when not set.</summary>
    public static float? Opacity(string? value) =>
        StyleValue.TryLength(value, out var opacity, out _) ? Math.Clamp(opacity, 0f, 1f) : null;

    static bool IsNone(string? value) => string.Equals(value?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
}
