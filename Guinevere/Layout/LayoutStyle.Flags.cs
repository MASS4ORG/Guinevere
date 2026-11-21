namespace Guinevere;

/// <summary>
/// Layout's fast reads of a <see cref="LayoutStyle"/>: which values are set and pixel-only, and their pixel terms,
/// without copying whole <see cref="Size"/> values per child.
/// </summary>
public partial struct LayoutStyle
{
    /// <summary>Which optional values are set and which values need a basis to resolve.</summary>
    [Flags]
    internal enum StyleFlags : uint
    {
        None = 0,
        WidthSet = 1 << 0,
        WidthRelative = 1 << 1,
        HeightSet = 1 << 2,
        HeightRelative = 1 << 3,
        MinWidthSet = 1 << 4,
        MinWidthRelative = 1 << 5,
        MaxWidthSet = 1 << 6,
        MaxWidthRelative = 1 << 7,
        MinHeightSet = 1 << 8,
        MinHeightRelative = 1 << 9,
        MaxHeightSet = 1 << 10,
        MaxHeightRelative = 1 << 11,
        AlignSelfSet = 1 << 12,
        GapRelative = 1 << 13,
        MarginTopRelative = 1 << 14,
        MarginRightRelative = 1 << 15,
        MarginBottomRelative = 1 << 16,
        MarginLeftRelative = 1 << 17,
        PaddingTopRelative = 1 << 18,
        PaddingRightRelative = 1 << 19,
        PaddingBottomRelative = 1 << 20,
        PaddingLeftRelative = 1 << 21,
        WidthBounds = MinWidthSet | MaxWidthSet,
        WidthBoundsRelative = MinWidthRelative | MaxWidthRelative,
        HeightBounds = MinHeightSet | MaxHeightSet,
        HeightBoundsRelative = MinHeightRelative | MaxHeightRelative,
        SpacingRelative = GapRelative | MarginTopRelative | MarginRightRelative | MarginBottomRelative
                          | MarginLeftRelative | PaddingTopRelative | PaddingRightRelative | PaddingBottomRelative
                          | PaddingLeftRelative,
    }

    /// <summary>Whether a width is set.</summary>
    internal readonly bool HasWidth => Has(StyleFlags.WidthSet);

    /// <summary>Whether a height is set.</summary>
    internal readonly bool HasHeight => Has(StyleFlags.HeightSet);

    /// <summary>Whether the width is set and pixels only, so <see cref="FixedWidth"/> is the whole value.</summary>
    internal readonly bool WidthIsFixed =>
        (_flags & (StyleFlags.WidthSet | StyleFlags.WidthRelative)) == StyleFlags.WidthSet;

    /// <summary>Whether the height is set and pixels only, so <see cref="FixedHeight"/> is the whole value.</summary>
    internal readonly bool HeightIsFixed =>
        (_flags & (StyleFlags.HeightSet | StyleFlags.HeightRelative)) == StyleFlags.HeightSet;

    /// <summary>The pixel term of the width.</summary>
    internal readonly float FixedWidth => PixelsAt(WidthSlot);

    /// <summary>The pixel term of the height.</summary>
    internal readonly float FixedHeight => PixelsAt(HeightSlot);

    /// <summary>Whether an unsized or zero-pixel relative width gets the floor that keeps a child visible.</summary>
    internal readonly bool WidthNeedsFloor => !Has(StyleFlags.WidthSet)
        || (Has(StyleFlags.WidthRelative) && PixelsAt(WidthSlot) <= 0f);

    /// <summary>Whether an unsized or zero-pixel relative height gets the floor that keeps a child visible.</summary>
    internal readonly bool HeightNeedsFloor => !Has(StyleFlags.HeightSet)
        || (Has(StyleFlags.HeightRelative) && PixelsAt(HeightSlot) <= 0f);

    /// <summary>The width's expand weight, or zero.</summary>
    internal readonly float WidthExpandWeight => Has(StyleFlags.WidthRelative) ? Expand(WidthSlot) : 0f;

    /// <summary>The height's expand weight, or zero.</summary>
    internal readonly float HeightExpandWeight => Has(StyleFlags.HeightRelative) ? Expand(HeightSlot) : 0f;

    /// <summary>Whether a minimum or maximum width is set.</summary>
    internal readonly bool HasWidthBounds => (_flags & StyleFlags.WidthBounds) != 0;

    /// <summary>Whether a minimum or maximum height is set.</summary>
    internal readonly bool HasHeightBounds => (_flags & StyleFlags.HeightBounds) != 0;

    /// <summary>Whether the width bounds need no basis.</summary>
    internal readonly bool WidthBoundsArePixels => (_flags & StyleFlags.WidthBoundsRelative) == 0;

    /// <summary>Whether the height bounds need no basis.</summary>
    internal readonly bool HeightBoundsArePixels => (_flags & StyleFlags.HeightBoundsRelative) == 0;

    /// <summary>The pixel lower width bound, or zero when unset.</summary>
    internal readonly float MinWidthPixels =>
        Has(StyleFlags.MinWidthSet) ? Math.Max(0f, PixelsAt(MinWidthSlot)) : 0f;

