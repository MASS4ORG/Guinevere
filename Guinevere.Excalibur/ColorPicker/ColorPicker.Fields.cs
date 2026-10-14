using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class ColorHexState
    {
        public bool Editing;
        public string Draft = "";
    }

    static readonly string[] HexClass = ["hex"];

    /// <summary>
    /// A hexadecimal color field (<c>#rrggbbaa</c>, or <c>#rrggbb</c> without <paramref name="alpha"/>) with copy and
    /// paste buttons when the platform has a clipboard. Typing applies as soon as the text holds six or eight digits;
    /// Enter or leaving the field also accepts the short <c>#rgb</c>/<c>#rgba</c> forms, and invalid text reverts. A
    /// <c>color-hex</c> row holding an <c>input.hex</c> and <c>button.icon</c> buttons.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited color.</param>
    /// <param name="alpha">Whether the text carries the alpha byte.</param>
    /// <param name="clipboard">Whether to show the copy and paste buttons.</param>
    /// <param name="width">Row width; 0 expands.</param>
    /// <param name="height">Row height.</param>
    /// <param name="enabled">Whether the field responds to input.</param>
    /// <param name="classes">Extra classes for the row.</param>
    /// <param name="id">Element id of the row; the input's and buttons' ids derive from it.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorHex(this Gui gui, ref Hsva value, bool alpha = true, bool clipboard = true,
        float width = 0, float height = ControlMetrics.CompactHeight, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ExcaliburStyles.Ensure(gui);
        var node = Sized(gui.StyledNode("color-hex", classes, id, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber), width, height);
        var before = value;
        using (node.Enter())
        {
            var inputId = node.Id + "/text";
            var state = gui.ControlState(inputId, () => new ColorHexState());
            if (gui.Pass == Pass.Pass2Render)
            {
                value = TrackHexEditing(gui, state, inputId, alpha, value);
            }

            value = HexInput(gui, state, inputId, value, alpha, height, enabled);
            if (clipboard && gui.Platform.TryGet<IClipboard>(out var board))
            {
                value = ClipboardButtons(gui, board!, node.Id, value, alpha, height, enabled);
            }
        }
        return gui.Pass == Pass.Pass2Render && value != before;
    }

    /// <summary>The text input; while editing it shows the draft, and a complete typed color applies at once.</summary>
    static Hsva HexInput(Gui gui, ColorHexState state, string inputId, Hsva value, bool alpha, float height,
        bool enabled)
    {
        var text = state.Editing ? state.Draft : value.ToColor().ToHex(alpha);
        gui.TextInput(ref text, width: 0, height: height, fontSize: ControlMetrics.CompactFontSize,
            enabled: enabled, id: inputId, classes: HexClass);
        return gui.Pass == Pass.Pass2Render && state.Editing && text != state.Draft
            ? ApplyTypedHex(state, text, value, alpha)
            : value;
    }

    /// <summary>Stores the typed draft and applies it when it holds a complete six- or eight-digit color.</summary>
    static Hsva ApplyTypedHex(ColorHexState state, string text, Hsva value, bool alpha)
    {
        state.Draft = text;
        return HexDigits(text) is 6 or 8 && TryParseColorText(text, out var typed)
            ? Hsva.Sync(value, KeepAlpha(typed, value, alpha))
            : value;
    }

    /// <summary>A parsed color, with the current alpha when the field does not edit alpha.</summary>
    static Color KeepAlpha(Color parsed, Hsva value, bool alpha) =>
        alpha ? parsed : Color.FromArgb((int)MathF.Round(Math.Clamp(value.A, 0f, 1f) * 255f), parsed);

    /// <summary>Starts a draft when the input gains focus and applies it when focus leaves.</summary>
    static Hsva TrackHexEditing(Gui gui, ColorHexState state, string inputId, bool alpha, Hsva value)
    {
        var focused = gui.HasFocus(inputId);
        if (focused && !state.Editing)
        {
            state.Editing = true;
            state.Draft = value.ToColor().ToHex(alpha);
            return value;
        }
        if (!(focused ? gui.Input.IsKeyPressed(KeyboardKey.Enter) : state.Editing))
        {
            return value;
        }

        state.Editing = focused;
        if (TryParseColorText(state.Draft, out var color))
        {
            value = Hsva.Sync(value, KeepAlpha(color, value, alpha));
        }

        state.Draft = value.ToColor().ToHex(alpha);
        return value;
    }

    static Hsva ClipboardButtons(Gui gui, IClipboard board, string rowId, Hsva value, bool alpha, float size,
        bool enabled)
    {
        if (GlyphButton(gui, WidgetIcons.Copy, size, rowId + "/copy", enabled))
        {
            board.SetClipboardText(value.ToColor().ToHex(alpha));
        }

        if (GlyphButton(gui, WidgetIcons.Paste, size, rowId + "/paste", enabled)
            && TryParseColorText(board.GetClipboardText(), out var pasted))
        {
            value = Hsva.Sync(value, KeepAlpha(pasted, value, alpha));
        }

        return value;
    }

    /// <summary>
    /// Parses color text as copied from other tools: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c>, with
    /// or without <c>#</c> or <c>0x</c>, ignoring surrounding whitespace and quotes.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="color">The parsed color.</param>
    /// <returns>Whether the text holds a color.</returns>
    public static bool TryParseColorText(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim().Trim("\"'`").Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            span = span[2..];
        }

        return Color.TryParseHex(span.ToString(), out color);
    }

    static int HexDigits(string text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (Uri.IsHexDigit(c))
            {
                count++;
            }
            else if (c != '#' && !char.IsWhiteSpace(c))
            {
                return -1;
            }
        }

        return count;
    }

    /// <summary>
    /// One number field per channel of <paramref name="mode"/>, plus alpha in [0, 255]; a committed edit rebuilds the
    /// color through the mode. A <c>color-channels</c> row of <c>color-channel</c> cells, each a <c>label</c> and an
    /// <c>input.number</c>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited color.</param>
    /// <param name="mode">The color model; <see cref="ColorPickerModes.Rgb"/> by default.</param>
    /// <param name="alpha">Whether to add the alpha field.</param>
    /// <param name="height">Row height.</param>
    /// <param name="enabled">Whether the fields respond to input.</param>
    /// <param name="classes">Extra classes for the row.</param>
    /// <param name="id">Element id of the row; field ids derive from it.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorChannels(this Gui gui, ref Hsva value, IColorPickerMode? mode = null, bool alpha = true,
        float height = ControlMetrics.CompactHeight, bool enabled = true, IReadOnlyList<string>? classes = null,
        string? id = null, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        mode ??= ColorPickerModes.Rgb;
        ExcaliburStyles.Ensure(gui);
        var before = value;
        var channels = mode.Channels;
        Span<float> values = stackalloc float[channels.Count];
        mode.Read(value, values);
        using (gui.StyledNode("color-channels", classes, id, disabled: !enabled, filePath: filePath,
                   lineNumber: lineNumber).ExpandWidth().Height(height).Enter())
        {
            var rowId = gui.CurrentNode.Id;
            for (var i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                var number = values[i];
                if (!ChannelField(gui, channel, ref number, $"{rowId}/{mode.Name}/{i}", height, enabled))
                {
                    continue;
                }

                values[i] = number;
                value = mode.Write(value, values);
            }
            var opacity = MathF.Round(value.A * 255f);
            if (alpha && ChannelField(gui, new ColorChannel("A", 0, 255), ref opacity, rowId + "/alpha", height, enabled))
            {
                value = value with { A = opacity / 255f };
            }
        }
        return gui.Pass == Pass.Pass2Render && value != before;
    }

    static bool ChannelField(Gui gui, ColorChannel channel, ref float number, string fieldId, float height,
        bool enabled)
    {
        using (gui.StyledNode("color-channel").ExpandWidth().Height(height).Enter())
        {
            using (gui.StyledNode("label").Enter())
            {
                gui.DrawText(channel.Label, ControlMetrics.CompactFontSize, centerInRect: false);
            }

            return gui.NumberField(ref number, 1f, channel.Min, channel.Max, width: 0, height: height,
                format: channel.Format, fontSize: ControlMetrics.CompactFontSize, padding: 4, enabled: enabled,
                id: fieldId);
        }
    }

    /// <summary>A square <c>button.icon</c> drawing a widget-icon glyph; true when clicked or activated by keyboard.</summary>
    static bool GlyphButton(Gui gui, string glyph, float size, string id, bool enabled,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        using (StyledButton(gui, IconClass, null, id, enabled, filePath, lineNumber).Width(size).Height(size)
                   .ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.DrawText(glyph, size * 0.5f);
            if (gui.Pass != Pass.Pass2Render || !enabled)
            {
                return false;
            }

            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            return Activated(gui);
        }
    }
}
