using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Adds <c>.pss</c> styling to <see cref="Gui"/> and <see cref="ControlPalette"/>. The state lives beside each
/// <see cref="Gui"/> instance and is collected with it.
/// </summary>
public static class StylingExtensions
{
    static readonly ConditionalWeakTable<Gui, GuiStyling> States = new();

    /// <summary>Per-<see cref="Gui"/> stylesheets, fonts and the ancestor buffer reused by every styled node.</summary>
    sealed class GuiStyling
    {
        public readonly StyleSheetCollection Sheets = [];
        public readonly List<StyleTarget> Ancestors = [];
        public readonly StyleFonts Fonts = new();
    }

    /// <summary>The GUI's stylesheet font resolver, shared by styled text and icons.</summary>
    internal static StyleFonts FontsOf(Gui gui) => States.GetOrCreateValue(gui).Fonts;

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
        /// Creates a layout node and styles it from the GUI's stylesheets by its type, classes and id. Layout
        /// declarations apply in the build pass. The box (<c>background</c>, <c>box-shadow</c>, <c>border-*</c>,
        /// per-corner <c>border-radius</c>, <c>outline</c>) is drawn behind the node's children in the render pass,
        /// re-resolved for <c>:hover</c>, <c>:active</c> and <c>:focus</c>, as is <c>cursor</c>. Text properties
        /// (<c>color</c>, <c>font-*</c>) and <c>opacity</c> apply to the node's scope, so its children inherit them.
        /// <c>:disabled</c> applies in both passes, so it may change layout, and suppresses hover and press. In the
        /// render pass, styled descendants see this node's live state, so <c>checkbox:hover &gt; indicator</c> matches.
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
            var inherited = parent.Scope.Get<StyleTokens>();

            if (gui.Pass == Pass.Pass1Build)
            {
                var style = styling.Sheets.Resolve(target, variables, inherited);
                StyleLayout.Apply(node, style);
                ApplyInherited(gui, node, style, styling, inherited);
                return node;
            }

