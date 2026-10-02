namespace Autoformers;

/// <summary>
/// The layout pieces form drawers share — a labelled row, scrub numbers, buttons, fold arrows — so a custom
/// drawer stays a few calls over the same look the built-in rows have. Each builds the same nodes in both passes
/// and reacts to input only in the render pass.
/// </summary>
public static class FormControls
{
    static readonly string[] Axes = ["X", "Y", "Z", "W"];

    static readonly Dictionary<Type, (double Min, double Max)> TypeRanges = new()
    {
        [typeof(byte)] = (byte.MinValue, byte.MaxValue),
        [typeof(sbyte)] = (sbyte.MinValue, sbyte.MaxValue),
        [typeof(short)] = (short.MinValue, short.MaxValue),
        [typeof(ushort)] = (ushort.MinValue, ushort.MaxValue),
        [typeof(int)] = (int.MinValue, int.MaxValue),
        [typeof(uint)] = (uint.MinValue, uint.MaxValue),
        [typeof(long)] = (long.MinValue, long.MaxValue),
        [typeof(ulong)] = (ulong.MinValue, ulong.MaxValue),
        [typeof(float)] = (float.MinValue, float.MaxValue),
        [typeof(double)] = (double.MinValue, double.MaxValue),
        [typeof(decimal)] = ((double)decimal.MinValue, (double)decimal.MaxValue),
    };

    /// <summary>A label column on the left and whatever <paramref name="editor"/> draws filling the rest.</summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="label">The label text.</param>
    /// <param name="id">Stable id, unique among siblings.</param>
    /// <param name="editor">Draws the value side.</param>
    /// <param name="modified">Draws the label bold with an accent margin bar, for values that differ from a source.</param>
    /// <param name="labelInteraction">Runs in the render pass inside the label node, for label drags; highlights the label on hover.</param>
    /// <param name="height">The row's height for a taller editor; the style's row height when null. The label stays on the first line.</param>
    public static void Row(Gui gui, string label, string id, Action editor, bool modified = false,
        Action? labelInteraction = null, float? height = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(editor);
        var style = new FormStyle(gui);
        var rowHeight = height ?? style.RowHeight;

        using (gui.Node(-1, rowHeight, id).ExpandWidth().Direction(Axis.Horizontal).Gap(6f).Enter())
        {
            MarkModified(gui, modified);
            using (gui.Node(style.LabelWidth, style.RowHeight, $"{id}/label").ContentAlignY(0.5f).Enter())
            {
                var interactive = labelInteraction is not null && gui.Pass == Pass.Pass2Render;
                var hot = interactive && gui.GetInteractable().OnHover();
                if (interactive) labelInteraction!();

                var color = hot ? style.Accent : style.Ink;
                gui.DrawText(label, style.FontSize, color, centerInRect: false, effects: Emphasis(modified, color));
            }

            using (gui.Node(-1, rowHeight, $"{id}/editor").Expand().Direction(Axis.Horizontal).Gap(4f).Enter())
                editor();
        }
    }

    /// <summary>
    /// Bold text. The default font has one weight, so bold is a hairline outline in the text's own color.
    /// </summary>
    /// <returns>The effects to pass to <c>DrawText</c>, or null when not bold.</returns>
    public static TextEffects? Emphasis(bool bold, Color color) =>
        bold ? new TextEffects { Outline = new TextEffects.TextOutline(color, 0.35f) } : null;

    /// <summary>
    /// Makes the current node a scrub handle for a numeric field: dragging right or up increases it, an integer by
    /// one per pixel and anything else by a hundredth, clamped to <c>[Range]</c> or the type's own limits.
    /// </summary>
    public static void ScrubLabel(Gui gui, FormField field, Type type)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(field);
        if (!gui.GetInteractable().OnDrag(out var drag)) return;

