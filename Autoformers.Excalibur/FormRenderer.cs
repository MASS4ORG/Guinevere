namespace Autoformers;

/// <summary>
/// Draws form models, sections and fields with Excalibur controls. Call from the same call site in both passes:
/// drawers build identical nodes in each and apply input only in the render pass.
/// </summary>
public static class FormRenderer
{
    sealed record SummaryMetadata(MemberMetadata? Name, MemberMetadata? Path);

    static readonly ConditionalWeakTable<Type, SummaryMetadata> SummaryMetadataByType = [];

    /// <summary>The context used when a caller passes none. Its fold state is shared by every such caller.</summary>
    static readonly FormRenderContext Fallback = new();

    /// <summary>
    /// Draws every section of a model: a heading (with the section's enabled switch, if any), its body fields and
    /// its <c>[Button]</c> actions.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="model">The form to draw.</param>
    /// <param name="id">Stable id, unique among siblings.</param>
    /// <param name="context">Drawers, fold state and host rules; keep one per panel.</param>
    public static void Form(this Gui gui, FormModel model, string id, FormRenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(model);
        context ??= Fallback;

        using (gui.Node(-1, -1, id).ExpandWidth().Direction(Axis.Vertical).Gap(2f).Enter())
            for (var i = 0; i < model.Sections.Count; i++)
                DrawSection(gui, model.Sections[i], $"{id}/s{i}", context);
    }

    /// <summary>Draws one field as a labelled row, or as a group for collections and nested objects.</summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="field">The value to edit.</param>
    /// <param name="id">Stable id, unique among siblings.</param>
    /// <param name="context">Drawers, fold state and host rules; keep one per panel.</param>
    public static void FormField(this Gui gui, FormField field, string id, FormRenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(field);
        var resolved = context ?? Fallback;

        Decorate(gui, field, id, resolved, () => DrawProperty(gui, field, id, resolved));
    }

    /// <summary>Draws only a field's editor, for a caller that lays out the label itself.</summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="field">The value to edit.</param>
    /// <param name="id">Stable id, unique among siblings.</param>
    /// <param name="context">Drawers and host rules; its <see cref="FormRenderContext.Translate"/> applies to enums.</param>
    public static void FormFieldEditor(this Gui gui, FormField field, string id, FormRenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(field);
        var resolved = context ?? Fallback;

        Decorate(gui, field, id, resolved, () => DrawEditor(gui, field, id, resolved));
    }

    static void DrawSection(Gui gui, FormSection section, string id, FormRenderContext context)
    {
        var style = new FormStyle(gui);
        using (gui.Node(-1, style.RowHeight, $"{id}/head").ExpandWidth().Direction(Axis.Horizontal).Gap(6f)
                   .ContentAlignY(0.5f).Enter())
        {
            if (section.EnabledField is { } enabled) gui.FormFieldEditor(enabled, $"{id}/enabled", context);
            gui.DrawText(section.Title, style.FontSize, style.Ink, centerInRect: false,
                effects: FormControls.Emphasis(true, style.Ink));
        }

        var fields = section.BodyFields;
        for (var i = 0; i < fields.Count; i++)
            gui.FormField(fields[i], $"{id}/f{i}", context);

        for (var i = 0; i < section.Buttons.Count; i++)
        {
            var button = section.Buttons[i];
            if (FormControls.TextButton(gui, button.Label, $"{id}/b{i}", button.IsEnabled)) button.Invoke();
        }
    }

    /// <summary>Wraps <paramref name="inner"/> in the decorators registered for the field's attributes.</summary>
    static void Decorate(Gui gui, FormField field, string id, FormRenderContext context, Action inner)
    {
        if (field.Metadata?.Attributes is not { Count: > 0 } attributes)
        {
            inner();
            return;
        }

        var chain = attributes
            .Select((attribute, index) => (attribute, index, drawer: context.Drawers.AttributeDrawer(attribute.GetType())))
            .Where(entry => entry.drawer is not null)
            .OrderBy(entry => entry.drawer!.Order)
            .ThenBy(entry => entry.index)
            .ToArray();

        var next = inner;
        for (var i = chain.Length - 1; i >= 0; i--)
        {
            var (attribute, _, drawer) = chain[i];
            var continuation = next;
            next = () => drawer!.Draw(gui, field, attribute, id, context, continuation);
        }

        next();
    }

