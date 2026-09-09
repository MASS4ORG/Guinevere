namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Active <c>.uss</c> stylesheets, lowest priority first. Add sheets before the frame; every
    /// <see cref="StyledNode"/> resolves against them.
    /// </summary>
    public List<StyleSheet> StyleSheets { get; } = [];

    /// <summary>
    /// Creates a layout node and styles it from <see cref="StyleSheets"/> by its type, classes and
    /// id. Layout declarations are applied to the node in the build pass; <c>background-color</c>,
    /// <c>border-radius</c>, <c>border-color</c> and <c>border-width</c> are drawn behind the node's
    /// children in the render pass, re-resolved for <c>:hover</c> / <c>:active</c> / <c>:focus</c>.
    /// </summary>
    /// <param name="type">Element type name matched by a bare-type selector, or <c>null</c>.</param>
    /// <param name="classes">Class names matched by <c>.class</c> selectors.</param>
    /// <param name="id">Element id matched by an <c>#id</c> selector, or <c>null</c>.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The layout node, ready to <c>.Enter()</c>.</returns>
    public LayoutNode StyledNode(
        string? type = null,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
    {
        var node = Node(-1, -1, id, filePath, lineNumber);
        if (StyleSheets.Count == 0) return node;

        var target = new StyleTarget(type, id, classes ?? [], StyleState.None);

        if (Pass == Pass.Pass1Build)
        {
            StyleLayout.Apply(node, StyleResolver.Resolve(StyleSheets, target));
            return node;
        }

        var state = StyleState.None;
        var interactable = GetInteractable(node);
        if (interactable.OnHover()) state |= StyleState.Hover;
        if (interactable.OnHold()) state |= StyleState.Active;

        var resolved = state == StyleState.None
            ? StyleResolver.Resolve(StyleSheets, target)
            : StyleResolver.Resolve(StyleSheets, target with { State = state });

        DrawStyledBox(node, resolved);
        return node;
    }

    /// <summary>
    /// Resolves the effective <c>.uss</c> style for an element with the given type, classes and id
    /// against <see cref="StyleSheets"/>, without creating a node. Callers that draw their own
    /// control (buttons, text) use this to read declarations like <c>width</c> or <c>color</c>.
    /// </summary>
    /// <param name="type">Element type name, or <c>null</c>.</param>
    /// <param name="classes">Class names.</param>
    /// <param name="id">Element id, or <c>null</c>.</param>
    /// <param name="state">Interaction state to resolve for.</param>
    public ResolvedStyle ResolveStyle(
        string? type = null,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        StyleState state = StyleState.None) =>
        StyleSheets.Count == 0
            ? ResolvedStyle.Empty
            : StyleResolver.Resolve(StyleSheets, new StyleTarget(type, id, classes ?? [], state));

    private static void DrawStyledBox(LayoutNode node, ResolvedStyle style)
    {
        var background = style.GetColor("background-color");
        var borderColor = style.GetColor("border-color");
        var borderWidth = style.GetLength("border-width") ?? 0f;
        if (background is null && (borderColor is null || borderWidth <= 0f)) return;

        var radius = style.GetLength("border-radius") ?? 0f;
        var r = node.Rect;

        if (background is { } bg)
        {
            var fill = radius > 0f
                ? Shape.RoundRect(r.X, r.Y, r.X + r.W, r.Y + r.H, radius)
                : Shape.Rect(r.X, r.Y, r.X + r.W, r.Y + r.H);
            fill.SolidColor(bg);
            node.DrawList.Add(fill);
        }

        if (borderColor is { } bc && borderWidth > 0f)
        {
            var stroke = radius > 0f
                ? Shape.RoundRect(r.X, r.Y, r.X + r.W, r.Y + r.H, radius)
                : Shape.Rect(r.X, r.Y, r.X + r.W, r.Y + r.H);
            stroke.Stroke(bc, borderWidth);
            node.DrawList.Add(stroke);
        }
    }
}
