namespace MASS4.Autoformers;

/// <summary>The fallback drawers for booleans, text, enums and numbers, plus the read-only summary.</summary>
static class BuiltinDrawers
{
    sealed record EnumMetadata(string[] Names, string[] Labels);

    static readonly ConditionalWeakTable<Type, EnumMetadata> EnumMetadataByType = [];

    static readonly IPropertyDrawer Boolean = new PrimitiveDrawer((gui, field, _, _, _) => DrawBool(gui, field));
    static readonly IPropertyDrawer Text = new PrimitiveDrawer((gui, field, _, id, _) => DrawString(gui, field, id));
    static readonly IPropertyDrawer Number =
        new PrimitiveDrawer((gui, field, type, id, _) => DrawNumber(gui, field, type, id), numeric: true);
    static readonly IPropertyDrawer Enumeration = new PrimitiveDrawer(DrawEnum);

    /// <summary>The value as dim text, for read-only fields and types with no editor.</summary>
    internal static readonly IPropertyDrawer Summary = new PrimitiveDrawer((gui, field, _, _, _) =>
    {
        var style = new FormStyle(gui);
        gui.DrawText(field.GetValue()?.ToString() ?? "—", style.FontSize, style.InkDim, centerInRect: false);
    });

    /// <summary>The built-in drawer for a value type; the summary when there is none.</summary>
    internal static IPropertyDrawer For(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(bool) ? Boolean
            : type == typeof(string) ? Text
            : type.IsEnum ? Enumeration
            : FormControls.IsNumeric(type) ? Number
            : Summary;
    }

    static void DrawBool(Gui gui, FormField field)
    {
        // Checkbox builds its own nodes, so it runs in both passes; only the render pass writes.
        var current = field.GetValue() is true;
        var next = gui.Checkbox(current, size: new FormStyle(gui).FontSize + 2f);

        if (gui.Pass == Pass.Pass2Render && next != current) field.SetValue(next);
    }

    static void DrawString(Gui gui, FormField field, string id)
    {
        var style = new FormStyle(gui);
        var current = field.GetValue() as string ?? string.Empty;
        var next = field.Attribute<TextAreaAttribute>() is { } area
            ? gui.TextArea(current, width: 0, height: TextAreaHeight(gui, current, area), fontSize: style.FontSize,
                backgroundColor: style.Field, borderColor: style.Border, textColor: style.Ink, padding: 4,
                id: $"{id}/text")
            : gui.TextInput(current, width: 0, height: style.RowHeight, fontSize: style.FontSize,
                backgroundColor: style.Field, borderColor: style.Border, textColor: style.Ink, padding: 4,
                id: $"{id}/text");

        if (!string.Equals(next, current, StringComparison.Ordinal)) field.SetValue(next);
    }

    /// <summary>
    /// A text area's height: its explicit lines clamped to <c>[TextArea]</c>'s range, never shorter than a row.
    /// Past the maximum the area scrolls.
    /// </summary>
    internal static float TextAreaHeight(Gui gui, string text, TextAreaAttribute area)
    {
        var style = new FormStyle(gui);
        var lines = Math.Clamp(text.Count(c => c == '\n') + 1, area.MinLines, area.MaxLines);
        return Math.Max(style.RowHeight, lines * style.FontSize * 1.4f + 8f);
    }

    /// <summary>The row height a field's editor needs: taller for a <c>[TextArea]</c>, otherwise the default.</summary>
    static float? RowHeightFor(Gui gui, FormField field) =>
        field.Attribute<TextAreaAttribute>() is { } area && !field.IsReadOnly
            ? TextAreaHeight(gui, field.GetValue() as string ?? string.Empty, area)
            : null;

    static void DrawEnum(Gui gui, FormField field, Type type, string id, FormRenderContext context)
    {
        var style = new FormStyle(gui);
        var metadata = EnumMetadataByType.GetValue(type, static t =>
        {
            var names = Enum.GetNames(t);
            return new EnumMetadata(names,
                [.. names.Select(name => t.GetField(name)?.GetCustomAttribute<EnumLabelAttribute>()?.Label ?? name)]);
        });
        var labels = context.Translate is { } translate ? [.. metadata.Labels.Select(translate)] : metadata.Labels;
        var current = Array.IndexOf(metadata.Names, field.GetValue()?.ToString() ?? string.Empty);

        // Dropdown keys its open state by call site, so every enum field would otherwise share one.
        // ReSharper disable once ExplicitCallerInfoArgument
        var next = gui.Dropdown(labels, current, width: 0, height: style.RowHeight, fontSize: style.FontSize,
            backgroundColor: style.Field, borderColor: style.Border, textColor: style.Ink,
            dropdownColor: style.Field, filePath: $"{id}/enum");

        if (next >= 0 && next != current) field.SetValue(Enum.Parse(type, metadata.Names[next]));
    }

