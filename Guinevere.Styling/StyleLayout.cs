using System.Collections.Frozen;

namespace Guinevere;

/// <summary>
/// Applies the layout-affecting subset of a <see cref="ResolvedStyle"/> (flexbox, sizing,
/// spacing, alignment) onto a <see cref="LayoutNode"/> during the build pass. Visual properties
/// (colors, radius) are drawn by <c>gui.StyledNode</c>, not here.
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

        var text = new StyleTextLayout(node.Scope.Get<LayoutNodeScopeTextLayout>().Value);

        foreach (var (prop, raw) in style.DeclarationMap)
        {
            if (prop.Length == 0)
            {
                continue;
            }

            ApplyDeclaration(node, prop, raw.Trim(), ref text);
        }
        if (text.Changed)
        {
            node.Scope.Set(new LayoutNodeScopeTextLayout { Value = text.Options });
        }
    }

    static void ApplyDeclaration(LayoutNode node, string prop, string value, ref StyleTextLayout text)
    {
        if (!text.Apply(prop, value))
        {
            ApplyLayout(node, prop, value);
        }
    }

    /// <summary>Which layout group a property belongs to, by its first letter: 1 flow, 2 size, 3 min/max and margin.</summary>
    static readonly byte[] LayoutGroups = CreateLayoutGroups();

    static byte[] CreateLayoutGroups()
    {
        var groups = new byte[128];
        foreach (var letter in "fagj")
        {
            groups[letter] = 1;
        }

        foreach (var letter in "whp")
        {
            groups[letter] = 2;
        }

        groups['m'] = 3;
        return groups;
    }

    static void ApplyLayout(LayoutNode node, string prop, string value)
    {
        switch (LayoutGroups[prop[0] & 127])
        {
            case 1: ApplyFlow(node, prop, value); break;
            case 2: ApplySize(node, prop, value); break;
            case 3: ApplyM(node, prop, value); break;
        }
    }

    static void ApplyFlow(LayoutNode node, string prop, string value)
    {
        switch (prop)
        {
            case "flex-direction":
            case "flow-dir":
                node.Direction(value is "row" or "row-reverse" or "x" or "x-reverse"
                    ? Axis.Horizontal : Axis.Vertical);
                break;
            case "flex-wrap":
                if (value is "wrap" or "wrap-reverse")
                {
                    node.Wrap(0);
                }

                break;
            case "flex-grow":
                if (StyleValue.TryFloat(value, out var grow) && grow > 0f)
                {
                    node.Expand();
                }

                break;
            case "align-self": node.AlignSelf(AlignFraction(value)); break;
            case "align-items": node.ContentAlignX(AlignFraction(value)); break;
            case "justify-content": node.ContentAlignY(AlignFraction(value)); break;
            case "gap" when StyleValue.TryFloat(value, out var gap): node.Gap(gap); break;
        }
    }

    static void ApplySize(LayoutNode node, string prop, string value)
    {
        switch (prop)
        {
            case "width": ApplyLength(node, value, width: true); break;
            case "height": ApplyLength(node, value, width: false); break;
            case "padding": ApplyBox(node, value, padding: true); break;
        }
    }

    static void ApplyM(LayoutNode node, string prop, string value)
    {
        switch (prop)
        {
            case "margin": ApplyBox(node, value, padding: false); break;
            case "min-width":
                if (StyleValue.TryLength(value, out var minWidth, out _))
                {
                    node.MinWidth(minWidth);
                }

                break;
            case "max-width":
                if (StyleValue.TryLength(value, out var maxWidth, out _))
                {
                    node.MaxWidth(maxWidth);
                }

                break;
            case "min-height":
                if (StyleValue.TryLength(value, out var minHeight, out _))
                {
                    node.MinHeight(minHeight);
                }

                break;
            case "max-height":
                if (StyleValue.TryLength(value, out var maxHeight, out _))
                {
                    node.MaxHeight(maxHeight);
                }

                break;
        }
    }

    static void ApplyLength(LayoutNode node, string value, bool width)
    {
        if (!TryParseDimension(value, out var dimension))
        {
            return;
        }

        if (dimension.Unit == DimensionUnit.Expression)
        {
            if (width)
            {
                node.Width(dimension.Expression);
            }
            else
            {
                node.Height(dimension.Expression);
            }

            return;
        }

        if (width)
        {
            if (dimension.Unit == DimensionUnit.Percent)
            {
                node.WidthPercent(dimension.Length);
            }
            else
            {
                node.Width(dimension.Length);
            }
        }
        else
        {
            if (dimension.Unit == DimensionUnit.Percent)
            {
                node.HeightPercent(dimension.Length);
            }
            else
            {
                node.Height(dimension.Length);
            }
        }
    }

    enum DimensionUnit { Pixel, Percent, Expression }

    readonly record struct DimensionValue(DimensionUnit Unit, float Length = 0, UnitValue Expression = default);

    static bool TryParseDimension(string value, out DimensionValue dimension)
    {
        if (value == "expand")
        {
            dimension = new(DimensionUnit.Expression, Expression: UnitValue.Expand());
        }
        else if (value is "fit" or "auto")
        {
            dimension = new(DimensionUnit.Expression, Expression: UnitValue.Fit);
        }
        else if (value.StartsWith("ratio(", StringComparison.Ordinal) && value.EndsWith(')')
                 && StyleValue.TryFloat(value.AsSpan(6, value.Length - 7), out var ratio))
        {
            dimension = new(DimensionUnit.Expression, Expression: UnitValue.Ratio(ratio));
        }
        else if (StyleValue.TryLength(value, out var length, out var isPercent))
        {
            dimension = new(isPercent ? DimensionUnit.Percent : DimensionUnit.Pixel, length);
        }
        else
        {
            dimension = default;
            return false;
        }
        return true;
    }

    static void ApplyBox(LayoutNode node, string value, bool padding)
    {
        Span<float> lengths = stackalloc float[4];
        var count = ParseBox(value, lengths);
        if (padding)
        {
            switch (count)
            {
                case 1: node.Padding(lengths[0]); break;
                case 2: node.Padding(lengths[1], lengths[0]); break;
                case 4: node.Padding(lengths[0], lengths[1], lengths[2], lengths[3]); break;
            }
        }
        else
        {
            switch (count)
            {
                case 1: node.Margin(lengths[0]); break;
                case 2: node.Margin(lengths[1], lengths[0]); break;
                case 4: node.Margin(lengths[0], lengths[1], lengths[2], lengths[3]); break;
            }
        }
    }

    static int ParseBox(string value, Span<float> lengths)
    {
        var parts = value.AsSpan();
        var count = 0;
        while (!parts.IsEmpty)
        {
            parts = parts.TrimStart();
            if (parts.IsEmpty)
            {
                break;
            }

            if (count == lengths.Length)
            {
                return 0;
            }

            var end = parts.IndexOf(' ');
            var token = end < 0 ? parts : parts[..end];
            if (!StyleValue.TryLength(token, out lengths[count++], out _))
            {
                return 0;
            }

            parts = end < 0 ? [] : parts[(end + 1)..];
        }
        return count;
    }

    static readonly FrozenDictionary<string, float> AlignFractions = new Dictionary<string, float>
    {
        ["flex-start"] = 0f,
        ["start"] = 0f,
        ["left"] = 0f,
        ["top"] = 0f,
        ["center"] = 0.5f,
        ["middle"] = 0.5f,
        ["flex-end"] = 1f,
        ["end"] = 1f,
        ["right"] = 1f,
        ["bottom"] = 1f
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Maps a CSS alignment keyword to a fraction of the free space; unknown ones align to the start.</summary>
    internal static float AlignFraction(string value) => AlignFractions.GetValueOrDefault(value);
}
