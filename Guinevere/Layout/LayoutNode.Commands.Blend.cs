namespace Guinevere;

/// <summary>
/// Additive and interpolating forms of the sizing and spacing commands. <c>Add</c> sums weighted contributions, so a
/// container can blend several sources, such as one <see cref="Size.PercentOf"/> per tab; <c>Lerp</c> moves the
/// current value toward a target by a fraction. An unset size or bound starts from zero.
/// </summary>
public partial class LayoutNode
{
    /// <summary>Adds a weighted contribution to the width.</summary>
    /// <param name="value">The contribution.</param>
    /// <param name="weight">Its weight.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddWidth(Size value, float weight = 1f) => Width((Style.Width ?? default) + value * weight);

    /// <summary>Adds a weighted contribution to the height.</summary>
    /// <param name="value">The contribution.</param>
    /// <param name="weight">Its weight.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddHeight(Size value, float weight = 1f) => Height((Style.Height ?? default) + value * weight);

    /// <summary>Moves the width toward a target; an unset width starts from its content size.</summary>
    /// <param name="target">The target width.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpWidth(Size target, float amount) =>
        Width(Guinevere.Size.Lerp(Style.Width ?? Guinevere.Size.Fit, target, amount));

    /// <summary>Moves the height toward a target; an unset height starts from its content size.</summary>
    /// <param name="target">The target height.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpHeight(Size target, float amount) =>
        Height(Guinevere.Size.Lerp(Style.Height ?? Guinevere.Size.Fit, target, amount));

    /// <summary>Adds to the minimum width; an unset bound starts at zero.</summary>
    /// <param name="value">The contribution.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddMinWidth(Size value) => MinWidth((Style.MinWidth ?? default) + value);

    /// <summary>Adds to the maximum width; an unset bound starts at zero.</summary>
    /// <param name="value">The contribution.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddMaxWidth(Size value) => MaxWidth((Style.MaxWidth ?? default) + value);

    /// <summary>Adds to the minimum height; an unset bound starts at zero.</summary>
    /// <param name="value">The contribution.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddMinHeight(Size value) => MinHeight((Style.MinHeight ?? default) + value);

    /// <summary>Adds to the maximum height; an unset bound starts at zero.</summary>
    /// <param name="value">The contribution.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddMaxHeight(Size value) => MaxHeight((Style.MaxHeight ?? default) + value);

    /// <summary>Moves the minimum width toward a target.</summary>
    /// <param name="target">The target bound.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpMinWidth(Size target, float amount) =>
        MinWidth(Guinevere.Size.Lerp(Style.MinWidth ?? default, target, amount));

    /// <summary>Moves the maximum width toward a target.</summary>
    /// <param name="target">The target bound.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpMaxWidth(Size target, float amount) =>
        MaxWidth(Guinevere.Size.Lerp(Style.MaxWidth ?? default, target, amount));

    /// <summary>Moves the minimum height toward a target.</summary>
    /// <param name="target">The target bound.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpMinHeight(Size target, float amount) =>
        MinHeight(Guinevere.Size.Lerp(Style.MinHeight ?? default, target, amount));

    /// <summary>Moves the maximum height toward a target.</summary>
    /// <param name="target">The target bound.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpMaxHeight(Size target, float amount) =>
        MaxHeight(Guinevere.Size.Lerp(Style.MaxHeight ?? default, target, amount));

    /// <summary>Adds a weighted contribution to the gap.</summary>
    /// <param name="value">The contribution.</param>
    /// <param name="weight">Its weight.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddGap(Size value, float weight = 1f) => Gap(Style.Gap + value * weight);

    /// <summary>Moves the gap toward a target.</summary>
    /// <param name="target">The target gap.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpGap(Size target, float amount) => Gap(Guinevere.Size.Lerp(Style.Gap, target, amount));

    /// <summary>Adds a weighted contribution to every padding side.</summary>
    /// <param name="value">The contribution.</param>
    /// <param name="weight">Its weight.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddPadding(Size value, float weight = 1f)
    {
        var add = value * weight;
        return SetPadding(Style.PaddingTop + add, Style.PaddingRight + add, Style.PaddingBottom + add,
            Style.PaddingLeft + add);
    }

    /// <summary>Moves every padding side toward a target.</summary>
    /// <param name="target">The target padding.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpPadding(Size target, float amount) => SetPadding(
        Guinevere.Size.Lerp(Style.PaddingTop, target, amount), Guinevere.Size.Lerp(Style.PaddingRight, target, amount),
        Guinevere.Size.Lerp(Style.PaddingBottom, target, amount),
        Guinevere.Size.Lerp(Style.PaddingLeft, target, amount));

    /// <summary>Adds a weighted contribution to every margin side.</summary>
    /// <param name="value">The contribution.</param>
    /// <param name="weight">Its weight.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AddMargin(Size value, float weight = 1f)
    {
        var add = value * weight;
        return SetMargins(Style.MarginTop + add, Style.MarginRight + add, Style.MarginBottom + add,
            Style.MarginLeft + add);
    }

    /// <summary>Moves every margin side toward a target.</summary>
    /// <param name="target">The target margin.</param>
    /// <param name="amount">The fraction of the way, where 1 reaches the target.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode LerpMargin(Size target, float amount) => SetMargins(
        Guinevere.Size.Lerp(Style.MarginTop, target, amount), Guinevere.Size.Lerp(Style.MarginRight, target, amount),
        Guinevere.Size.Lerp(Style.MarginBottom, target, amount),
        Guinevere.Size.Lerp(Style.MarginLeft, target, amount));
}
