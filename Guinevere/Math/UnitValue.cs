namespace Guinevere;

/// <summary>A composable layout size whose weighted terms can be blended across size modes.</summary>
public readonly struct UnitValue : IEquatable<UnitValue>
{
    /// <summary>Creates a pixel size.</summary>
    public UnitValue(float pixels) : this(pixels, 0f, 0f, 0f, 0f, 0f) { }

    UnitValue(float pixels, float percentage, float ratio, float expand, float fitContent, float fitLargest)
    {
        PixelsContribution = pixels;
        PercentageContribution = percentage;
        RatioContribution = ratio;
        ExpandContribution = expand;
        FitContentContribution = fitContent;
        FitLargestContribution = fitLargest;
    }

    /// <summary>Weighted absolute-pixel contribution.</summary>
    public float PixelsContribution { get; }
    /// <summary>Weighted fraction of the available parent size.</summary>
    public float PercentageContribution { get; }
    /// <summary>Weighted fraction of the resolved perpendicular size.</summary>
    public float RatioContribution { get; }
    /// <summary>Weight used to share remaining space with sibling expanders.</summary>
    public float ExpandContribution { get; }
    /// <summary>Weighted natural content-size contribution.</summary>
    public float FitContentContribution { get; }
    /// <summary>Weighted largest-child contribution.</summary>
    public float FitLargestContribution { get; }

    /// <summary>The legacy mode for a single-term expression.</summary>
    public UnitType Mode => PercentageContribution != 0f ? UnitType.Percentage
        : RatioContribution != 0f ? UnitType.Ratio
        : ExpandContribution != 0f ? UnitType.Expand
        : FitLargestContribution != 0f ? UnitType.FitLargest
        : FitContentContribution != 0f ? UnitType.Auto
        : UnitType.Pixels;

    /// <summary>The coefficient selected by <see cref="Mode"/> for legacy single-term callers.</summary>
    public float Value => Mode switch
    {
        UnitType.Percentage => PercentageContribution,
        UnitType.Ratio => RatioContribution,
        UnitType.Expand => ExpandContribution,
        UnitType.FitLargest => FitLargestContribution,
        UnitType.Auto => FitContentContribution,
        _ => PixelsContribution
    };

    /// <summary>Creates an absolute pixel contribution.</summary>
    public static UnitValue Pixels(float pixels) => new(pixels);
    /// <summary>Creates a parent-relative contribution.</summary>
    public static UnitValue Percentage(float percentage) => new(0f, percentage, 0f, 0f, 0f, 0f);
    /// <summary>Creates a perpendicular-axis ratio contribution.</summary>
    public static UnitValue Ratio(float ratio) => new(0f, 0f, ratio, 0f, 0f, 0f);
    /// <summary>Creates a remaining-space contribution.</summary>
    public static UnitValue Expand(float weight = 1f) => new(0f, 0f, 0f, weight, 0f, 0f);
    /// <summary>Creates a natural content-size contribution.</summary>
    public static UnitValue FitContent(float weight = 1f) => new(0f, 0f, 0f, 0f, weight, 0f);
    /// <summary>Alias for <see cref="FitContent"/>.</summary>
    public static UnitValue Fit => FitContent();
    /// <summary>Creates a contribution based on the largest child on the resolved axis.</summary>
    public static UnitValue FitLargest(float weight = 1f) => new(0f, 0f, 0f, 0f, 0f, weight);

    /// <summary>Linearly interpolates every contribution, including between unrelated modes.</summary>
    public static UnitValue Lerp(UnitValue from, UnitValue to, float amount) => from * (1f - amount) + to * amount;

    /// <summary>Adds all weighted contributions.</summary>
    public static UnitValue operator +(UnitValue left, UnitValue right) => new(
        left.PixelsContribution + right.PixelsContribution,
        left.PercentageContribution + right.PercentageContribution,
        left.RatioContribution + right.RatioContribution,
        left.ExpandContribution + right.ExpandContribution,
        left.FitContentContribution + right.FitContentContribution,
        left.FitLargestContribution + right.FitLargestContribution);

    /// <summary>Scales all contributions.</summary>
    public static UnitValue operator *(UnitValue value, float weight) => new(
        value.PixelsContribution * weight,
        value.PercentageContribution * weight,
        value.RatioContribution * weight,
        value.ExpandContribution * weight,
        value.FitContentContribution * weight,
        value.FitLargestContribution * weight);

    /// <summary>Scales all contributions.</summary>
    public static UnitValue operator *(float weight, UnitValue value) => value * weight;
    /// <summary>Adds pixels to an expression.</summary>
    public static UnitValue operator +(UnitValue value, float pixels) => value + Pixels(pixels);
    /// <summary>Adds pixels to an expression.</summary>
    public static UnitValue operator +(float pixels, UnitValue value) => value + pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static UnitValue operator +(UnitValue value, int pixels) => value + (float)pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static UnitValue operator +(int pixels, UnitValue value) => value + pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static UnitValue operator +(UnitValue value, double pixels) => value + (float)pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static UnitValue operator +(double pixels, UnitValue value) => value + (float)pixels;

    /// <summary>Converts a pixel count into a size expression.</summary>
    public static implicit operator UnitValue(float pixels) => Pixels(pixels);
    /// <summary>Converts a pixel count into a size expression.</summary>
    public static implicit operator UnitValue(int pixels) => Pixels(pixels);
    /// <summary>Converts a pixel count into a size expression.</summary>
    public static implicit operator UnitValue(double pixels) => Pixels((float)pixels);

    /// <inheritdoc />
    public bool Equals(UnitValue other) => PixelsContribution.Equals(other.PixelsContribution)
        && PercentageContribution.Equals(other.PercentageContribution)
        && RatioContribution.Equals(other.RatioContribution)
        && ExpandContribution.Equals(other.ExpandContribution)
        && FitContentContribution.Equals(other.FitContentContribution)
        && FitLargestContribution.Equals(other.FitLargestContribution);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UnitValue other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PixelsContribution, PercentageContribution,
        RatioContribution, ExpandContribution, FitContentContribution, FitLargestContribution);
}
