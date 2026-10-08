namespace Guinevere;

public static partial class ControlsExtensions
{
    const int ListingOverscan = 4;
    /// <summary>Width of the size column.</summary>
    const float SizeWidth = 90f;

    /// <summary>Width of the modified column.</summary>
    const float ModifiedWidth = 132f;

    /// <summary>
    /// The directory listing: a sortable header over the rows. A click selects, a double click opens
    /// a folder or accepts a file.
    /// </summary>
    static void Listing(Gui gui, FileDialogState state, float fontSize)
    {

        using (gui.StyledNode("file-list").Expand().Direction(Axis.Vertical).Enter())
        {

            ListingHeader(gui, state, fontSize);

            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(1f).Padding(4f).Enter())
            {
                gui.ScrollY();

                if (state.Browser.Entries.Count == 0)
                {
                    using (gui.StyledNode("file-label").Height(RowHeight).ExpandWidth().ContentAlignY(0.5f).Enter())
                        gui.DrawText(state.Browser.IsLoading ? "Loading…" :
                                state.Browser.Error is null ? "Nothing here" : "Cannot read this folder",
                            fontSize, gui.ResolvePart("file-detail").GetColor("color"), centerInRect: false);

                    return;
                }

                var entries = state.Browser.Entries;
                var first = Math.Max(0, (int)(state.ListingScrollY / (RowHeight + 1f)) - ListingOverscan);
                var take = (int)(state.ListingViewportHeight / (RowHeight + 1f)) + (ListingOverscan * 2) + 1;
                var last = Math.Min(entries.Count, first + take);

                ListingSpacer(gui, state.ControlId("pad-top"), first * (RowHeight + 1f));
                for (var i = first; i < last; i++) Row(gui, state, entries[i], fontSize);
                ListingSpacer(gui, state.ControlId("pad-bottom"), (entries.Count - last) * (RowHeight + 1f));

                if (gui.Pass == Pass.Pass2Render)
                {
                    state.ListingViewportHeight = Math.Max(1f, gui.CurrentNode.Rect.H);
                    state.ListingScrollY = gui.GetScrollState(gui.CurrentNode.Id)?.ScrollOffset.Y ?? 0f;
                }
            }
        }
    }

    static void ListingSpacer(Gui gui, string id, float height)
    {
        if (height <= 0f) return;
        using (gui.Node(-1, height, id).ExpandWidth().Enter()) { }
    }

    static void ListingHeader(Gui gui, FileDialogState state, float fontSize)
    {

        using (gui.StyledNode("file-header").Height(RowHeight).ExpandWidth().Direction(Axis.Horizontal).Gap(6f)
                   .PaddingX(8f).ContentAlignY(0.5f).Enter())
        {

            Column(gui, state, FileSortColumn.Name, "Name", 0f, fontSize);
            Column(gui, state, FileSortColumn.Size, "Size", SizeWidth, fontSize);
            Column(gui, state, FileSortColumn.Modified, "Modified", ModifiedWidth, fontSize);
        }
    }

    /// <summary>One header cell. Clicking it sorts by that column, or reverses an order already on it.</summary>
    static void Column(Gui gui, FileDialogState state, FileSortColumn column, string label,
        float width, float fontSize)
    {
        var active = state.Browser.Sort == column;
        var node = gui.StyledNode("file-column", id: state.ControlId($"column/{column}"),
            modifiers: active ? ["selected"] : []).Height(RowHeight);
        if (width > 0f) node.Width(width);
        else node.Expand();

        using (node.ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();
            var marker = active ? state.Browser.Ascending ? " ⬆" : " ⬇" : "";

            gui.DrawText(label + marker, fontSize - 1f,
                centerInRect: false);

            if (gui.Pass == Pass.Pass2Render && interactable.OnClick()) state.Browser.SortBy(column);
        }
    }

    static void Row(Gui gui, FileDialogState state, FileEntry entry, float fontSize)
    {
        var selected = state.Selected?.FullPath == entry.FullPath;

        using (gui.StyledNode("file-row", id: state.ControlId($"row/{entry.FullPath}"),
                       modifiers: selected ? ["selected"] : []).Height(RowHeight).ExpandWidth()
                   .Direction(Axis.Horizontal).Gap(6f).PaddingX(4f).ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();


            gui.DrawText(entry.IsDirectory ? WidgetIcons.Folder : WidgetIcons.FileLines, fontSize, gui.ResolvePart("file-detail").GetColor("color"));

            using (gui.Node().Expand().ContentAlignY(0.5f).Enter())
            {
                gui.ClipContent();
                gui.DrawText(entry.Name, fontSize, centerInRect: false, clip: true);
            }

            using (gui.Node(SizeWidth, RowHeight).ContentAlignY(0.5f).Enter())
                gui.DrawText(Guinevere.FileBrowser.FormatSize(entry.Size), fontSize - 1f, gui.ResolvePart("file-detail").GetColor("color"),
                    centerInRect: false);

            using (gui.Node(ModifiedWidth, RowHeight).ContentAlignY(0.5f).Enter())
                gui.DrawText(Modified(entry), fontSize - 1f, gui.ResolvePart("file-detail").GetColor("color"), centerInRect: false);

            if (gui.Pass != Pass.Pass2Render || !interactable.OnClick(out var clicks)) return;

            Select(state, entry);
            if (clicks >= 2) Activate(state, entry);
        }
    }

    /// <summary>
    /// Takes the clicked row as the selection. Clicking an existing file in a save dialog also fills
    /// the name field with it, which is how every save dialog behaves.
    /// </summary>
    static void Select(FileDialogState state, FileEntry entry)
    {
        state.Selected = entry;
        state.Message = null;

        if (!entry.IsDirectory && state.Request?.Mode == FileDialogMode.SaveFile) state.Name = entry.Name;
    }

    /// <summary>The last write time, short enough for the column and unambiguous between locales.</summary>
    static string Modified(FileEntry entry) =>
        entry.Modified == DateTime.MinValue
            ? ""
            : entry.Modified.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
