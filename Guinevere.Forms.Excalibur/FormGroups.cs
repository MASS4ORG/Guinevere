namespace Guinevere.Forms;

/// <summary>Foldable groups: collections of entries and nested objects, drawn with the same field drawers.</summary>
static class FormGroups
{
    /// <summary>
    /// A list, array or dictionary as a foldable group of rows, with an entry count and, when resizable, add and
    /// remove buttons.
    /// </summary>
    internal static void DrawCollection(Gui gui, FormField field, CollectionField collection, string id,
        FormRenderContext context)
    {
        var style = new FormStyle(gui);
        var entries = collection.Entries();
        var isOpen = Heading(gui, collection.Label, id, context.Collapsed, context.Modified(field), actions: () =>
        {
            gui.DrawText($"{entries.Count}", style.FontSize - 1f, style.InkDim, centerInRect: false);
            if (collection.CanResize && FormControls.SmallButton(gui, "+", $"{id}/add")) collection.Add();
        });

        if (!isOpen) return;

        for (var i = 0; i < entries.Count; i++)
        {
            using (gui.Node(-1, -1, $"{id}/entry{i}").ExpandWidth().Direction(Axis.Horizontal).Gap(4f).Enter())
            {
                using (gui.Node(-1, -1, $"{id}/entry{i}/value").Expand().Direction(Axis.Vertical).Enter())
                    gui.FormField(entries[i], $"{id}/entry{i}", context);

                // Removing shifts every later entry, so the rest of this frame's handles no longer address
                // what they were built for.
                if (collection.CanResize && FormControls.SmallButton(gui, "-", $"{id}/entry{i}/remove"))
                {
                    collection.RemoveAt(i);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// A nested object as a compartment: a bordered panel holding a fold heading and, when open, the object's own
    /// members indented below it. Panels alternate their fill with nesting depth so boxes inside boxes stay
    /// distinct. The form inherits the field's options, so null rules, failure reporting and read-only state carry
    /// through; a struct is written back to its owner after each edit, since the form edits a boxed copy.
    /// </summary>
    internal static void DrawNested(Gui gui, FormField field, object target, string id, FormRenderContext context)
    {
        // Read from the parent: the box keeps its scope across both passes, so depth read inside would grow.
        var style = new FormStyle(gui);
        var depth = style.Depth;
        var padding = style.CompartmentPadding;

        using (gui.Node(-1, -1, $"{id}/box").ExpandWidth().Direction(Axis.Vertical).Gap(2f)
                   .Padding(padding, padding, padding, padding).Enter())
        {
            DrawCompartment(gui, style, depth);
            var isOpen = Heading(gui, FormRenderer.Summary(field, target), id, context.Collapsed,
                context.Modified(field), actions: null);
            if (!isOpen) return;

            var fields = FormBuilder.Build(target, NestedOptions(field, target)).Sections
                .SelectMany(section => section.BodyFields).ToList();

            using (gui.Node(-1, -1, $"{id}/body").ExpandWidth().Direction(Axis.Vertical)
                       .Margin(style.Indent, 0f, 0f, 0f).Enter())
            {
                gui.CurrentNodeScope.Set(ControlStyles.Value<FormNestingDepth, int>(depth + 1));
                for (var i = 0; i < fields.Count; i++)
                    gui.FormField(fields[i], $"{id}/f{i}", context);
            }
        }
    }

    /// <summary>The panel behind a compartment, slightly toward the text color, alternating with depth.</summary>
    internal static Color CompartmentFill(Color background, Color ink, int depth) =>
        GUIColorDrawer.Blend(background, ink, depth % 2 == 0 ? 0.04f : 0.08f);

    static void DrawCompartment(Gui gui, FormStyle style, int depth)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        gui.DrawBackgroundRect(CompartmentFill(style.Background, style.Ink, depth), style.CornerRadius);
        gui.DrawRectBorder(gui.CurrentNode.Rect, style.Divider, 1, style.CornerRadius);
    }

    static FormOptions NestedOptions(FormField field, object target) => field.Options with
    {
        ReadOnly = field.Options.ReadOnly || field.IsReadOnly,
        MutationNotifier = target.GetType().IsValueType ? boxed => field.SetValue(boxed) : _ => field.Touch(),
    };

    /// <summary>A clickable fold heading: arrow and title in the label column, optional actions beside it.</summary>
    /// <returns>Whether the group is open.</returns>
    static bool Heading(Gui gui, string title, string id, ISet<string> collapsed, bool modified, Action? actions)
    {
        var style = new FormStyle(gui);
        var isOpen = !collapsed.Contains(id);

        using (gui.Node(-1, style.RowHeight, $"{id}/head").ExpandWidth().Direction(Axis.Horizontal).Gap(6f).Enter())
        {
            FormControls.MarkModified(gui, modified);
            using (gui.Node(style.LabelWidth, style.RowHeight, $"{id}/head/label").Direction(Axis.Horizontal).Gap(2f)
                       .Enter())
            {
                if (gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick() && !collapsed.Add(id))
                    collapsed.Remove(id);

                using (gui.Node(10f, style.RowHeight, $"{id}/head/arrow").ContentAlignX(0.5f).ContentAlignY(0.5f)
                           .Enter())
                    FormControls.FoldArrow(gui, isOpen);

                gui.DrawText(title, style.FontSize, style.Ink, centerInRect: false,
                    effects: FormControls.Emphasis(modified, style.Ink));
            }

            if (actions is not null)
                using (gui.Node(-1, style.RowHeight, $"{id}/head/actions").Expand().Direction(Axis.Horizontal).Gap(4f)
                           .ContentAlignY(0.5f).Enter())
                    actions();
        }

        return isOpen;
    }
}
