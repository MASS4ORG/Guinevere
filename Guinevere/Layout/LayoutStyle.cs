namespace Guinevere;

/// <summary>
/// The layout properties of a <see cref="LayoutNode"/>. Every size, constraint, spacing and offset is a
/// <see cref="Size"/>, so any of them can mix pixels, percentages, ratios, expansion and node references.
/// </summary>
/// <remarks>
/// Setters keep a mask of which values are set and which need a basis, so layout can test the common pixel-only
/// case with one read instead of copying each <see cref="Size"/>. Values are stored compactly (see
/// <c>LayoutStyle.Storage.cs</c>), because every node carries one.
/// </remarks>
public partial struct LayoutStyle
{
    SlotFloats _pixels;
    SlotFloats _terms;
    ulong _kinds;
    Size[]? _complex;
    StyleFlags _flags;

    /// <summary>The width; null sizes to content along the parent's flow and stretches across it.</summary>
    public Size? Width
    {
        readonly get => Get(WidthSlot, StyleFlags.WidthSet);
        set => SetOptional(WidthSlot, value, StyleFlags.WidthSet, StyleFlags.WidthRelative);
    }

    /// <summary>The height; null sizes to content along the parent's flow and stretches across it.</summary>
    public Size? Height
    {
        readonly get => Get(HeightSlot, StyleFlags.HeightSet);
        set => SetOptional(HeightSlot, value, StyleFlags.HeightSet, StyleFlags.HeightRelative);
    }

    /// <summary>Lower bound on the resolved width, or null for none.</summary>
    public Size? MinWidth
    {
        readonly get => Get(MinWidthSlot, StyleFlags.MinWidthSet);
        set => SetOptional(MinWidthSlot, value, StyleFlags.MinWidthSet, StyleFlags.MinWidthRelative);
    }

    /// <summary>Upper bound on the resolved width, or null for none.</summary>
    public Size? MaxWidth
    {
        readonly get => Get(MaxWidthSlot, StyleFlags.MaxWidthSet);
        set => SetOptional(MaxWidthSlot, value, StyleFlags.MaxWidthSet, StyleFlags.MaxWidthRelative);
    }

    /// <summary>Lower bound on the resolved height, or null for none.</summary>
    public Size? MinHeight
    {
        readonly get => Get(MinHeightSlot, StyleFlags.MinHeightSet);
        set => SetOptional(MinHeightSlot, value, StyleFlags.MinHeightSet, StyleFlags.MinHeightRelative);
    }

    /// <summary>Upper bound on the resolved height, or null for none.</summary>
    public Size? MaxHeight
    {
        readonly get => Get(MaxHeightSlot, StyleFlags.MaxHeightSet);
        set => SetOptional(MaxHeightSlot, value, StyleFlags.MaxHeightSet, StyleFlags.MaxHeightRelative);
    }

    /// <summary>This node's placement across its parent's flow, overriding the parent's content alignment.</summary>
    public Size? AlignSelf
    {
        readonly get => Get(AlignSelfSlot, StyleFlags.AlignSelfSet);
        set => SetOptional(AlignSelfSlot, value, StyleFlags.AlignSelfSet, StyleFlags.None);
    }

    /// <summary>Space between adjacent children; percentages use this node's content box on the flow axis.</summary>
    public Size Gap
    {
        readonly get => Read(GapSlot);
        set => Set(GapSlot, value, StyleFlags.GapRelative);
    }

    /// <summary>Space above the node; percentages use the parent's content height.</summary>
    public Size MarginTop
    {
        readonly get => Read(MarginTopSlot);
        set => Set(MarginTopSlot, value, StyleFlags.MarginTopRelative);
    }

    /// <summary>Space right of the node; percentages use the parent's content width.</summary>
    public Size MarginRight
    {
        readonly get => Read(MarginRightSlot);
        set => Set(MarginRightSlot, value, StyleFlags.MarginRightRelative);
    }

    /// <summary>Space below the node; percentages use the parent's content height.</summary>
    public Size MarginBottom
    {
        readonly get => Read(MarginBottomSlot);
        set => Set(MarginBottomSlot, value, StyleFlags.MarginBottomRelative);
    }

    /// <summary>Space left of the node; percentages use the parent's content width.</summary>
    public Size MarginLeft
    {
        readonly get => Read(MarginLeftSlot);
        set => Set(MarginLeftSlot, value, StyleFlags.MarginLeftRelative);
    }

    /// <summary>Space inside the top edge; percentages use the parent's content height.</summary>
    public Size PaddingTop
    {
        readonly get => Read(PaddingTopSlot);
        set => Set(PaddingTopSlot, value, StyleFlags.PaddingTopRelative);
    }

    /// <summary>Space inside the right edge; percentages use the parent's content width.</summary>
    public Size PaddingRight
    {
        readonly get => Read(PaddingRightSlot);
        set => Set(PaddingRightSlot, value, StyleFlags.PaddingRightRelative);
    }

