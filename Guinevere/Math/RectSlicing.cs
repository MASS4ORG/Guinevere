namespace Guinevere;

/// <summary>
/// Slices, insets and offsets of a <see cref="Rect"/> for drawing inside a laid-out node. Every distance is a
/// <see cref="Size"/>: plain numbers are pixels, percentages use the rectangle's extent on the same axis and ratios
/// its other extent, so <c>rect.AlignLeft(Size.Percentage(0.5f))</c> is the left half.
/// </summary>
public static class RectSlicing
{
    /// <summary>The strip of <paramref name="width"/> at the left edge.</summary>
    /// <param name="rect">The rectangle to slice.</param>
    /// <param name="width">The strip width.</param>
    public static Rect AlignLeft(this Rect rect, Size width) => rect with { Width = ResolveX(rect, width) };

    /// <summary>The strip of <paramref name="width"/> at the right edge.</summary>
    /// <param name="rect">The rectangle to slice.</param>
    /// <param name="width">The strip width.</param>
    public static Rect AlignRight(this Rect rect, Size width)
    {
        var w = ResolveX(rect, width);
        return new Rect(rect.X + rect.W - w, rect.Y, w, rect.H);
    }

    /// <summary>The strip of <paramref name="width"/> centered horizontally.</summary>
    /// <param name="rect">The rectangle to slice.</param>
    /// <param name="width">The strip width.</param>
    public static Rect AlignCenterX(this Rect rect, Size width)
    {
        var w = ResolveX(rect, width);
        return new Rect(rect.X + (rect.W - w) * 0.5f, rect.Y, w, rect.H);
    }

    /// <summary>The strip of <paramref name="height"/> at the top edge.</summary>
    /// <param name="rect">The rectangle to slice.</param>
    /// <param name="height">The strip height.</param>
    public static Rect AlignTop(this Rect rect, Size height) => rect with { Height = ResolveY(rect, height) };

    /// <summary>The strip of <paramref name="height"/> at the bottom edge.</summary>
    /// <param name="rect">The rectangle to slice.</param>
    /// <param name="height">The strip height.</param>
    public static Rect AlignBottom(this Rect rect, Size height)
    {
        var h = ResolveY(rect, height);
        return new Rect(rect.X, rect.Y + rect.H - h, rect.W, h);
    }

    /// <summary>The strip of <paramref name="height"/> centered vertically.</summary>
    /// <param name="rect">The rectangle to slice.</param>
    /// <param name="height">The strip height.</param>
    public static Rect AlignCenterY(this Rect rect, Size height)
    {
        var h = ResolveY(rect, height);
        return new Rect(rect.X, rect.Y + (rect.H - h) * 0.5f, rect.W, h);
    }

    /// <summary>Insets every side by the same distance.</summary>
    /// <param name="rect">The rectangle to inset.</param>
    /// <param name="all">The inset; a negative value grows the rectangle.</param>
    public static Rect Padding(this Rect rect, Size all) => rect.Padding(all, all);

    /// <summary>Insets left and right by <paramref name="x"/>, top and bottom by <paramref name="y"/>.</summary>
    /// <param name="rect">The rectangle to inset.</param>
    /// <param name="x">The horizontal inset.</param>
    /// <param name="y">The vertical inset.</param>
    public static Rect Padding(this Rect rect, Size x, Size y)
    {
        var dx = ResolveX(rect, x);
        var dy = ResolveY(rect, y);
        return new Rect(rect.X + dx, rect.Y + dy, rect.W - 2f * dx, rect.H - 2f * dy);
    }

    /// <summary>Insets the left and right sides.</summary>
    /// <param name="rect">The rectangle to inset.</param>
    /// <param name="x">The horizontal inset.</param>
    public static Rect HPadding(this Rect rect, Size x) => rect.Padding(x, 0f);

    /// <summary>Insets the top and bottom sides.</summary>
    /// <param name="rect">The rectangle to inset.</param>
    /// <param name="y">The vertical inset.</param>
    public static Rect VPadding(this Rect rect, Size y) => rect.Padding(0f, y);

    /// <summary>Grows every side by the same distance.</summary>
    /// <param name="rect">The rectangle to grow.</param>
    /// <param name="all">The growth; a negative value shrinks the rectangle.</param>
    public static Rect Expand(this Rect rect, Size all) => rect.Padding(-all);

    /// <summary>Grows left and right by <paramref name="x"/>, top and bottom by <paramref name="y"/>.</summary>
    /// <param name="rect">The rectangle to grow.</param>
    /// <param name="x">The horizontal growth.</param>
    /// <param name="y">The vertical growth.</param>
    public static Rect Expand(this Rect rect, Size x, Size y) => rect.Padding(-x, -y);

    /// <summary>Moves the rectangle horizontally.</summary>
    /// <param name="rect">The rectangle to move.</param>
    /// <param name="x">The offset.</param>
    public static Rect AddX(this Rect rect, Size x) => rect with { X = rect.X + ResolveX(rect, x) };

    /// <summary>Moves the rectangle vertically.</summary>
    /// <param name="rect">The rectangle to move.</param>
    /// <param name="y">The offset.</param>
    public static Rect AddY(this Rect rect, Size y) => rect with { Y = rect.Y + ResolveY(rect, y) };

    /// <summary>Grows the rectangle to the right.</summary>
    /// <param name="rect">The rectangle to grow.</param>
    /// <param name="width">The added width.</param>
    public static Rect AddWidth(this Rect rect, Size width) => rect with { Width = rect.W + ResolveX(rect, width) };

    /// <summary>Grows the rectangle downward.</summary>
    /// <param name="rect">The rectangle to grow.</param>
    /// <param name="height">The added height.</param>
    public static Rect AddHeight(this Rect rect, Size height) => rect with { Height = rect.H + ResolveY(rect, height) };

    /// <summary>Places the left edge, keeping the size.</summary>
    /// <param name="rect">The rectangle to move.</param>
    /// <param name="x">The new left edge.</param>
    public static Rect SetX(this Rect rect, float x) => rect with { X = x };

    /// <summary>Places the top edge, keeping the size.</summary>
    /// <param name="rect">The rectangle to move.</param>
    /// <param name="y">The new top edge.</param>
    public static Rect SetY(this Rect rect, float y) => rect with { Y = y };

    /// <summary>Sets the width, keeping the left edge.</summary>
    /// <param name="rect">The rectangle to resize.</param>
    /// <param name="width">The new width; percentages use the current width.</param>
    public static Rect SetWidth(this Rect rect, Size width) => rect with { Width = ResolveX(rect, width) };

    /// <summary>Sets the height, keeping the top edge.</summary>
    /// <param name="rect">The rectangle to resize.</param>
    /// <param name="height">The new height; percentages use the current height.</param>
    public static Rect SetHeight(this Rect rect, Size height) => rect with { Height = ResolveY(rect, height) };

    static float ResolveX(Rect rect, Size value) => value.Resolve(rect.W, rect.H, horizontal: true);

    static float ResolveY(Rect rect, Size value) => value.Resolve(rect.H, rect.W, horizontal: false);
}
