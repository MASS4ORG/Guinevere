namespace Guinevere;

/// <summary>How a table column's width is decided.</summary>
public enum TableLengthKind
{
    /// <summary>A fixed number of pixels.</summary>
    Pixels,

    /// <summary>A weighted share of the width the fixed and auto columns leave over.</summary>
    Star,

    /// <summary>As wide as the column's header text; cells are never measured, so wider content is clipped.</summary>
    Auto
}

/// <summary>A column width: pixels, a weighted share of the free width, or the width of the header.</summary>
public readonly record struct TableLength
{
    TableLength(TableLengthKind kind, float value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>How the width is decided.</summary>
    public TableLengthKind Kind { get; }

    /// <summary>The pixel count for <see cref="TableLengthKind.Pixels"/>, the weight for <see cref="TableLengthKind.Star"/>.</summary>
    public float Value { get; }

    /// <summary>A column as wide as its header text.</summary>
    public static TableLength Auto => new(TableLengthKind.Auto, 0f);

    /// <summary>A fixed width.</summary>
    /// <param name="pixels">Width in pixels, zero or more.</param>
    public static TableLength Pixels(float pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pixels);
        return new TableLength(TableLengthKind.Pixels, pixels);
    }

    /// <summary>A share of the width left after fixed and auto columns, proportional to <paramref name="weight"/>.</summary>
    /// <param name="weight">Relative share, above zero.</param>
    public static TableLength Star(float weight = 1f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight);
        return new TableLength(TableLengthKind.Star, weight);
    }

    /// <summary>Reads a plain number as a pixel width.</summary>
    /// <param name="pixels">Width in pixels.</param>
    public static implicit operator TableLength(float pixels) => Pixels(pixels);
}