        var current = Convert.ToDouble(field.GetValue() ?? 0, CultureInfo.InvariantCulture);
        var delta = (drag.FrameDelta.X - drag.FrameDelta.Y) * (IsIntegral(type) ? 1 : 0.01);
        var (min, max) = field.Range ?? TypeRange(type);
        var next = Math.Clamp(current + delta, min, max);
        if (Math.Abs(next - current) > double.Epsilon) field.SetValue(ToNumber(next, type));
    }

    /// <summary>
    /// A scrub number field: drag to change, click to type. Edits round-trip through double so a long, a decimal
    /// or an unsigned keeps its digits.
    /// </summary>
    /// <returns>True when the user changed the value; <paramref name="result"/> holds it.</returns>
    public static bool NumberEditor(Gui gui, double value, string id, bool integral, double min, double max,
        out double result)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var style = new FormStyle(gui);
        var edited = value;

        gui.NumberField(ref edited, integral ? 1d : 0.01d, min, max, width: 0, height: style.RowHeight,
            format: integral ? "0" : "0.###", backgroundColor: style.Field, borderColor: style.Border,
            textColor: style.Ink, fontSize: style.FontSize, padding: 4, id: id, alignX: 1f);

        result = edited;
        return !edited.Equals(value);
    }

    /// <summary>A labelled row of X/Y fields writing through <paramref name="set"/> and touching <paramref name="owner"/>.</summary>
    public static void VectorRow(Gui gui, string id, string label, Vector2 value, Action<Vector2> set,
        FormField owner, FormRenderContext? context = null) =>
        VectorRow(gui, id, label, [value.X, value.Y], parts => set(new Vector2(parts[0], parts[1])), owner, context);

    /// <summary>A labelled row of X/Y/Z fields writing through <paramref name="set"/> and touching <paramref name="owner"/>.</summary>
    public static void VectorRow(Gui gui, string id, string label, Vector3 value, Action<Vector3> set,
        FormField owner, FormRenderContext? context = null) =>
        VectorRow(gui, id, label, [value.X, value.Y, value.Z],
            parts => set(new Vector3(parts[0], parts[1], parts[2])), owner, context);

    /// <summary>A labelled row of X/Y/Z/W fields writing through <paramref name="set"/> and touching <paramref name="owner"/>.</summary>
    public static void VectorRow(Gui gui, string id, string label, Vector4 value, Action<Vector4> set,
        FormField owner, FormRenderContext? context = null) =>
        VectorRow(gui, id, label, [value.X, value.Y, value.Z, value.W],
            parts => set(new Vector4(parts[0], parts[1], parts[2], parts[3])), owner, context);

    /// <summary>
    /// A full-width button sized like a row, for <c>[Button]</c> actions. Blocks input so a press never falls
    /// through to what is behind it.
    /// </summary>
    /// <returns>True on the render pass the button was clicked.</returns>
    public static bool TextButton(Gui gui, string label, string id, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var style = new FormStyle(gui);

        using (gui.Node(-1, style.RowHeight, id).ExpandWidth().BlockInput().ContentAlignX(0.5f).ContentAlignY(0.5f)
                   .Enter())
        {
            var interactable = gui.GetInteractable();
            var hot = enabled && interactable.OnHover();

            if (gui.Pass == Pass.Pass2Render) gui.DrawBackgroundRect(hot ? style.Border : style.Field, 3f);
            gui.DrawText(label, style.FontSize, enabled ? style.Ink : style.InkDim);

            return gui.Pass == Pass.Pass2Render && hot && interactable.OnClick();
        }
    }

    /// <summary>A square glyph button the size of a row. Blocks input so a click never reaches the row behind it.</summary>
    /// <returns>True on the render pass the button was clicked.</returns>
    public static bool SmallButton(Gui gui, string glyph, string id)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var style = new FormStyle(gui);

        using (gui.Node(style.RowHeight, style.RowHeight, id).BlockInput().ContentAlignX(0.5f).ContentAlignY(0.5f)
                   .Enter())
        {
            var interactable = gui.GetInteractable();
            var hot = interactable.OnHover();

            if (gui.Pass == Pass.Pass2Render && hot) gui.DrawBackgroundRect(style.Border, 3f);
            gui.DrawText(glyph, style.FontSize + 1f, hot ? style.Ink : style.InkDim);

            return gui.Pass == Pass.Pass2Render && hot && interactable.OnClick();
        }
    }

    /// <summary>A fold arrow in the current node: open points down, closed points right.</summary>
    public static void FoldArrow(Gui gui, bool isOpen)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var style = new FormStyle(gui);
        gui.DrawText(isOpen ? style.CaretOpen : style.CaretClosed, style.FontSize * 0.75f, style.InkDim);
    }

    /// <summary>Draws the modified bar in the left margin of the current row.</summary>
    internal static void MarkModified(Gui gui, bool modified)
    {
        if (!modified || gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        gui.DrawRect(new Rect(rect.X - 6f, rect.Y + 1f, 2f, rect.H - 2f), new FormStyle(gui).Accent);
    }

    /// <summary>One axis: a dim X/Y/Z/W prefix that scrubs, and a number field sharing the width evenly.</summary>
    /// <returns>True when the prefix was dragged or the field edited; <paramref name="result"/> holds the value.</returns>
    internal static bool AxisField(Gui gui, int index, double value, string id, out double result)
    {
        var style = new FormStyle(gui);
        var original = value;
        using (gui.Node(style.FontSize, style.RowHeight, $"{id}/axis").Enter())
        {
            var render = gui.Pass == Pass.Pass2Render;
            var hot = render && gui.GetInteractable().OnHover();
            if (render && gui.GetInteractable().OnDrag(out var drag))
                value += (drag.FrameDelta.X - drag.FrameDelta.Y) * 0.01;

            gui.DrawText(Axes[Math.Min(index, Axes.Length - 1)], style.FontSize - 1f, hot ? style.Ink : style.InkDim,
                centerInRect: false);
        }

        NumberEditor(gui, value, id, integral: false, float.MinValue, float.MaxValue, out result);
        return !result.Equals(original);
    }

    /// <summary>Edits each axis in turn; true when any changed, with <paramref name="values"/> updated.</summary>
    internal static bool EditAxes(Gui gui, string id, Span<double> values)
    {
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            if (!AxisField(gui, i, values[i], $"{id}/a{i}", out var next)) continue;

            values[i] = next;
            changed = true;
        }

        return changed;
    }

    /// <summary>An edited number as the member's own type, saturating at the type's limits.</summary>
    internal static object ToNumber(double value, Type type)
    {
        try
        {
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return type.GetField(value < 0 ? "MinValue" : "MaxValue")!.GetValue(null)!;
        }
    }

    internal static (double Min, double Max) TypeRange(Type type) =>
        TypeRanges.TryGetValue(type, out var range) ? range : (float.MinValue, float.MaxValue);

    internal static bool IsIntegral(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort);

    internal static bool IsNumeric(Type type) =>
        IsIntegral(type) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    static void VectorRow(Gui gui, string id, string label, double[] parts, Action<float[]> set, FormField owner,
        FormRenderContext? context)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(owner);

        Row(gui, label, id, () =>
        {
            if (!EditAxes(gui, id, parts)) return;

            set(Array.ConvertAll(parts, part => (float)part));
            owner.Touch();
        }, context?.Modified(owner) == true);
    }
}
