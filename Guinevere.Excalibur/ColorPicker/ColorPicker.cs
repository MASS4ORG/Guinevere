using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class ColorValueState
    {
        public Hsva Value;
    }

    sealed class ColorPickerState
    {
        public int Mode;
        public Task<Color?>? Sample;
    }

    sealed class ColorFieldState
    {
        public Hsva Value;
        public bool Open;
        public bool? PendingOpen;
        public Color Original;
        public Rect Anchor;
    }

    const float ColorRowGap = 8f;
    const float SwatchSize = 18f;
    const float SwatchGap = 4f;
    static readonly string[] ColorModeClass = ["color-mode"];
    static readonly string[] ColorModeCheckedClass = ["color-mode", "checked"];
    static readonly string[] RecentClass = ["recent"];
    static readonly string[] ColorPopupClass = ["color-popup"];

    /// <summary>
    /// A color picker for a caller-owned <see cref="Color"/>. Hue and saturation survive grays and black between
    /// frames. See the <see cref="Hsva"/> overload for the layout and styling.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="color">The edited color.</param>
    /// <param name="options">What to show; <see cref="ColorPickerOptions.For"/> by default.</param>
    /// <param name="original">The color before editing; shows a before/after preview whose left half restores it.</param>
    /// <param name="enabled">Whether the picker responds to input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for stylesheet selectors and persistent control state.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorPicker(this Gui gui, ref Color color, ColorPickerOptions? options = null,
        Color? original = null, bool enabled = true, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var initial = color;
        var state = gui.ControlState((id ?? gui.AutomaticId(filePath, lineNumber)) + "/value",
            () => new ColorValueState { Value = Hsva.FromColor(initial) });
        state.Value = Hsva.Sync(state.Value, color);
        var changed = gui.ColorPicker(ref state.Value, options, original, enabled, classes, id, filePath, lineNumber);
        color = state.Value.ToColor();
        return changed;
    }

    /// <summary>
    /// A color picker assembled from the public parts: <see cref="ColorArea"/> with its companion
    /// <see cref="ColorStrip"/> and an alpha strip, a mode switch (<c>button.color-mode</c>, <c>:checked</c>),
    /// <see cref="ColorChannels"/>, a row with <see cref="ColorPreview"/>, <see cref="ColorHex"/> and the eyedropper
    /// (when the platform publishes <see cref="IColorSamplerCapability"/>), then preset and recent
    /// <see cref="ColorSwatches"/> (the recent row has class <c>recent</c>). The root is a <c>color-picker</c> column and
    /// each row a <c>color-picker-row</c>; <see cref="ColorPickerOptions.Body"/> replaces the layout.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited color.</param>
    /// <param name="options">What to show; <see cref="ColorPickerOptions.For"/> by default.</param>
    /// <param name="original">The color before editing; shows a before/after preview whose left half restores it.</param>
    /// <param name="enabled">Whether the picker responds to input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for stylesheet selectors and persistent control state.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorPicker(this Gui gui, ref Hsva value, ColorPickerOptions? options = null,
        Color? original = null, bool enabled = true, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        options ??= ColorPickerOptions.For(gui);
        ExcaliburStyles.Ensure(gui);
        var before = value;
        var node = Sized(gui.StyledNode("color-picker", classes, id, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber), options.Width, -1);
        using (node.Enter())
        {
            if (options.Body is { } body) body(gui, ref value, original);
            else DefaultPickerBody(gui, ref value, options, original, enabled, node.Id);
        }
        return gui.Pass == Pass.Pass2Render && value != before;
    }

    static void DefaultPickerBody(Gui gui, ref Hsva value, ColorPickerOptions options, Color? original, bool enabled,
        string pickerId)
    {
        var state = gui.ControlState(pickerId, () => new ColorPickerState());
        SurfaceRow(gui, ref value, options, enabled);
        ChannelRows(gui, ref value, state, options, enabled, pickerId);
        SampleRow(gui, ref value, state, options, original, enabled, pickerId);
        SwatchRows(gui, ref value, options, enabled);
    }

    static void SurfaceRow(Gui gui, ref Hsva value, ColorPickerOptions options, bool enabled)
    {
        using (gui.StyledNode("color-picker-row").ExpandWidth().Height(options.AreaHeight).Enter())
        {
            gui.ColorArea(ref value, options.Shape, width: 0, height: 0, enabled: enabled);
            gui.ColorStrip(ref value, options.Shape.Companion, vertical: true, length: 0, enabled: enabled);
            if (options.Alpha) gui.ColorStrip(ref value, HsvChannel.Alpha, vertical: true, length: 0, enabled: enabled);
        }
    }

    static void ChannelRows(Gui gui, ref Hsva value, ColorPickerState state, ColorPickerOptions options, bool enabled,
        string pickerId)
    {
        var modes = options.Modes;
        if (modes.Count == 0) return;
        if (modes.Count > 1) ColorModeSwitch(gui, state, modes, enabled, pickerId);
        gui.ColorChannels(ref value, modes[Math.Clamp(state.Mode, 0, modes.Count - 1)], options.Alpha,
            enabled: enabled);
    }

    static void SampleRow(Gui gui, ref Hsva value, ColorPickerState state, ColorPickerOptions options,
        Color? original, bool enabled, string pickerId)
    {
        using (gui.StyledNode("color-picker-row").ExpandWidth().Height(ControlMetrics.CompactHeight).Enter())
        {
            if (gui.ColorPreview(value.ToColor(), original) && enabled && original is { } restore)
                value = Hsva.Sync(value, restore);
            if (options.Hex) gui.ColorHex(ref value, options.Alpha, options.Clipboard, enabled: enabled);
            if (gui.Platform.TryGet<IColorSamplerCapability>(out var sampler))
                value = Eyedropper(gui, state, sampler!, value, pickerId, enabled);
        }
    }

    static void SwatchRows(Gui gui, ref Hsva value, ColorPickerOptions options, bool enabled)
    {
        if (options.Swatches.Count > 0) gui.ColorSwatches(ref value, options.Swatches, SwatchSize, enabled);
        var recent = ColorRecents.For(gui).Items;
        if (options.ShowRecent && recent.Count > 0)
            gui.ColorSwatches(ref value, recent, SwatchSize, enabled, RecentClass);
    }

    static void ColorModeSwitch(Gui gui, ColorPickerState state, IReadOnlyList<IColorPickerMode> modes, bool enabled,
        string pickerId)
    {
        using (gui.StyledNode("color-picker-row").ExpandWidth().Height(ControlMetrics.CompactHeight).Enter())
        {
            for (var i = 0; i < modes.Count; i++)
                if (gui.Button(modes[i].Name, height: ControlMetrics.CompactHeight, enabled: enabled,
                        classes: i == state.Mode ? ColorModeCheckedClass : ColorModeClass, id: $"{pickerId}/mode/{i}"))
                    state.Mode = i;
        }
    }

    /// <summary>The eyedropper button; starts a sample on click and applies it on the frame it completes.</summary>
    static Hsva Eyedropper(Gui gui, ColorPickerState state, IColorSamplerCapability sampler, Hsva value,
        string pickerId, bool enabled)
    {
        var idle = state.Sample is null;
        if (GlyphButton(gui, WidgetIcons.Eyedropper, ControlMetrics.CompactHeight, pickerId + "/eyedropper",
                enabled && idle))
            state.Sample = sampler.SampleAsync().AsTask();
        if (gui.Pass != Pass.Pass2Render || state.Sample is not { IsCompleted: true } sample) return value;
        state.Sample = null;
        return SampledColor(sample) is { } picked ? Hsva.Sync(value, picked) : value;
    }

    static Color? SampledColor(Task<Color?> sample) => sample.IsCompletedSuccessfully ? sample.Result : null;

    /// <summary>
    /// An inline color field: a <c>swatch</c> (<c>:open</c> while its popup shows) that opens a
    /// <see cref="ColorPicker(Gui, ref Hsva, ColorPickerOptions?, Color?, bool, IReadOnlyList{string}?, string?, string, int)"/>
    /// popup (<c>popup.color-popup</c>), followed by an editable <see cref="ColorHex"/>. The popup closes on Escape, a
    /// click outside or a second click on the swatch, and adds a changed color to the GUI's <see cref="ColorRecents"/>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="color">The edited color.</param>
    /// <param name="options">What the popup picker shows; <see cref="ColorPickerOptions.For"/> by default.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height.</param>
    /// <param name="enabled">Whether the field responds to input.</param>
    /// <param name="classes">Extra classes for the <c>color-field</c> row.</param>
    /// <param name="id">Element id for stylesheet selectors and persistent control state.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorField(this Gui gui, ref Color color, ColorPickerOptions? options = null,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.CompactHeight, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        options ??= ColorPickerOptions.For(gui);
        ExcaliburStyles.Ensure(gui);
        var node = Sized(gui.StyledNode("color-field", classes, id, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber), width, height);
        var initial = color;
        var state = gui.ControlState(node.Id, () => new ColorFieldState { Value = Hsva.FromColor(initial) });
        if (gui.Pass == Pass.Pass1Build && state.PendingOpen is { } pending)
        {
            state.Open = pending && enabled;
            state.PendingOpen = null;
        }
        state.Value = Hsva.Sync(state.Value, color);
        var before = state.Value;
        using (node.Enter())
        {
            if (gui.Pass == Pass.Pass2Render) state.Anchor = node.Rect;
            ColorFieldSwatch(gui, state, color, height, enabled);
            gui.ColorHex(ref state.Value, options.Alpha, clipboard: false, width: 0, height: height, enabled: enabled,
                id: node.Id + "/hex");
        }
        ColorFieldPopup(gui, state, options, enabled, node.Id);
        color = state.Value.ToColor();
        return gui.Pass == Pass.Pass2Render && state.Value != before;
    }

    static void ColorFieldSwatch(Gui gui, ColorFieldState state, Color color, float height, bool enabled)
    {
        using (gui.StyledNode("swatch", modifiers: state.Open ? OpenModifier : null, disabled: !enabled)
                   .Width(height * 1.5f).Height(height).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;
            DrawColorSample(gui, gui.CurrentNode.InnerRect, color);
            if (!enabled) return;
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            if (!Activated(gui)) return;
            state.PendingOpen = !state.Open;
            if (!state.Open) state.Original = color;
        }
    }

    static void ColorFieldPopup(Gui gui, ColorFieldState state, ColorPickerOptions options, bool enabled,
        string fieldId)
    {
        var popupWidth = (options.Width > 0 ? options.Width : ControlMetrics.FieldWidth) + 16f;
        var popupHeight = options.PopupHeight ?? EstimatePickerHeight(options, ColorRecents.For(gui).Items.Count) + 16f;
        var below = new Vector2(state.Anchor.X, state.Anchor.Y + state.Anchor.H + 4f);
        var open = state.Open;
        gui.Popup(ref open, () => gui.ColorPicker(ref state.Value, options, state.Original, enabled,
                id: fieldId + "/picker"),
            popupWidth, popupHeight, position: ConstrainToScreen(gui, below, popupWidth, popupHeight),
            classes: ColorPopupClass, id: fieldId + "/popup");
        if (gui.Pass != Pass.Pass2Render || open == state.Open) return;
        state.Open = open;
        if (!open && state.Value.ToColor() != state.Original) ColorRecents.For(gui).Add(state.Value.ToColor());
    }

    /// <summary>The default layout's height, from the default sheet's gaps and the options' rows.</summary>
    internal static float EstimatePickerHeight(ColorPickerOptions options, int recentCount)
    {
        var height = options.AreaHeight;
        if (options.Modes.Count > 1) height += ColorRowGap + ControlMetrics.CompactHeight;
        if (options.Modes.Count > 0) height += ColorRowGap + ControlMetrics.CompactHeight;
        height += ColorRowGap + ControlMetrics.CompactHeight;
        var width = options.Width > 0 ? options.Width : ControlMetrics.FieldWidth;
        height += SwatchRowsHeight(options.Swatches.Count, width);
        if (options.ShowRecent) height += SwatchRowsHeight(recentCount, width);
        return height;
    }

    static float SwatchRowsHeight(int count, float width)
    {
        if (count == 0) return 0f;
        var perRow = Math.Max(1, (int)((width + SwatchGap) / (SwatchSize + SwatchGap)));
        var rows = (count + perRow - 1) / perRow;
        return ColorRowGap + rows * SwatchSize + (rows - 1) * SwatchGap;
    }
}
