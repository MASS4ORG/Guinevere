namespace Guinevere;

/// <summary>Four edge distances in pixels — used for nine-slice borders and similar.</summary>
/// <param name="Left">Left edge inset.</param>
/// <param name="Top">Top edge inset.</param>
/// <param name="Right">Right edge inset.</param>
/// <param name="Bottom">Bottom edge inset.</param>
public readonly record struct Insets(float Left, float Top, float Right, float Bottom)
{
    /// <summary>The same inset on all four edges.</summary>
    /// <param name="all">Inset applied to every edge.</param>
    public Insets(float all) : this(all, all, all, all) { }

    /// <summary>Horizontal inset on left/right, vertical inset on top/bottom.</summary>
    /// <param name="horizontal">Left and right inset.</param>
    /// <param name="vertical">Top and bottom inset.</param>
    public Insets(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    /// <summary>Sum of the left and right insets.</summary>
    public float Horizontal => Left + Right;

    /// <summary>Sum of the top and bottom insets.</summary>
    public float Vertical => Top + Bottom;
}