    /// <summary>Space inside the bottom edge; percentages use the parent's content height.</summary>
    public Size PaddingBottom
    {
        readonly get => Read(PaddingBottomSlot);
        set => Set(PaddingBottomSlot, value, StyleFlags.PaddingBottomRelative);
    }

    /// <summary>Space inside the left edge; percentages use the parent's content width.</summary>
    public Size PaddingLeft
    {
        readonly get => Read(PaddingLeftSlot);
        set => Set(PaddingLeftSlot, value, StyleFlags.PaddingLeftRelative);
    }

    /// <summary>Whether the margins were set per side rather than uniformly.</summary>
    [UsedImplicitly]
    public bool HasSpecificMargins { get; set; }

    /// <summary>Horizontal placement of the children in the free space: percentage of it plus a pixel offset.</summary>
    public Size AlignContentHorizontal
    {
        readonly get => Read(AlignContentXSlot);
        set => Write(AlignContentXSlot, value);
    }

    /// <summary>Vertical placement of the children in the free space: percentage of it plus a pixel offset.</summary>
    public Size AlignContentVertical
    {
        readonly get => Read(AlignContentYSlot);
        set => Write(AlignContentYSlot, value);
    }

    /// <summary>How free space along the flow axis is distributed between children.</summary>
    public SpacingTypes Spacing { get; set; }

    /// <summary>The axis children flow along.</summary>
    public Axis Direction { get; set; }

    /// <summary>Whether a horizontal flow breaks children onto new lines when they overflow.</summary>
    public bool Wrap { get; set; }

    /// <summary>
    /// Whether this node is taken out of its parent's flow and placed at <see cref="AbsoluteX"/>,
    /// <see cref="AbsoluteY"/>. Set by <see cref="LayoutNode.Left"/>, <see cref="LayoutNode.Top"/>,
    /// <see cref="LayoutNode.Absolute"/> and <see cref="LayoutNode.AbsoluteScreen"/>.
    /// </summary>
    public bool IsAbsolute { get; set; }

    /// <summary>Horizontal offset of an absolute node in <see cref="AbsoluteOrigin"/> space.</summary>
    public Size AbsoluteX
    {
        readonly get => Read(AbsoluteXSlot);
        set => Write(AbsoluteXSlot, value);
    }

    /// <summary>Vertical offset of an absolute node in <see cref="AbsoluteOrigin"/> space.</summary>
    public Size AbsoluteY
    {
        readonly get => Read(AbsoluteYSlot);
        set => Write(AbsoluteYSlot, value);
    }

    /// <summary>The coordinate space the absolute offsets are expressed in.</summary>
    public AbsoluteOrigin AbsoluteOrigin { get; set; }

    /// <summary>
    /// Whether this node swallows pointer input for everything drawn beneath it, such as floating dock windows,
    /// popups and drag ghosts. See <see cref="LayoutNode.BlockInput"/>.
    /// </summary>
    public bool BlocksInput { get; set; }

    /// <summary>A style with auto size, no spacing, start alignment and vertical flow.</summary>
    public static LayoutStyle Default => new() { Direction = Axis.Vertical, AbsoluteOrigin = AbsoluteOrigin.Parent };

    /// <summary>Whether the width takes a share of the remaining space.</summary>
    public readonly bool ExpandsWidth => Has(StyleFlags.WidthRelative) && !IsZeroTerm(Expand(WidthSlot));

    /// <summary>Whether the height takes a share of the remaining space.</summary>
    public readonly bool ExpandsHeight => Has(StyleFlags.HeightRelative) && !IsZeroTerm(Expand(HeightSlot));

    /// <summary>The content alignment along <paramref name="direction"/>.</summary>
    /// <param name="direction">The layout direction.</param>
    public readonly Size GetAlignContentForDirection(Axis direction) =>
        direction == Axis.Horizontal ? AlignContentHorizontal : AlignContentVertical;

    /// <summary>The content alignment across <paramref name="direction"/>.</summary>
    /// <param name="direction">The layout direction.</param>
    public readonly Size GetCrossAxisAlignContent(Axis direction) =>
        direction == Axis.Horizontal ? AlignContentVertical : AlignContentHorizontal;

    readonly bool Has(StyleFlags flag) => (_flags & flag) != 0;

    readonly Size? Get(int slot, StyleFlags set) => Has(set) ? Read(slot) : null;

    void SetOptional(int slot, Size? value, StyleFlags set, StyleFlags relative)
    {
        Write(slot, value.GetValueOrDefault());
        _flags = value is { } size
            ? (_flags | set) & ~relative | (size.IsPixelsOnly ? StyleFlags.None : relative)
            : _flags & ~(set | relative);
    }

    void Set(int slot, Size value, StyleFlags relative)
    {
        Write(slot, value);
        _flags = value.IsPixelsOnly ? _flags & ~relative : _flags | relative;
    }
}
