namespace Guinevere;

/// <summary>
/// The layout properties of a <see cref="LayoutNode"/>. Every size, constraint, spacing and offset is a
/// <see cref="Size"/>, so any of them can mix pixels, percentages, ratios, expansion and node references.
/// </summary>
/// <remarks>
/// Setters keep a mask of which values are set and which need a basis, so layout can test the common pixel-only
/// case with one read instead of copying each <see cref="Size"/>.
/// </remarks>
public partial struct LayoutStyle
{
    Size _width, _height, _minWidth, _maxWidth, _minHeight, _maxHeight, _alignSelf;
    Size _gap, _marginTop, _marginRight, _marginBottom, _marginLeft;
    Size _paddingTop, _paddingRight, _paddingBottom, _paddingLeft;
    StyleFlags _flags;

    /// <summary>The width; null sizes to content along the parent's flow and stretches across it.</summary>
    public Size? Width
    {
        readonly get => Get(_width, StyleFlags.WidthSet);
        set => SetOptional(ref _width, value, StyleFlags.WidthSet, StyleFlags.WidthRelative);
    }

    /// <summary>The height; null sizes to content along the parent's flow and stretches across it.</summary>
    public Size? Height
    {
        readonly get => Get(_height, StyleFlags.HeightSet);
        set => SetOptional(ref _height, value, StyleFlags.HeightSet, StyleFlags.HeightRelative);
    }

    /// <summary>Lower bound on the resolved width, or null for none.</summary>
    public Size? MinWidth
    {
        readonly get => Get(_minWidth, StyleFlags.MinWidthSet);
        set => SetOptional(ref _minWidth, value, StyleFlags.MinWidthSet, StyleFlags.MinWidthRelative);
    }

    /// <summary>Upper bound on the resolved width, or null for none.</summary>
    public Size? MaxWidth
    {
        readonly get => Get(_maxWidth, StyleFlags.MaxWidthSet);
        set => SetOptional(ref _maxWidth, value, StyleFlags.MaxWidthSet, StyleFlags.MaxWidthRelative);
    }

    /// <summary>Lower bound on the resolved height, or null for none.</summary>
    public Size? MinHeight
    {
        readonly get => Get(_minHeight, StyleFlags.MinHeightSet);
        set => SetOptional(ref _minHeight, value, StyleFlags.MinHeightSet, StyleFlags.MinHeightRelative);
    }

    /// <summary>Upper bound on the resolved height, or null for none.</summary>
    public Size? MaxHeight
    {
        readonly get => Get(_maxHeight, StyleFlags.MaxHeightSet);
        set => SetOptional(ref _maxHeight, value, StyleFlags.MaxHeightSet, StyleFlags.MaxHeightRelative);
    }

    /// <summary>This node's placement across its parent's flow, overriding the parent's content alignment.</summary>
    public Size? AlignSelf
    {
        readonly get => Get(_alignSelf, StyleFlags.AlignSelfSet);
        set => SetOptional(ref _alignSelf, value, StyleFlags.AlignSelfSet, StyleFlags.None);
    }

    /// <summary>Space between adjacent children; percentages use this node's content box on the flow axis.</summary>
    public Size Gap
    {
        readonly get => _gap;
        set => Set(ref _gap, value, StyleFlags.GapRelative);
    }

    /// <summary>Space above the node; percentages use the parent's content height.</summary>
    public Size MarginTop
    {
        readonly get => _marginTop;
        set => Set(ref _marginTop, value, StyleFlags.MarginTopRelative);
    }

    /// <summary>Space right of the node; percentages use the parent's content width.</summary>
    public Size MarginRight
    {
        readonly get => _marginRight;
        set => Set(ref _marginRight, value, StyleFlags.MarginRightRelative);
    }

    /// <summary>Space below the node; percentages use the parent's content height.</summary>
    public Size MarginBottom
    {
        readonly get => _marginBottom;
        set => Set(ref _marginBottom, value, StyleFlags.MarginBottomRelative);
    }

    /// <summary>Space left of the node; percentages use the parent's content width.</summary>
    public Size MarginLeft
    {
        readonly get => _marginLeft;
        set => Set(ref _marginLeft, value, StyleFlags.MarginLeftRelative);
    }

    /// <summary>Space inside the top edge; percentages use the parent's content height.</summary>
    public Size PaddingTop
    {
        readonly get => _paddingTop;
        set => Set(ref _paddingTop, value, StyleFlags.PaddingTopRelative);
    }

    /// <summary>Space inside the right edge; percentages use the parent's content width.</summary>
    public Size PaddingRight
    {
        readonly get => _paddingRight;
        set => Set(ref _paddingRight, value, StyleFlags.PaddingRightRelative);
    }

    /// <summary>Space inside the bottom edge; percentages use the parent's content height.</summary>
    public Size PaddingBottom
    {
        readonly get => _paddingBottom;
        set => Set(ref _paddingBottom, value, StyleFlags.PaddingBottomRelative);
    }

    /// <summary>Space inside the left edge; percentages use the parent's content width.</summary>
    public Size PaddingLeft
    {
        readonly get => _paddingLeft;
        set => Set(ref _paddingLeft, value, StyleFlags.PaddingLeftRelative);
    }

    /// <summary>Whether the margins were set per side rather than uniformly.</summary>
    [UsedImplicitly]
    public bool HasSpecificMargins { get; set; }

    /// <summary>Horizontal placement of the children in the free space: percentage of it plus a pixel offset.</summary>
    public Size AlignContentHorizontal { get; set; }

    /// <summary>Vertical placement of the children in the free space: percentage of it plus a pixel offset.</summary>
    public Size AlignContentVertical { get; set; }

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
    public Size AbsoluteX { get; set; }

    /// <summary>Vertical offset of an absolute node in <see cref="AbsoluteOrigin"/> space.</summary>
    public Size AbsoluteY { get; set; }

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
    public readonly bool ExpandsWidth => Has(StyleFlags.WidthRelative) && _width.ExpandContribution != 0f;

    /// <summary>Whether the height takes a share of the remaining space.</summary>
    public readonly bool ExpandsHeight => Has(StyleFlags.HeightRelative) && _height.ExpandContribution != 0f;

    /// <summary>The content alignment along <paramref name="direction"/>.</summary>
    /// <param name="direction">The layout direction.</param>
    public readonly Size GetAlignContentForDirection(Axis direction) =>
        direction == Axis.Horizontal ? AlignContentHorizontal : AlignContentVertical;

    /// <summary>The content alignment across <paramref name="direction"/>.</summary>
    /// <param name="direction">The layout direction.</param>
    public readonly Size GetCrossAxisAlignContent(Axis direction) =>
        direction == Axis.Horizontal ? AlignContentVertical : AlignContentHorizontal;

    readonly bool Has(StyleFlags flag) => (_flags & flag) != 0;

    readonly Size? Get(in Size value, StyleFlags set) => Has(set) ? value : null;

    void SetOptional(ref Size field, Size? value, StyleFlags set, StyleFlags relative)
    {
        field = value.GetValueOrDefault();
        _flags = value is { } size
            ? (_flags | set) & ~relative | (size.IsPixelsOnly ? StyleFlags.None : relative)
            : _flags & ~(set | relative);
    }

    void Set(ref Size field, Size value, StyleFlags relative)
    {
        field = value;
        _flags = value.IsPixelsOnly ? _flags & ~relative : _flags | relative;
    }
}
