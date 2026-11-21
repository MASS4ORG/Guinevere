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
    internal readonly float FixedWidth => _width.PixelsContribution;

    /// <summary>The pixel term of the height.</summary>
    internal readonly float FixedHeight => _height.PixelsContribution;

    /// <summary>Whether an unsized or zero-pixel relative width gets the floor that keeps a child visible.</summary>
    internal readonly bool WidthNeedsFloor => !Has(StyleFlags.WidthSet)
        || (Has(StyleFlags.WidthRelative) && _width.PixelsContribution <= 0f);

    /// <summary>Whether an unsized or zero-pixel relative height gets the floor that keeps a child visible.</summary>
    internal readonly bool HeightNeedsFloor => !Has(StyleFlags.HeightSet)
        || (Has(StyleFlags.HeightRelative) && _height.PixelsContribution <= 0f);

    /// <summary>The width's expand weight, or zero.</summary>
    internal readonly float WidthExpandWeight => Has(StyleFlags.WidthRelative) ? _width.ExpandContribution : 0f;

    /// <summary>The height's expand weight, or zero.</summary>
    internal readonly float HeightExpandWeight => Has(StyleFlags.HeightRelative) ? _height.ExpandContribution : 0f;

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
        Has(StyleFlags.MinWidthSet) ? Math.Max(0f, _minWidth.PixelsContribution) : 0f;

    /// <summary>The pixel upper width bound, or <see cref="float.MaxValue"/> when unset.</summary>
    internal readonly float MaxWidthPixels =>
        Has(StyleFlags.MaxWidthSet) ? Math.Max(0f, _maxWidth.PixelsContribution) : float.MaxValue;

    /// <summary>The pixel lower height bound, or zero when unset.</summary>
    internal readonly float MinHeightPixels =>
        Has(StyleFlags.MinHeightSet) ? Math.Max(0f, _minHeight.PixelsContribution) : 0f;

    /// <summary>The pixel upper height bound, or <see cref="float.MaxValue"/> when unset.</summary>
    internal readonly float MaxHeightPixels =>
        Has(StyleFlags.MaxHeightSet) ? Math.Max(0f, _maxHeight.PixelsContribution) : float.MaxValue;

    /// <summary>Whether the node overrides its parent's cross alignment.</summary>
    internal readonly bool HasAlignSelf => Has(StyleFlags.AlignSelfSet);

    /// <summary>Whether gap, margins and padding all need no basis.</summary>
    internal readonly bool SpacingIsPixels => (_flags & StyleFlags.SpacingRelative) == 0;

    /// <summary>Whether the gap needs no basis.</summary>
    internal readonly bool GapIsPixels => !Has(StyleFlags.GapRelative);

    /// <summary>The pixel term of the gap.</summary>
    internal readonly float GapPixels => _gap.PixelsContribution;

    /// <summary>The pixel terms of the padding as top, right, bottom, left.</summary>
    internal readonly Vector4 PaddingPixels => new(_paddingTop.PixelsContribution,
        _paddingRight.PixelsContribution, _paddingBottom.PixelsContribution, _paddingLeft.PixelsContribution);

    /// <summary>The pixel terms of the margins as top, right, bottom, left.</summary>
    internal readonly Vector4 MarginPixels => new(_marginTop.PixelsContribution,
        _marginRight.PixelsContribution, _marginBottom.PixelsContribution, _marginLeft.PixelsContribution);
}