    static void DrawNumber(Gui gui, FormField field, Type type, string id)
    {
        var current = Convert.ToDouble(field.GetValue() ?? 0, CultureInfo.InvariantCulture);
        var (min, max) = field.Range ?? (float.MinValue, float.MaxValue);

        if (FormControls.NumberEditor(gui, current, $"{id}/num", FormControls.IsIntegral(type), min, max,
                out var edited))
            field.SetValue(FormControls.ToNumber(edited, type));
    }

    /// <summary>A drawer over one value-drawing routine, laid out as a labelled row.</summary>
    sealed class PrimitiveDrawer(Action<Gui, FormField, Type, string, FormRenderContext> draw, bool numeric = false)
        : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context)
        {
            var type = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
            FormControls.Row(gui, field.Label, id, () => DrawValue(gui, field, id, context), context.Modified(field),
                numeric && !field.IsReadOnly ? () => FormControls.ScrubLabel(gui, field, type) : null,
                RowHeightFor(gui, field));
        }

        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context)
        {
            if (field.IsReadOnly && !ReferenceEquals(this, Summary))
                return Summary.DrawValue(gui, field, id, context);

            draw(gui, field, Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType, id, context);
            return true;
        }
    }
}

/// <summary>Vector2, Vector3 and Vector4 as one row of per-axis scrub fields.</summary>
sealed class VectorDrawer : IPropertyDrawer
{
    internal static readonly VectorDrawer Instance = new();

    public void Draw(Gui gui, FormField field, string id, FormRenderContext context) =>
        FormControls.Row(gui, field.Label, id, () => DrawValue(gui, field, id, context), context.Modified(field));

    public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context)
    {
        var parts = Split(field.GetValue());
        if (FormControls.EditAxes(gui, id, parts)) field.SetValue(Compose(Array.ConvertAll(parts, p => (float)p)));
        return true;
    }

    static double[] Split(object? value) => value switch
    {
        Vector2 v => [v.X, v.Y],
        Vector3 v => [v.X, v.Y, v.Z],
        Vector4 v => [v.X, v.Y, v.Z, v.W],
        _ => [],
    };

    static object Compose(float[] f) => f.Length switch
    {
        2 => new Vector2(f[0], f[1]),
        3 => new Vector3(f[0], f[1], f[2]),
        _ => new Vector4(f[0], f[1], f[2], f[3]),
    };
}

/// <summary>A color as a swatch and four byte fields, one per channel.</summary>
sealed class ColorDrawer : IPropertyDrawer
{
    internal static readonly ColorDrawer Instance = new();

    public void Draw(Gui gui, FormField field, string id, FormRenderContext context) =>
        FormControls.Row(gui, field.Label, id, () => DrawValue(gui, field, id, context), context.Modified(field));

    public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context)
    {
        var style = new FormStyle(gui);
        var color = field.GetValue() is Color value ? value : Color.Black;

        using (gui.Node(style.RowHeight, style.RowHeight, $"{id}/swatch").Enter())
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawBackgroundRect(color, 2);
                gui.DrawRectBorder(gui.CurrentNode.Rect, style.Border, 1, 2);
            }

        Span<int> channels = [color.R, color.G, color.B, color.A];
        var changed = false;
        for (var i = 0; i < channels.Length; i++)
        {
            if (!FormControls.NumberEditor(gui, channels[i], $"{id}/c{i}", integral: true, 0, 255, out var next))
                continue;

            channels[i] = (int)next;
            changed = true;
        }

        if (changed) field.SetValue(Color.FromArgb(channels[3], channels[0], channels[1], channels[2]));
        return true;
    }
}

/// <summary>Shows a <c>[Tooltip]</c> when the field's row is hovered.</summary>
sealed class TooltipDrawer : IAttributeDrawer
{
    internal static readonly TooltipDrawer Instance = new();

    public void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context, Action next)
    {
        using (gui.Node(-1, -1, $"{id}/tooltip").ExpandWidth().Direction(Axis.Vertical).Enter())
        {
            next();
            gui.Tooltip(gui.CurrentNode, ((TooltipAttribute)attribute).Text);
        }
    }
}
