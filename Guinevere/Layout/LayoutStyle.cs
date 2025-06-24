namespace Guinevere;

/// <summary>
/// Contains all layout styling properties for a LayoutNode.
/// This includes spacing, alignment, sizing, and layout direction.
/// </summary>
public struct LayoutStyle
{
    // Size properties
    /// <summary>
    /// Represents the width of the layout element. This property determines the horizontal size
    /// of the element, which can either be explicitly defined or dynamically calculated based
    /// on layout logic, constraints, or the size of its content within the parent container.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Specifies the height of the layout element. This property determines the vertical size
    /// of the element, which can be explicitly set or dynamically calculated based on layout rules,
    /// expansion parameters, or content size within the parent container.
    /// </summary>
    public float Height { get; set; }

    // Spacing properties

    /// <summary>
    /// Specifies the spacing between adjacent child elements within a layout. This property
    /// determines the fixed amount of gap or separation to be applied between individual
    /// children, contributing to the overall structure and appearance of the layout.
    /// </summary>
    public float Gap { get; set; }

    /// <summary>
    /// Specifies the margin spacing at the top of the layout element. This property defines the space
    /// between the top edge of the element and its boundary or surrounding content, contributing to
    /// the overall spacing configuration within the layout.
    /// </summary>
    public float MarginTop { get; set; }

    /// <summary>
    /// Represents the margin on the right side of the layout element. This property defines the spacing
    /// between the right edge of the element and its adjacent content or container boundary.
    /// It can be explicitly set or influenced by overall layout rules and margin configuration.
    /// </summary>
    public float MarginRight { get; set; }

    /// <summary>
    /// Represents the bottom margin of the layout element. This property defines the spacing
    /// between the bottom edge of the element and adjacent elements or its container.
    /// The value may be applied specifically or derived from a general margin setting
    /// depending on the layout configuration.
    /// </summary>
    public float MarginBottom { get; set; }

    /// <summary>
    /// Represents the left margin of the layout element. This property defines the space between
    /// the left edge of the element and its containing or neighboring elements, enabling fine-grained
    /// control over horizontal positioning and spacing within the layout.
    /// </summary>
    public float MarginLeft { get; set; }

    /// <summary>
    /// Represents the top padding of a layout element. This property defines
    /// the spacing between the content of the element and its top border,
    /// affecting how the content is positioned within the element.
    /// </summary>
    public float PaddingTop { get; set; }

    /// <summary>
    /// Represents the padding on the right side of a layout element. This property
    /// defines the spacing between the content of the element and its right boundary,
    /// allowing for precise adjustment of the element's internal layout.
    /// </summary>
    public float PaddingRight { get; set; }

    /// <summary>
    /// Specifies the bottom padding of the layout element. This property defines the space between
    /// the content of the element and its bottom boundary, which can affect overall layout spacing
    /// and alignment within a container or parent element.
    /// </summary>
    public float PaddingBottom { get; set; }

    /// <summary>
    /// Specifies the padding on the left side of a layout element. This property determines
    /// the amount of space between the element's left edge and its content, adding spacing
    /// within the element's boundaries and affecting the overall layout behavior.
    /// </summary>
    public float PaddingLeft { get; set; }

    /// <summary>
    /// Indicates whether the layout has specific margin values defined for each side.
    /// When set to true, values for MarginTop, MarginRight, MarginBottom, and MarginLeft
    /// are utilized individually. When set to false, the general Margin value is applied
    /// uniformly to all sides.
    /// </summary>
    public bool HasSpecificMargins { get; set; }

    // Alignment properties

    /// <summary>
    /// Determines how content is aligned along the horizontal axis within the layout container.
    /// This property defines the horizontal positioning of child elements relative to the available
    /// horizontal space when the parent's layout rules are applied.
    /// </summary>
    public float AlignContentHorizontal { get; set; }

    /// <summary>
    /// Specifies the alignment of content along the vertical axis within the layout container.
    /// This property determines how child elements are positioned vertically within the
    /// available space when the parent's layout configuration is applied.
    /// </summary>
    public float AlignContentVertical { get; set; }

