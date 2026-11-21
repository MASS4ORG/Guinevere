using System.Globalization;
using System.Text;

namespace Guinevere;

/// <summary>A composable layout distance whose weighted terms can be blended across size modes.</summary>
/// <remarks>
/// Every size, constraint, spacing and offset in layout is a <see cref="Size"/>. Terms resolve against the
/// property's basis: the parent's content box for sizes, padding and margins, the node's own content box for gaps.
/// </remarks>
public readonly struct Size : IEquatable<Size>
{
    readonly NodeReference[]? _references;

    /// <summary>Creates a pixel distance.</summary>
    public Size(float pixels) : this(pixels, 0f, 0f, 0f, 0f, 0f, null) { }

    Size(float pixels, float percentage, float ratio, float expand, float fitContent, float fitLargest,
        NodeReference[]? references)
    {
        PixelsContribution = pixels;
        PercentageContribution = percentage;
        RatioContribution = ratio;
        ExpandContribution = expand;
        FitContentContribution = fitContent;
        FitLargestContribution = fitLargest;
        _references = references;
    }

    /// <summary>Weighted absolute-pixel contribution.</summary>
    public float PixelsContribution { get; }
    /// <summary>Weighted fraction of the basis on the property's axis.</summary>
    public float PercentageContribution { get; }
    /// <summary>Weighted fraction of the basis on the perpendicular axis.</summary>
    public float RatioContribution { get; }
    /// <summary>Weight used to share remaining space with sibling expanders.</summary>
    public float ExpandContribution { get; }
    /// <summary>Weighted natural content-size contribution; sizes and constraints only.</summary>
    public float FitContentContribution { get; }
    /// <summary>Weighted largest-child contribution; sizes and constraints only.</summary>
    public float FitLargestContribution { get; }

    /// <summary>Weighted sizes of other current-frame nodes on the property's axis.</summary>
    public ReadOnlySpan<NodeReference> References => _references;

    /// <summary>Whether only the pixel term is set, so the value needs no basis to resolve.</summary>
    public bool IsPixelsOnly => PercentageContribution == 0f && RatioContribution == 0f && ExpandContribution == 0f
        && FitContentContribution == 0f && FitLargestContribution == 0f && _references is null;

    /// <summary>Whether the value is exactly zero pixels.</summary>
    public bool IsZero => PixelsContribution == 0f && IsPixelsOnly;

    /// <summary>Creates an absolute pixel contribution.</summary>
    public static Size Pixels(float pixels) => new(pixels);
    /// <summary>Creates a basis-relative contribution, where 1 is the whole basis.</summary>
    public static Size Percentage(float percentage) => new(0f, percentage, 0f, 0f, 0f, 0f, null);
    /// <summary>Creates a perpendicular-axis ratio contribution.</summary>
    public static Size Ratio(float ratio) => new(0f, 0f, ratio, 0f, 0f, 0f, null);
    /// <summary>Creates a remaining-space contribution.</summary>
    public static Size Expand(float weight = 1f) => new(0f, 0f, 0f, weight, 0f, 0f, null);
    /// <summary>Creates a natural content-size contribution.</summary>
    public static Size FitContent(float weight = 1f) => new(0f, 0f, 0f, 0f, weight, 0f, null);
    /// <summary>Alias for <see cref="FitContent"/>.</summary>
    public static Size Fit => FitContent();
    /// <summary>Creates a contribution based on the largest child on the resolved axis.</summary>
    public static Size FitLargest(float weight = 1f) => new(0f, 0f, 0f, 0f, 0f, weight, null);

    /// <summary>
    /// Creates a fraction of another node's resolved size on the same axis. The node must be built in the frame that
    /// lays it out; layout reports a stale or cyclic reference.
    /// </summary>
    /// <param name="node">The node whose size is referenced.</param>
    /// <param name="fraction">The fraction of that size, where 1 is the whole size.</param>
    public static Size PercentOf(LayoutNode node, float fraction = 1f)
    {
        ArgumentNullException.ThrowIfNull(node);
        return fraction == 0f ? default : new(0f, 0f, 0f, 0f, 0f, 0f, [new NodeReference(node, fraction)]);
    }

    /// <summary>Linearly interpolates every contribution, including between unrelated modes.</summary>
    public static Size Lerp(Size from, Size to, float amount) => from * (1f - amount) + to * amount;

    /// <summary>Adds all weighted contributions.</summary>
    public static Size operator +(Size left, Size right) => new(
        left.PixelsContribution + right.PixelsContribution,
        left.PercentageContribution + right.PercentageContribution,
        left.RatioContribution + right.RatioContribution,
        left.ExpandContribution + right.ExpandContribution,
        left.FitContentContribution + right.FitContentContribution,
        left.FitLargestContribution + right.FitLargestContribution,
        NodeReference.Merge(left._references, right._references));

    /// <summary>Subtracts all weighted contributions.</summary>
    public static Size operator -(Size left, Size right) => left + -right;

    /// <summary>Negates all weighted contributions.</summary>
    public static Size operator -(Size value) => value * -1f;

    /// <summary>Scales all contributions.</summary>
    public static Size operator *(Size value, float weight) => new(
        value.PixelsContribution * weight,
        value.PercentageContribution * weight,
        value.RatioContribution * weight,
        value.ExpandContribution * weight,
        value.FitContentContribution * weight,
        value.FitLargestContribution * weight,
        NodeReference.Scale(value._references, weight));

    /// <summary>Scales all contributions.</summary>
    public static Size operator *(float weight, Size value) => value * weight;
    /// <summary>Adds pixels to an expression.</summary>
    public static Size operator +(Size value, float pixels) => value + Pixels(pixels);
    /// <summary>Adds pixels to an expression.</summary>
    public static Size operator +(float pixels, Size value) => value + pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static Size operator +(Size value, int pixels) => value + (float)pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static Size operator +(int pixels, Size value) => value + pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static Size operator +(Size value, double pixels) => value + (float)pixels;
    /// <summary>Adds pixels to an expression.</summary>
    public static Size operator +(double pixels, Size value) => value + (float)pixels;

    /// <summary>Converts a pixel count into a distance.</summary>
    public static implicit operator Size(float pixels) => Pixels(pixels);
    /// <summary>Converts a pixel count into a distance.</summary>
    public static implicit operator Size(int pixels) => Pixels(pixels);
    /// <summary>Converts a pixel count into a distance.</summary>
    public static implicit operator Size(double pixels) => Pixels((float)pixels);

    /// <summary>Resolves the basis-only terms: pixels, percentage, ratio and an expand share.</summary>
    /// <param name="along">The basis on the property's axis.</param>
    /// <param name="across">The basis on the perpendicular axis.</param>
    /// <param name="expandShare">The space one unit of expand weight receives.</param>
    internal float ResolveBasis(float along, float across, float expandShare = 0f) => PixelsContribution
        + along * PercentageContribution + across * RatioContribution + expandShare * ExpandContribution;

    /// <summary>
    /// Resolves the value inside a rectangle: percentages of its extent on the axis, ratios of the other extent and
    /// node references from their current rects. Expand and fit terms have no meaning there and are ignored.
    /// </summary>
    /// <param name="along">The rectangle's extent on the value's axis.</param>
    /// <param name="across">The rectangle's extent on the other axis.</param>
    /// <param name="horizontal">Whether the value is a width.</param>
    public float Resolve(float along, float across, bool horizontal)
    {
        var value = ResolveBasis(along, across);
        foreach (var reference in References)
        {
            value += reference.Fraction * (horizontal ? reference.Node.Rect.W : reference.Node.Rect.H);
        }

        return value;
    }

    /// <inheritdoc />
    public bool Equals(Size other) => PixelsContribution.Equals(other.PixelsContribution)
        && PercentageContribution.Equals(other.PercentageContribution)
        && RatioContribution.Equals(other.RatioContribution)
        && ExpandContribution.Equals(other.ExpandContribution)
        && FitContentContribution.Equals(other.FitContentContribution)
        && FitLargestContribution.Equals(other.FitLargestContribution)
        && References.SequenceEqual(other.References);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Size other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PixelsContribution, PercentageContribution,
        RatioContribution, ExpandContribution, FitContentContribution, FitLargestContribution, References.Length);

    /// <summary>Compares two values term by term.</summary>
    public static bool operator ==(Size left, Size right) => left.Equals(right);

    /// <summary>Compares two values term by term.</summary>
    public static bool operator !=(Size left, Size right) => !left.Equals(right);

    /// <summary>Formats the value in <c>.pss</c> syntax, such as <c>50% + 8px</c> or <c>expand(2)</c>.</summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        Append(text, PixelsContribution, "{0}px");
        Append(text, PercentageContribution * 100f, "{0}%");
        Append(text, RatioContribution, "ratio({0})");
        Append(text, ExpandContribution, "expand({0})");
        Append(text, FitContentContribution, "fit({0})");
        Append(text, FitLargestContribution, "fit-largest({0})");
        foreach (var reference in References)
        {
            Append(text, reference.Fraction * 100f, "{0}% of " + reference.Node.Id);
        }

        return text.Length == 0 ? "0px" : text.ToString();
    }

    static void Append(StringBuilder text, float weight, string format)
    {
        if (weight == 0f)
        {
            return;
        }

        if (text.Length > 0)
        {
            text.Append(weight < 0f ? " - " : " + ");
            weight = Math.Abs(weight);
        }

        text.AppendFormat(CultureInfo.InvariantCulture, format, weight);
    }
}