    /// <summary>
    /// Dispatch: writable type drawer, predicate drawer, collection, nested object, built-in, read-only summary.
    /// A <c>[HideLabel]</c> field draws its value across the whole row; groups keep their heading.
    /// </summary>
    static void DrawProperty(Gui gui, FormField field, string id, FormRenderContext context, bool hideLabel = false)
    {
        var drawer = RegisteredDrawer(field, context.Drawers);
        if (drawer is null && TryDrawGroup(gui, field, id, context)) return;

        drawer ??= field.IsReadOnly ? BuiltinDrawers.Summary : BuiltinDrawers.For(field.ValueType);
        if (!(hideLabel || HidesLabel(field)) || !DrawWithoutLabel(gui, drawer, field, id, context))
            drawer.Draw(gui, field, id, context);
    }

    /// <summary>Draws a collection entry like <see cref="FormField(Gui, Autoformers.FormField, string, FormRenderContext?)"/>, optionally without its label.</summary>
    internal static void FormEntry(Gui gui, FormField field, string id, FormRenderContext context, bool hideLabel) =>
        Decorate(gui, field, id, context, () => DrawProperty(gui, field, id, context, hideLabel));

    /// <summary>The type drawer when the field is writable, else the first matching predicate drawer.</summary>
    static IPropertyDrawer? RegisteredDrawer(FormField field, FormDrawers drawers) =>
        (field.IsReadOnly ? null : drawers.TypeDrawer(field.ValueType)) ?? drawers.PredicateDrawer(field);

    static bool HidesLabel(FormField field) => field.Metadata?.GetAttribute<HideLabelAttribute>() is not null;

    /// <summary>Draws a collection or an inlined nested object; false when the field is neither.</summary>
    static bool TryDrawGroup(Gui gui, FormField field, string id, FormRenderContext context)
    {
        if (CollectionField.TryCreate(field) is { } collection)
            FormGroups.DrawCollection(gui, field, collection, id, context);
        else if (Inline(field, context) is { } target)
            FormGroups.DrawNested(gui, field, target, id, context);
        else
            return false;

        return true;
    }

    /// <summary>The value alone across the row; false when the drawer has no value-only form.</summary>
    static bool DrawWithoutLabel(Gui gui, IPropertyDrawer drawer, FormField field, string id, FormRenderContext context)
    {
        using (gui.Node(-1, -1, $"{id}/value").ExpandWidth().Direction(Axis.Horizontal).Gap(4f).Enter())
            return drawer.DrawValue(gui, field, id, context);
    }

    static void DrawEditor(Gui gui, FormField field, string id, FormRenderContext context)
    {
        if (field.IsReadOnly)
        {
            BuiltinDrawers.Summary.DrawValue(gui, field, id, context);
            return;
        }

        var builtin = BuiltinDrawers.For(field.ValueType);
        var drawer = context.Drawers.TypeDrawer(field.ValueType) ?? builtin;
        if (!drawer.DrawValue(gui, field, id, context)) builtin.DrawValue(gui, field, id, context);
    }

    /// <summary>
    /// The object a field holds when it is a plain settings-style object worth expanding in place, rather than a
    /// value with an editor of its own or a type the host excludes.
    /// </summary>
    static object? Inline(FormField field, FormRenderContext context)
    {
        var type = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;

        if (IsPlainValue(type) || context.CanInline?.Invoke(type) == false) return null;
        return FormBuilder.EditableMembers(type).Count == 0 ? null : field.GetValue();
    }

    /// <summary>A primitive, enum, string, decimal or System type: a value with an editor, never a nested form.</summary>
    static bool IsPlainValue(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true;

    /// <summary>
    /// A nested object's heading: its own <c>Name</c> or <c>Path</c> when it has one, so a list of objects reads
    /// as what it holds; otherwise the field's label. A throwing getter is reported, not propagated.
    /// </summary>
    internal static string Summary(FormField field, object target)
    {
        var members = SummaryMetadataByType.GetValue(target.GetType(), static type =>
            new SummaryMetadata(SummaryMember(type, "Name"), SummaryMember(type, "Path")));

        foreach (var member in (ReadOnlySpan<MemberMetadata?>)[members.Name, members.Path])
        {
            if (member is null) continue;
            try
            {
                if (member.GetValue(target) is string { Length: > 0 } text) return text;
            }
            catch (Exception ex)
            {
                field.Options.FailureReporter?.Invoke(
                    new FormFailure(FormFailureKind.Read, target, member.Member.Name, ex));
            }
        }

        return field.Label;
    }

    static MemberMetadata? SummaryMember(Type type, string name) =>
        type.GetProperty(name) is { PropertyType: var propertyType, GetMethod: not null } property
        && propertyType == typeof(string)
            ? MemberMetadata.For(property)
            : null;
}
