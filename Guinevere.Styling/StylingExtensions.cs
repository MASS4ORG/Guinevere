using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Adds <c>.pss</c> styling to <see cref="Gui"/> and <see cref="ControlPalette"/>. The state lives beside each
/// <see cref="Gui"/> instance and is collected with it.
/// </summary>
public static class StylingExtensions
{
    static readonly ConditionalWeakTable<Gui, GuiStyling> States = new();

    /// <summary>Per-<see cref="Gui"/> stylesheets and the ancestor buffer reused by every styled node.</summary>
    sealed class GuiStyling
    {
        public readonly StyleSheetCollection Sheets = [];
        public readonly List<StyleTarget> Ancestors = [];
    }

    extension(Gui gui)
    {
        /// <summary>
        /// Active <c>.pss</c> stylesheets, lowest priority first, with host token overrides. Add sheets before the
        /// frame; every styled node resolves against them through a cache that any change invalidates.
        /// </summary>
        public StyleSheetCollection StyleSheets => States.GetOrCreateValue(gui).Sheets;

        /// <summary>Adds a reloadable source and keeps its entry in the GUI's stylesheets current.</summary>
        /// <param name="source">The reloadable sheet.</param>
        public void AddStyleSheet(StyleSheetSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var sheets = gui.StyleSheets;
            var current = source.Current;
            sheets.Add(current);
            source.Reloaded += replacement =>
            {
                var index = sheets.IndexOf(current);
                if (index >= 0) sheets[index] = replacement;
                current = replacement;
            };
        }

        /// <summary>Applies a semantic stylesheet rule to <see cref="Gui.ControlPalette"/>.</summary>
        /// <param name="type">Selector type containing palette declarations.</param>
        /// <param name="fallback">Palette used for declarations the rule omits. Defaults to the current palette.</param>
        public void ApplyControlPalette(string type = "control-palette", ControlPalette? fallback = null) =>
            gui.ControlPalette = ControlPalette.FromStyle(gui.ResolveStyle(type), fallback ?? gui.ControlPalette);

        /// <summary>
        /// Creates a layout node and styles it from the GUI's stylesheets by its type, classes and
        /// id. Layout declarations are applied to the node in the build pass; <c>background-color</c>,
        /// <c>border-radius</c>, <c>border-color</c> and <c>border-width</c> are drawn behind the node's
        /// children in the render pass, re-resolved for <c>:hover</c>, <c>:active</c> and <c>:focus</c>.
        /// <c>:disabled</c> applies in both passes, so it may change layout, and suppresses hover and press.
        /// </summary>
        /// <param name="type">Element type name matched by a bare-type selector, or <c>null</c>.</param>
        /// <param name="classes">Class names matched by <c>.class</c> selectors.</param>
        /// <param name="id">Element id matched by an <c>#id</c> selector, or <c>null</c>.</param>
        /// <param name="modifiers">
        /// Active semantic modifiers matched by custom pseudo-classes; <see cref="StyleModifiers"/> lists the standard
        /// names. Defaults to <paramref name="classes"/>.
        /// </param>
        /// <param name="variables">Typed variables exposed to declarations as <c>$name</c>.</param>
        /// <param name="disabled">Whether the element is disabled, matched by <c>:disabled</c>.</param>
        /// <param name="filePath">Compiler-supplied; do not pass.</param>
        /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
        /// <returns>The layout node, ready to <c>.Enter()</c>.</returns>
        public LayoutNode StyledNode(
            string? type = null,
            IReadOnlyList<string>? classes = null,
            string? id = null,
            IReadOnlyList<string>? modifiers = null,
            IReadOnlyList<StyleVariable>? variables = null,
            bool disabled = false,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var parent = gui.CurrentNode;
            var node = gui.Node(-1, -1, id, filePath, lineNumber);
            var styling = States.GetOrCreateValue(gui);
            if (styling.Sheets.Count == 0) return node;

            var state = disabled ? StyleState.Disabled : StyleState.None;
            var target = CreateStyleTarget(styling.Ancestors, parent, type, classes, id, modifiers ?? classes, state);
            node.StyleTarget = target with { Ancestors = null };

            if (gui.Pass == Pass.Pass1Build)
            {
                StyleLayout.Apply(node, styling.Sheets.Resolve(target, variables));
                return node;
            }

            var live = state | InteractionState(gui, node, disabled);
            DrawStyledBox(node, styling.Sheets.Resolve(target with { State = live }, variables));
            return node;
        }

