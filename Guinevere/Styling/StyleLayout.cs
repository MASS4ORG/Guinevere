namespace Guinevere;

/// <summary>
/// Applies the layout-affecting subset of a <see cref="ResolvedStyle"/> (flexbox, sizing,
/// spacing, alignment) onto a <see cref="LayoutNode"/> during the build pass. Visual properties
/// (colours, radius) are drawn by <see cref="Gui.StyledNode"/>, not here.
/// </summary>
public static class StyleLayout
{
    /// <summary>Applies every recognised declaration in <paramref name="style"/> to <paramref name="node"/>.</summary>
    /// <param name="node">The layout node being configured (build pass).</param>
    /// <param name="style">The resolved style.</param>
    public static void Apply(LayoutNode node, ResolvedStyle style)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(style);

        foreach (var (prop, raw) in style.Declarations)
        {
            var value = raw.Trim();
            switch (prop)
            {
                case "flex-direction":
                    node.Direction(value is "row" or "row-reverse" ? Axis.Horizontal : Axis.Vertical);
                    break;
                case "gap":
                    if (StyleValue.TryFloat(value, out var gap)) node.Gap(gap);
                    break;
                case "flex-grow":
                    if (StyleValue.TryFloat(value, out var grow) && grow > 0f) node.Expand();
                    break;
                case "align-self":
                    node.AlignSelf(AlignFraction(value));
                    break;
                case "align-items":
                    node.ContentAlignX(AlignFraction(value));
                    break;
                case "justify-content":
                    node.ContentAlignY(AlignFraction(value));
                    break;
                case "width":
                    ApplyLength(value, node.Width, node.WidthPercent);
                    break;
                case "height":
                    ApplyLength(value, node.Height, node.HeightPercent);
                    break;
                case "min-width":
                    if (StyleValue.TryLength(value, out var mnw, out _)) node.MinWidth(mnw);
                    break;
                case "max-width":
                    if (StyleValue.TryLength(value, out var mxw, out _)) node.MaxWidth(mxw);
                    break;
                case "min-height":
                    if (StyleValue.TryLength(value, out var mnh, out _)) node.MinHeight(mnh);
                    break;
                case "max-height":
                    if (StyleValue.TryLength(value, out var mxh, out _)) node.MaxHeight(mxh);
                    break;
                case "padding":
                    ApplyBox(value, node.Padding, node.Padding, node.Padding);
                    break;
                case "margin":
                    ApplyBox(value, node.Margin, node.Margin, node.Margin);
                    break;
                default:
                    break;
            }
        }
    }

    private static void ApplyLength(string value, Func<float, LayoutNode> px, Func<float, LayoutNode> percent)
    {
        if (!StyleValue.TryLength(value, out var v, out var isPercent)) return;
        if (isPercent) percent(v);
        else px(v);
    }

    private static void ApplyBox(
        string value,
        Func<float, LayoutNode> all,
        Func<float, float, LayoutNode> hv,
        Func<float, float, float, float, LayoutNode> tblr)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var n = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!StyleValue.TryLength(parts[i], out n[i], out _))
                return;

        switch (parts.Length)
        {
            case 1: all(n[0]); break;
            case 2: hv(n[1], n[0]); break; // CSS "vertical horizontal" → Guinevere (horizontal, vertical)
            case 4: tblr(n[0], n[1], n[2], n[3]); break;
            default: break;
        }
    }

    private static float AlignFraction(string value) => value switch
    {
        "flex-start" or "start" or "left" or "top" => 0f,
        "center" or "middle" => 0.5f,
        "flex-end" or "end" or "right" or "bottom" => 1f,
        _ => 0f,
    };
}
