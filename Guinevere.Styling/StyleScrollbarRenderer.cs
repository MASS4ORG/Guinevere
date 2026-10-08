namespace Guinevere;

/// <summary>Resolves scrollbar tracks and thumbs against the container's selectors and inherited tokens.</summary>
sealed class StyleScrollbarRenderer : IScrollbarRenderer
{
    internal static readonly StyleScrollbarRenderer Instance = new();
    static readonly string[] Horizontal = ["horizontal"];
    static readonly string[] Vertical = ["vertical"];

    /// <summary>Draws stylesheet boxes without adding layout nodes during rendering.</summary>
    public bool Draw(Gui gui, LayoutNode container, Axis axis, Rect track, Rect thumb, StyleState state)
    {
        var ancestors = new List<StyleTarget>();
        for (var node = container; node is not null; node = node.Parent)
            if (node.StyleTarget is { } target) ancestors.Add(target);
        var modifiers = axis == Axis.Vertical ? Vertical : Horizontal;
        var root = new StyleTarget("scrollbar", null, [], state, modifiers, ancestors);
        var tokens = container.Scope.Get<StyleTokens>();
        var style = gui.StyleSheets.Resolve(root, null, tokens);
        ancestors.Insert(0, root);
        var part = new StyleTarget("thumb", null, [], state, modifiers, ancestors);
        var thumbStyle = gui.StyleSheets.Resolve(part, null, tokens.With(style.Locals));
        if (style.DeclarationMap.Count == 0 && thumbStyle.DeclarationMap.Count == 0) return false;
        gui.DrawStyledBox(style, track);
        gui.DrawStyledBox(thumbStyle, thumb);
        return true;
    }
}