        /// <summary>
        /// Resolves the effective <c>.pss</c> style for an element with the given type, classes and id
        /// against the GUI's stylesheets, without creating a node. Callers that draw their own
        /// control (buttons, text) use this to read declarations like <c>width</c> or <c>color</c>; cached hits
        /// without variables do not allocate.
        /// </summary>
        /// <param name="type">Element type name, or <c>null</c>.</param>
        /// <param name="classes">Class names.</param>
        /// <param name="id">Element id, or <c>null</c>.</param>
        /// <param name="state">Interaction state to resolve for.</param>
        /// <param name="modifiers">Active semantic modifiers.</param>
        /// <param name="ancestors">Nearest-first styled ancestors for combinator matching.</param>
        /// <param name="variables">Typed variables exposed to declarations as <c>$name</c>.</param>
        public ResolvedStyle ResolveStyle(
            string? type = null,
            IReadOnlyList<string>? classes = null,
            string? id = null,
            StyleState state = StyleState.None,
            IReadOnlyList<string>? modifiers = null,
            IReadOnlyList<StyleTarget>? ancestors = null,
            IReadOnlyList<StyleVariable>? variables = null) =>
            gui.StyleSheets.Resolve(new StyleTarget(type, id, classes ?? [], state, modifiers, ancestors), variables);
    }

    extension(ControlPalette)
    {
        /// <summary>
        /// Creates a palette by applying semantic color declarations from a resolved style over a fallback.
        /// Supported names mirror the property names in kebab case, such as <c>surface-hover</c>,
        /// <c>text-disabled</c>, <c>focus-ring</c>, and <c>text-selection</c>.
        /// </summary>
        /// <param name="style">The resolved palette rule.</param>
        /// <param name="fallback">Palette used for colors the style omits. Defaults to <see cref="ControlPalette.Light"/>.</param>
        public static ControlPalette FromStyle(ResolvedStyle style, ControlPalette? fallback = null)
        {
            ArgumentNullException.ThrowIfNull(style);
            var source = fallback ?? ControlPalette.Light;
            Color Get(string name, Color value) => style.GetColor(name) ?? value;
            return new ControlPalette
            {
                BaseBackground = Get("base-background", source.BaseBackground),
                Surface = Get("surface", source.Surface),
                SurfaceHover = Get("surface-hover", source.SurfaceHover),
                SurfaceActive = Get("surface-active", source.SurfaceActive),
                Popup = Get("popup", source.Popup),
                Border = Get("border", source.Border),
                BorderActive = Get("border-active", source.BorderActive),
                Divider = Get("divider", source.Divider),
                Accent = Get("accent", source.Accent),
                AccentHover = Get("accent-hover", source.AccentHover),
                AccentSubtle = Get("accent-subtle", source.AccentSubtle),
                Text = Get("text", source.Text),
                TextDim = Get("text-dim", source.TextDim),
                TextDisabled = Get("text-disabled", source.TextDisabled),
                TextOnAccent = Get("text-on-accent", source.TextOnAccent),
                Selected = Get("selected", source.Selected),
                Positive = Get("positive", source.Positive),
                Negative = Get("negative", source.Negative),
                Warning = Get("warning", source.Warning),
                Info = Get("info", source.Info),
                FocusRing = Get("focus-ring", source.FocusRing),
                Shadow = Get("shadow", source.Shadow),
                Overlay = Get("overlay", source.Overlay),
                TextSelection = Get("text-selection", source.TextSelection)
            };
        }
    }

    /// <summary>Builds a target whose ancestors live in a shared buffer that is only valid during this call.</summary>
    static StyleTarget CreateStyleTarget(List<StyleTarget> buffer, LayoutNode? parent, string? type,
        IReadOnlyList<string>? classes, string? id, IReadOnlyList<string>? modifiers, StyleState state)
    {
        buffer.Clear();
        for (var current = parent; current is not null; current = current.Parent)
            if (current.StyleTarget is { } ancestor) buffer.Add(ancestor);
        return new StyleTarget(type, id, classes ?? [], state, modifiers, buffer);
    }

    /// <summary>Focus, plus hover and press unless disabled; a disabled node never takes the pointer.</summary>
    static StyleState InteractionState(Gui gui, LayoutNode node, bool disabled)
    {
        var state = gui.HasFocus(node.Id) ? StyleState.Focus : StyleState.None;
        if (disabled) return state;
        var interactable = gui.GetInteractable(node);
        if (interactable.OnHover()) state |= StyleState.Hover;
        return interactable.OnHold() ? state | StyleState.Active : state;
    }

    static void DrawStyledBox(LayoutNode node, ResolvedStyle style)
    {
        var background = style.GetColor("background-color") ?? style.GetColor("bg-color");
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