/// <summary>A weighted reference to another node's resolved size inside a <see cref="Size"/>.</summary>
/// <param name="Node">The referenced node.</param>
/// <param name="Fraction">The fraction of its size, where 1 is the whole size.</param>
public readonly record struct NodeReference(LayoutNode Node, float Fraction)
{
    /// <summary>Combines two term lists, summing fractions of the same node and dropping zero terms.</summary>
    internal static NodeReference[]? Merge(NodeReference[]? left, NodeReference[]? right)
    {
        if (right is null)
        {
            return left;
        }

        if (left is null)
        {
            return right;
        }

        var merged = new List<NodeReference>(left.Length + right.Length);
        merged.AddRange(left);
        foreach (var term in right)
        {
            var index = merged.FindIndex(existing => ReferenceEquals(existing.Node, term.Node));
            if (index < 0)
            {
                merged.Add(term);
            }
            else
            {
                merged[index] = merged[index] with { Fraction = merged[index].Fraction + term.Fraction };
            }
        }

        merged.RemoveAll(static term => term.Fraction == 0f);
        return merged.Count == 0 ? null : [.. merged];
    }

    /// <summary>Scales every fraction, dropping the list when the weight is zero.</summary>
    internal static NodeReference[]? Scale(NodeReference[]? references, float weight)
    {
        if (references is null || weight == 1f)
        {
            return references;
        }

        if (weight == 0f)
        {
            return null;
        }

        var scaled = new NodeReference[references.Length];
        for (var i = 0; i < references.Length; i++)
        {
            scaled[i] = references[i] with { Fraction = references[i].Fraction * weight };
        }

        return scaled;
    }
}