    /// <summary>The pixel upper width bound, or <see cref="float.MaxValue"/> when unset.</summary>
    internal readonly float MaxWidthPixels =>
        Has(StyleFlags.MaxWidthSet) ? Math.Max(0f, PixelsAt(MaxWidthSlot)) : float.MaxValue;

    /// <summary>The pixel lower height bound, or zero when unset.</summary>
    internal readonly float MinHeightPixels =>
        Has(StyleFlags.MinHeightSet) ? Math.Max(0f, PixelsAt(MinHeightSlot)) : 0f;

    /// <summary>The pixel upper height bound, or <see cref="float.MaxValue"/> when unset.</summary>
    internal readonly float MaxHeightPixels =>
        Has(StyleFlags.MaxHeightSet) ? Math.Max(0f, PixelsAt(MaxHeightSlot)) : float.MaxValue;

    /// <summary>Whether the node overrides its parent's cross alignment.</summary>
    internal readonly bool HasAlignSelf => Has(StyleFlags.AlignSelfSet);

    /// <summary>Whether gap, margins and padding all need no basis.</summary>
    internal readonly bool SpacingIsPixels => (_flags & StyleFlags.SpacingRelative) == 0;

    /// <summary>Whether the gap needs no basis.</summary>
    internal readonly bool GapIsPixels => !Has(StyleFlags.GapRelative);

    /// <summary>The pixel term of the gap.</summary>
    internal readonly float GapPixels => PixelsAt(GapSlot);

    /// <summary>The pixel terms of the padding as top, right, bottom, left.</summary>
    internal readonly Vector4 PaddingPixels => new(PixelsAt(PaddingTopSlot),
        PixelsAt(PaddingRightSlot), PixelsAt(PaddingBottomSlot), PixelsAt(PaddingLeftSlot));

    /// <summary>The pixel terms of the margins as top, right, bottom, left.</summary>
    internal readonly Vector4 MarginPixels => new(PixelsAt(MarginTopSlot),
        PixelsAt(MarginRightSlot), PixelsAt(MarginBottomSlot), PixelsAt(MarginLeftSlot));

    /// <summary>The width as stored; meaningful only when <see cref="HasWidth"/>.</summary>
    internal readonly Size WidthValue => Read(WidthSlot);

    /// <summary>The height as stored; meaningful only when <see cref="HasHeight"/>.</summary>
    internal readonly Size HeightValue => Read(HeightSlot);

    /// <summary>The lower width bound as stored; meaningful only when <see cref="HasMinWidth"/>.</summary>
    internal readonly Size MinWidthValue => Read(MinWidthSlot);

    /// <summary>The upper width bound as stored; meaningful only when <see cref="HasMaxWidth"/>.</summary>
    internal readonly Size MaxWidthValue => Read(MaxWidthSlot);

    /// <summary>The lower height bound as stored; meaningful only when <see cref="HasMinHeight"/>.</summary>
    internal readonly Size MinHeightValue => Read(MinHeightSlot);

    /// <summary>The upper height bound as stored; meaningful only when <see cref="HasMaxHeight"/>.</summary>
    internal readonly Size MaxHeightValue => Read(MaxHeightSlot);

    /// <summary>The cross alignment override as stored; meaningful only when <see cref="HasAlignSelf"/>.</summary>
    internal readonly Size AlignSelfValue => Read(AlignSelfSlot);

    /// <summary>Whether a lower width bound is set.</summary>
    internal readonly bool HasMinWidth => Has(StyleFlags.MinWidthSet);

    /// <summary>Whether an upper width bound is set.</summary>
    internal readonly bool HasMaxWidth => Has(StyleFlags.MaxWidthSet);

    /// <summary>Whether a lower height bound is set.</summary>
    internal readonly bool HasMinHeight => Has(StyleFlags.MinHeightSet);

    /// <summary>Whether an upper height bound is set.</summary>
    internal readonly bool HasMaxHeight => Has(StyleFlags.MaxHeightSet);

    /// <summary>The top margin's expand weight, or zero.</summary>
    internal readonly float MarginTopWeight => Expand(MarginTopSlot);

    /// <summary>The right margin's expand weight, or zero.</summary>
    internal readonly float MarginRightWeight => Expand(MarginRightSlot);

    /// <summary>The bottom margin's expand weight, or zero.</summary>
    internal readonly float MarginBottomWeight => Expand(MarginBottomSlot);

    /// <summary>The left margin's expand weight, or zero.</summary>
    internal readonly float MarginLeftWeight => Expand(MarginLeftSlot);

    /// <summary>The gap's expand weight, or zero.</summary>
    internal readonly float GapExpandWeight => Expand(GapSlot);

    /// <summary>Whether <paramref name="slot"/> holds at most one non-pixel term and no node references.</summary>
    internal readonly bool IsSimple(int slot) => KindOf(slot) != TermKind.Complex;

    /// <summary>
    /// The pixel term of a <see cref="IsSimple"/> slot plus its percentage of <paramref name="basis"/>, the only terms
    /// an alignment offset resolves without references.
    /// </summary>
    /// <param name="slot">The slot to read.</param>
    /// <param name="basis">What a whole percentage stands for.</param>
    internal readonly float PixelsAndPercentage(int slot, float basis)
    {
        var percentage = KindOf(slot) == TermKind.Percentage ? TermAt(slot) : 0f;
        return PixelsAt(slot) + percentage * basis;
    }
}
