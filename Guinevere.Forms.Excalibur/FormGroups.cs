namespace Guinevere.Forms;

/// <summary>Foldable groups: collections of entries and nested objects, drawn with the same field drawers.</summary>
static class FormGroups
{
    static readonly ListDrawerSettingsAttribute DefaultListSettings = new();

    /// <summary>Keeps entry drags apart from every other drag payload in the frame.</summary>
    static readonly DragDropTag EntryTag = new("guinevere.forms.entry");

    /// <summary>A dragged entry: the collection it belongs to and its index there.</summary>
    sealed record EntryDrag(string CollectionId, int Index);

    /// <summary>The slice of a collection on screen: the page, how many pages, and the entry range.</summary>
    internal readonly record struct PageView(int Page, int Count, int Start, int End);

    /// <summary>
    /// A list, array or dictionary as a foldable group of rows: entry count, page controls and an add button in
    /// the heading; a drag handle and a remove button per entry when the collection allows them. List entries
    /// hide their index labels unless <c>[ListDrawerSettings(ShowIndexLabels = true)]</c>; dictionary keys stay.
    /// </summary>
    internal static void DrawCollection(Gui gui, FormField field, CollectionField collection, string id,
        FormRenderContext context)
    {
        var settings = field.Attribute<ListDrawerSettingsAttribute>() ?? DefaultListSettings;
        var entries = collection.Entries();
        var view = Paging(entries.Count, settings, context.Pages, id);
        var isOpen = Heading(gui, collection.Label, id, context.Collapsed, context.Modified(field),
            actions: () => HeaderActions(gui, collection, entries.Count, view, settings, id, context));
        if (!isOpen) return;

        var draggable = settings.DraggableItems && collection.CanReorder;
        var hideLabels = !settings.ShowIndexLabels && !collection.IsDictionary;
        for (var i = view.Start; i < view.End; i++)
            if (DrawEntry(gui, collection, entries[i], i, id, context, draggable, hideLabels))
                return;
    }

    /// <summary>The page to show, clamped to the pages that exist; one page holding everything without paging.</summary>
    internal static PageView Paging(int count, ListDrawerSettingsAttribute settings, IDictionary<string, int> pages,
        string id)
    {
        if (!settings.ShowPaging) return new PageView(0, 1, 0, count);

        var size = Math.Max(1, settings.NumberOfItemsPerPage);
        var pageCount = Math.Max(1, (count + size - 1) / size);
        var page = Math.Clamp(pages.TryGetValue(id, out var stored) ? stored : 0, 0, pageCount - 1);
        return new PageView(page, pageCount, page * size, Math.Min(count, (page + 1) * size));
    }

    static void HeaderActions(Gui gui, CollectionField collection, int count, PageView view,
        ListDrawerSettingsAttribute settings, string id, FormRenderContext context)
    {
        HeaderText(gui, $"{count}", $"{id}/count");
        if (view.Count > 1) PageControls(gui, view, id, context.Pages);

        // A new entry lands at the end, so follow it to the last page.
        if (collection.CanResize && FormControls.SmallButton(gui, "+", $"{id}/add") && collection.Add())
            context.Pages[id] = count / Math.Max(1, settings.NumberOfItemsPerPage);
    }

    /// <summary>Previous and next buttons around "page/pages"; each click turns one page.</summary>
    static void PageControls(Gui gui, PageView view, string id, IDictionary<string, int> pages)
    {
        if (FormControls.SmallButton(gui, "<", $"{id}/prev") && view.Page > 0) pages[id] = view.Page - 1;
        HeaderText(gui, $"{view.Page + 1}/{view.Count}", $"{id}/page");
        if (FormControls.SmallButton(gui, ">", $"{id}/next") && view.Page < view.Count - 1) pages[id] = view.Page + 1;
    }

    static void HeaderText(Gui gui, string text, string id)
    {
        var style = new FormStyle(gui);
        using (gui.Node(style.FontSize * (text.Length * 0.6f + 1f), style.RowHeight, id).ContentAlignY(0.5f).Enter())
            gui.DrawText(text, style.FontSize - 1f, style.InkDim);
    }

    /// <summary>One entry row: optional drag handle, the entry's own drawer, optional remove button.</summary>
    /// <returns>True when the entry was removed, which shifts every later entry, so the rest of this frame's
    /// rows no longer address what they were built for.</returns>
    static bool DrawEntry(Gui gui, CollectionField collection, FormField entry, int index, string id,
        FormRenderContext context, bool draggable, bool hideLabel)
    {
        var entryId = $"{id}/entry{index}";
        using (gui.Node(-1, -1, entryId).ExpandWidth().Direction(Axis.Horizontal).Gap(4f).Enter())
        {
            if (draggable) DragHandle(gui, new EntryDrag(id, index), $"{entryId}/handle");

            using (gui.Node(-1, -1, $"{entryId}/slot").Expand().Direction(Axis.Vertical).Enter())
                FormRenderer.FormEntry(gui, entry, entryId, context, hideLabel);

            if (draggable) AcceptDrop(gui, collection, id, index);
            if (!collection.CanResize || !FormControls.SmallButton(gui, "-", $"{entryId}/remove")) return false;

            collection.RemoveAt(index);
            return true;
        }
    }

    /// <summary>A grip that starts dragging its entry; drawn as three short bars.</summary>
    static void DragHandle(Gui gui, EntryDrag payload, string id)
    {
        var style = new FormStyle(gui);
        using (gui.Node(style.FontSize, style.RowHeight, id).Enter())
        {
            gui.DragSource(id, payload, tag: EntryTag, keyboard: false);
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var width = rect.W * 0.6f;
            for (var bar = -1; bar <= 1; bar++)
                gui.DrawRect(new Rect(rect.X + (rect.W - width) / 2f, rect.Y + rect.H / 2f + bar * 3.5f - 0.75f, width,
                    1.5f), style.InkDim);
        }
    }

    /// <summary>Makes the current entry row accept another entry of the same collection, moving it here once.</summary>
    static void AcceptDrop(Gui gui, CollectionField collection, string id, int index)
    {
        var drop = gui.DropTarget<EntryDrag>($"{id}/entry{index}/drop", EntryTag,
            drag => drag.CollectionId == id && drag.Index != index, keyboard: false);

        if (drop.IsAccepted) gui.DrawDropIndicator(drop.State);
        if (drop.IsDropped) collection.Move(drop.Payload!.Index, index);
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