            var live = state | InteractionState(gui, node, disabled);
            node.StyleTarget = target with { State = live, Ancestors = null };
            var liveStyle = styling.Sheets.Resolve(target with { State = live }, variables, inherited);
            ApplyInherited(gui, node, liveStyle, styling, inherited);
            if (StyleBox.From(liveStyle, node.Rect) is { } box) node.DrawList.Add(box);
            return node;
        }

        /// <summary>
        /// Sets a style token for the current node and its subtree, like an inherited CSS custom property: styled
        /// descendants read <c>$name</c> as <paramref name="value"/>, above sheet and host tokens.
        /// </summary>
        /// <param name="name">Token name, with or without the <c>$</c> or <c>--</c> prefix.</param>
        /// <param name="value">The value; colors, numbers and vectors are formatted as <c>.pss</c> text.</param>
        /// <param name="scope">The scope to set it on; defaults to the current node's.</param>
        public void SetStyleToken(string name, object value, LayoutNodeScope? scope = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            scope ??= gui.CurrentNodeScope;
            var key = name.StartsWith("--", StringComparison.Ordinal) ? name
                : name[0] == '$' ? $"--{name[1..]}" : $"--{name}";
            scope.Set(scope.Get<StyleTokens>().With(new Dictionary<string, string> { [key] = StyleValue.Format(value) }));
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
        /// <remarks>Tokens inherited by the current node (<see cref="StyleTokens"/>) apply as well.</remarks>
        public ResolvedStyle ResolveStyle(
            string? type = null,
            IReadOnlyList<string>? classes = null,
            string? id = null,
            StyleState state = StyleState.None,
            IReadOnlyList<string>? modifiers = null,
            IReadOnlyList<StyleTarget>? ancestors = null,
            IReadOnlyList<StyleVariable>? variables = null) =>
            gui.StyleSheets.Resolve(new StyleTarget(type, id, classes ?? [], state, modifiers, ancestors), variables,
                gui.LayoutNodeScopeStack.Count > 0 ? gui.CurrentNodeScope.Get<StyleTokens>() : null);

        /// <summary>
        /// Resolves a part that a control draws itself instead of building as a node, such as a slider's thumb. The
        /// part matches as a child of the current node, so <c>slider:disabled thumb</c> applies, and reads the
        /// current node's tokens. Call inside a frame.
        /// </summary>
        /// <param name="type">The part's type name.</param>
        /// <param name="state">The part's own state, such as <see cref="StyleState.Focus"/>.</param>
        /// <param name="modifiers">The part's semantic modifiers.</param>
        /// <returns>The resolved style; cached hits do not allocate.</returns>
        public ResolvedStyle ResolvePart(string type, StyleState state = StyleState.None,
            IReadOnlyList<string>? modifiers = null)
        {
            var styling = States.GetOrCreateValue(gui);
            var target = CreateStyleTarget(styling.Ancestors, gui.CurrentNode, type, null, null, modifiers, state);
            return styling.Sheets.Resolve(target, null, gui.CurrentNodeScope.Get<StyleTokens>());
        }
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

    /// <summary>
    /// Focus, plus hover and press unless disabled. Styling never takes pointer capture, so a styled container does
    /// not steal a press from the controls inside it: the node is pressed while it holds the pointer (its control
    /// captured it), or while the button is down over it and nothing holds the pointer.
    /// </summary>
    static StyleState InteractionState(Gui gui, LayoutNode node, bool disabled)
    {
        var state = gui.HasFocus(node.Id) ? StyleState.Focus : StyleState.None;
        if (disabled) return state;
        var hovered = gui.GetInteractable(node).OnHover();
        if (hovered) state |= StyleState.Hover;
        var pressed = gui.HoldsPointer(node.Id, MouseButton.Left)
                      || (hovered && !gui.IsPointerCaptured && gui.Input.IsMouseButtonDown(MouseButton.Left));
        return pressed ? state | StyleState.Active : state;
    }

    /// <summary>
    /// Sets the text <c>color</c>, <c>font-size</c>, <c>font-family</c>/<c>font-weight</c>/<c>font-style</c> and
    /// <c>opacity</c> on the node's scope, and the node's <c>cursor</c>. A weight or style without a family restyles
    /// the inherited font.
    /// </summary>
    static void ApplyInherited(Gui gui, LayoutNode node, ResolvedStyle style, GuiStyling styling,
        StyleTokens inherited)
    {
        PassTokens(node, style, inherited);
        ApplyCursor(node, style);
        if (style.GetColor("color") is { } color) gui.SetTextColor(color, node.Scope);
        if (style.GetLength("font-size") is > 0f and var size && !style.Get("font-size")!.EndsWith('%'))
            gui.SetTextSize(size, node.Scope);
        if (StyleBoxValues.Opacity(style.Get("opacity")) is { } opacity) gui.SetOpacity(opacity, node.Scope);
        ApplyFont(gui, node, style, styling);
    }

    /// <summary>
    /// Passes the matched rules' tokens down to the subtree, re-derived from the parent's tokens in every pass so a
    /// render-pass state change (a <c>:hover</c> token) replaces the build-pass value.
    /// </summary>
    static void PassTokens(LayoutNode node, ResolvedStyle style, StyleTokens inherited)
    {
        if (style.Locals is { Count: > 0 } || node.Scope.HasLocal<StyleTokens>())
            node.Scope.Set(inherited.With(style.Locals));
    }

    static void ApplyCursor(LayoutNode node, ResolvedStyle style)
    {
        if (StyleBoxValues.Cursor(style.Get("cursor")) is { } cursor) node.Cursor(cursor);
    }

    /// <summary>A weight or style without a family restyles the inherited font; unknown families leave it as is.</summary>
    static void ApplyFont(Gui gui, LayoutNode node, ResolvedStyle style, GuiStyling styling)
    {
        var family = style.Get("font-family");
        var weight = style.Get("font-weight");
        var slant = style.Get("font-style");
        if (family is null && weight is null && slant is null) return;

        var inherited = (node.Parent?.Scope ?? node.Scope).Get<LayoutNodeScopeTextFont>().Value;
        var font = styling.Fonts.Resolve(styling.Sheets, family, inherited, StyleFonts.Weight(weight),
            StyleFonts.Italic(slant));
        if (font is not null) gui.SetTextFont(font, node.Scope);
    }
}