    /// <summary>
    /// Defines the alignment of the layout element along the cross-axis with respect
    /// to its parent. This property determines how the element positions itself
    /// within the available space when its parent's layout configuration is applied.
    /// </summary>
    public float AlignSelf { get; set; }

    // Expansion properties

    /// <summary>
    /// Determines whether the layout element is set to expand by adapting its size
    /// dynamically within its container. When enabled, the element's dimensions
    /// are proportionally adjusted based on the available space and its expand
    /// percentage settings.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Specifies whether the width of a layout element should be expandable,
    /// allowing it to adapt dynamically based on the available horizontal space.
    /// When enabled, the width is scaled proportionally using the value defined by
    /// <c>ExpandWidthPercentage</c>, relative to other expandable elements within
    /// the same container.
    /// </summary>
    public bool ExpandWidth { get; set; }

    /// <summary>
    /// Determines whether the height of a layout element should be expandable,
    /// allowing it to adjust dynamically based on the available vertical space.
    /// When enabled, the height is scaled proportionally to the value specified
    /// by <c>ExpandHeightPercentage</c>, relative to other expandable elements
    /// within the container.
    /// </summary>
    public bool ExpandHeight { get; set; }

    /// <summary>
    /// Represents the percentage of the available width that a layout element should occupy when its width is expandable.
    /// This value is utilized alongside <c>ExpandWidth</c> or <c>IsExpanded</c> to allocate horizontal space proportionally
    /// among elements within a container.
    /// </summary>
    public float ExpandWidthPercentage { get; set; }

    /// <summary>
    /// Represents the percentage of the available height that a layout element should occupy when its height is expandable.
    /// This value is used in conjunction with <c>ExpandHeight</c> or <c>IsExpanded</c> to proportionally allocate
    /// vertical space among elements within a container.
    /// </summary>
    public float ExpandHeightPercentage { get; set; }

    // Layout direction///

    /// <summary>
    /// Specifies the primary axis along which child elements are laid out within a layout container.
    /// The direction can be set to horizontal or vertical, influencing the arrangement and flow of child elements.
    /// </summary>
    public Axis Direction { get; set; }

    /// <summary>
    /// Determines whether child elements are wrapped to the next row or column when they exceed the allocated space in the current layout.
    /// </summary>
    public bool Wrap { get; set; }

    /// <summary>
    /// Creates a new LayoutStyle with default values
    /// </summary>
    public static LayoutStyle Default => new()
    {
        Width = -1f,
        Height = -1f,
        Gap = 0f,
        MarginTop = 0f,
        MarginRight = 0f,
        MarginBottom = 0f,
        MarginLeft = 0f,
        PaddingTop = 0f,
        PaddingRight = 0f,
        PaddingBottom = 0f,
        PaddingLeft = 0f,
        HasSpecificMargins = false,
        AlignContentHorizontal = 0.0f,
        AlignContentVertical = 0.0f,
        AlignSelf = 0.5f,
        IsExpanded = false,
        ExpandWidth = false,
        ExpandHeight = false,
        ExpandWidthPercentage = 1.0f,
        ExpandHeightPercentage = 1.0f,
        Direction = Axis.Vertical,
        Wrap = false
    };

    /// <summary>
    /// Gets the alignment content value for the current direction
    /// </summary>
    /// <param name="direction">The layout direction</param>
    /// <returns>The appropriate alignment value</returns>
    public readonly float GetAlignContentForDirection(Axis direction)
    {
        return direction == Axis.Horizontal ? AlignContentHorizontal : AlignContentVertical;
    }

    /// <summary>
    /// Gets the cross-axis alignment content value for the current direction
    /// </summary>
    /// <param name="direction">The layout direction</param>
    /// <returns>The appropriate cross-axis alignment value</returns>
    public readonly float GetCrossAxisAlignContent(Axis direction)
    {
        return direction == Axis.Horizontal ? AlignContentVertical : AlignContentHorizontal;
    }
}
