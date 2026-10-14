namespace Guinevere;

/// <summary>The geometry used to reveal a value fill.</summary>
public enum FillKind
{
    /// <summary>A continuous rectangular fill.</summary>
    Linear,
    /// <summary>An elliptical sector or ring.</summary>
    Radial,
    /// <summary>A row or column of separated cells, with a partial final cell.</summary>
    Segmented
}

/// <summary>The edge from which a linear or segmented fill grows.</summary>
public enum FillOrigin
{
    /// <summary>Grows rightward.</summary>
    Left,
    /// <summary>Grows leftward.</summary>
    Right,
    /// <summary>Grows downward.</summary>
    Top,
    /// <summary>Grows upward.</summary>
    Bottom
}

/// <summary>How values outside a fill's input range are displayed.</summary>
public enum FillOverflow
{
    /// <summary>Stops at the empty and full endpoints.</summary>
    Clamp,
    /// <summary>Repeats the fractional part outside the range; in-range endpoints remain empty and full.</summary>
    Repeat
}

/// <summary>A color applied at or below a normalized upper bound; the smallest matching bound wins.</summary>
public readonly record struct FillThreshold(float UpperBound, Color Color);

/// <summary>Presentation settings shared by value fills; image and path resources remain caller-owned.</summary>
public sealed record FillOptions
{
    /// <summary>The fill geometry.</summary>
    public FillKind Kind { get; init; }
    /// <summary>The growth edge for rectangular geometry.</summary>
    public FillOrigin Origin { get; init; }
    /// <summary>The input value mapped to empty; reversed ranges are supported.</summary>
    public float Minimum { get; init; }
    /// <summary>The input value mapped to full.</summary>
    public float Maximum { get; init; } = 1f;
    /// <summary>Optional raw-value to normalized-value mapping that replaces the minimum/maximum calculation.</summary>
    public Func<float, float>? Normalizer { get; init; }
    /// <summary>The treatment of values outside the input range.</summary>
    public FillOverflow Overflow { get; init; }
    /// <summary>The radial origin in degrees; zero points right and -90 points up.</summary>
    public float StartAngle { get; init; } = -90f;
    /// <summary>The radial range in degrees, between zero and 360.</summary>
    public float SweepAngle { get; init; } = 360f;
    /// <summary>Whether radial progress travels clockwise.</summary>
    public bool Clockwise { get; init; } = true;
    /// <summary>The inner radius as a fraction of the outer radius; zero produces a sector.</summary>
    public float InnerRadius { get; init; }
    /// <summary>The number of cells in a segmented fill.</summary>
    public int Segments { get; init; } = 10;
    /// <summary>The gap between cells in destination pixels.</summary>
    public float Gap { get; init; } = 2f;
    /// <summary>The fill color, also multiplied into a sprite when supplied.</summary>
    public Color Color { get; init; } = Color.White;
    /// <summary>Optional colors selected from the normalized display value.</summary>
    public IReadOnlyList<FillThreshold> Thresholds { get; init; } = [];
    /// <summary>An optional sprite stretched across the entire destination, then revealed by the fill.</summary>
    public SKImage? Sprite { get; init; }
    /// <summary>An optional source rectangle in sprite pixels, for atlas regions.</summary>
    public Rect? Source { get; init; }
    /// <summary>Optional nine-slice borders in source-region pixels, preserving corners before revealing the sprite.</summary>
    public Insets? SpriteBorder { get; init; }
    /// <summary>An optional alpha mask stretched across the destination; RGB channels are ignored.</summary>
    public SKImage? Mask { get; init; }
    /// <summary>An optional silhouette in destination coordinates, intersected with fill geometry.</summary>
    public SKPath? Shape { get; init; }

    /// <summary>Maps a host value into the normalized display range; non-finite values produce an empty fill.</summary>
    public float Normalize(float value)
    {
        var fraction = Normalizer is { } normalize ? normalize(value) : (value - Minimum) / (Maximum - Minimum);
        if (!float.IsFinite(fraction))
        {
            return 0f;
        }
        if (Overflow == FillOverflow.Repeat && (fraction < 0f || fraction > 1f))
        {
            return fraction - MathF.Floor(fraction);
        }
        return Math.Clamp(fraction, 0f, 1f);
    }

    internal Color ResolveColor(float fraction)
    {
        var color = Color;
        var bound = float.PositiveInfinity;
        foreach (var threshold in Thresholds)
        {
            if (fraction <= threshold.UpperBound && threshold.UpperBound < bound)
            {
                bound = threshold.UpperBound;
                color = threshold.Color;
            }
        }
        return color;
    }
}
