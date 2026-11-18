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
            case "align-items": node.AlignContentX(AlignFraction(value)); break;
            case "justify-content": node.AlignContentY(AlignFraction(value)); break;
            case "gap" when StyleValue.TrySize(value, out var gap): node.Gap(gap); break;
        }
    }

    static void ApplySize(LayoutNode node, string prop, string value)
    {
        switch (prop)
        {
            case "width" when StyleValue.TrySize(value, out var width): node.Width(width); break;
            case "height" when StyleValue.TrySize(value, out var height): node.Height(height); break;
            case "padding": ApplyBox(node, value, padding: true); break;
        }
    }

    static void ApplyM(LayoutNode node, string prop, string value)
    {
        if (prop == "margin")
        {
            ApplyBox(node, value, padding: false);
            return;
        }

        if (!StyleValue.TrySize(value, out var size))
        {
            return;
        }

        switch (prop)
        {
            case "min-width": node.MinWidth(size); break;
            case "max-width": node.MaxWidth(size); break;
            case "min-height": node.MinHeight(size); break;
            case "max-height": node.MaxHeight(size); break;
        }
    }

    /// <summary>The up-to-four sides of a box shorthand, without allocating.</summary>
    [System.Runtime.CompilerServices.InlineArray(4)]
    struct BoxSides
    {
        Size _first;
    }

    static void ApplyBox(LayoutNode node, string value, bool padding)
    {
        var buffer = new BoxSides();
        Span<Size> sides = buffer;
        var count = StyleValue.ReadSizes(value, sides);
        if (padding)
        {
            switch (count)
            {
                case 1: node.Padding(sides[0]); break;
                case 2: node.Padding(sides[1], sides[0]); break;
                case 4: node.Padding(sides[0], sides[1], sides[2], sides[3]); break;
            }
        }
        else
        {
            switch (count)
            {
                case 1: node.Margin(sides[0]); break;
                case 2: node.Margin(sides[1], sides[0]); break;
                case 4: node.Margin(sides[0], sides[1], sides[2], sides[3]); break;
            }
        }
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
