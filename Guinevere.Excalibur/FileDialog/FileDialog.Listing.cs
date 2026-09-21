namespace Guinevere;

public static partial class ControlsExtensions
{
    const int listingOverscan = 4;
    /// <summary>Width of the size column.</summary>
    const float sizeWidth = 90f;

    /// <summary>Width of the modified column.</summary>
    const float modifiedWidth = 132f;

    /// <summary>
    /// The directory listing: a sortable header over the rows. A click selects, a double click opens
    /// a folder or accepts a file.
    /// </summary>
    static void Listing(Gui gui, FileDialogState state, float fontSize)
    {
        var palette = gui.Controls;

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawBackgroundRect(palette.Surface, 4f);
                gui.DrawRectBorder(gui.CurrentNode.Rect, palette.Border, 1f, 4f);
            }

            ListingHeader(gui, state, fontSize);

            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(1f).Padding(4f).Enter())
            {
                gui.ScrollY();

                if (state.Browser.Entries.Count == 0)
                {
                    using (gui.Node(-1, rowHeight).ExpandWidth().ContentAlignY(0.5f).Enter())
                        gui.DrawText(state.Browser.IsLoading ? "Loading…" :
                                state.Browser.Error is null ? "Nothing here" : "Cannot read this folder",
                            fontSize, palette.TextDim, centerInRect: false);

                    return;
                }

                var entries = state.Browser.Entries;
                var first = Math.Max(0, (int)(state.ListingScrollY / (rowHeight + 1f)) - listingOverscan);
                var take = (int)(state.ListingViewportHeight / (rowHeight + 1f)) + (listingOverscan * 2) + 1;
                var last = Math.Min(entries.Count, first + take);

                ListingSpacer(gui, state.ControlId("pad-top"), first * (rowHeight + 1f));
                for (var i = first; i < last; i++) Row(gui, state, entries[i], fontSize);
                ListingSpacer(gui, state.ControlId("pad-bottom"), (entries.Count - last) * (rowHeight + 1f));

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
        var palette = gui.Controls;

        using (gui.Node(-1, rowHeight).ExpandWidth().Direction(Axis.Horizontal).Gap(6f)
                   .PaddingX(8f).ContentAlignY(0.5f).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
                gui.DrawRect(new Rect(gui.CurrentNode.Rect.X, gui.CurrentNode.Rect.Y + rowHeight - 1f,
                    gui.CurrentNode.Rect.W, 1f), palette.Border);

            Column(gui, state, FileSortColumn.Name, "Name", 0f, fontSize);
            Column(gui, state, FileSortColumn.Size, "Size", sizeWidth, fontSize);
            Column(gui, state, FileSortColumn.Modified, "Modified", modifiedWidth, fontSize);
        }
    }

    /// <summary>One header cell. Clicking it sorts by that column, or reverses an order already on it.</summary>
    static void Column(Gui gui, FileDialogState state, FileSortColumn column, string label,
        float width, float fontSize)
    {
        var palette = gui.Controls;
        var active = state.Browser.Sort == column;
        var node = width > 0f ? gui.Node(width, rowHeight, state.ControlId($"column/{column}"))
            : gui.Node(-1, rowHeight, state.ControlId($"column/{column}")).Expand();

        using (node.ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();
            var marker = active ? state.Browser.Ascending ? " ⬆" : " ⬇" : "";

            gui.DrawText(label + marker, fontSize - 1f,
                active || interactable.OnHover() ? palette.Text : palette.TextDim, centerInRect: false);

            if (gui.Pass == Pass.Pass2Render && interactable.OnClick()) state.Browser.SortBy(column);
        }
    }

    static void Row(Gui gui, FileDialogState state, FileEntry entry, float fontSize)
    {
        var palette = gui.Controls;
        var selected = state.Selected?.FullPath == entry.FullPath;

        using (gui.Node(-1, rowHeight, state.ControlId($"row/{entry.FullPath}")).ExpandWidth()
                   .Direction(Axis.Horizontal).Gap(6f).PaddingX(4f).ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();

            if (gui.Pass == Pass.Pass2Render)
            {
                if (selected) gui.DrawBackgroundRect(palette.Selected, 3f);
                else if (interactable.OnHover()) gui.DrawBackgroundRect(palette.SurfaceHover, 3f);
            }

            gui.DrawText(entry.IsDirectory ? "📁" : "📄", fontSize, palette.TextDim);

            using (gui.Node().Expand().ContentAlignY(0.5f).Enter())
            {
                gui.ClipContent();
                gui.DrawText(entry.Name, fontSize, palette.Text, centerInRect: false, clip: true);
            }

            using (gui.Node(sizeWidth, rowHeight).ContentAlignY(0.5f).Enter())
                gui.DrawText(global::Guinevere.FileBrowser.FormatSize(entry.Size), fontSize - 1f, palette.TextDim,
                    centerInRect: false);

            using (gui.Node(modifiedWidth, rowHeight).ContentAlignY(0.5f).Enter())
                gui.DrawText(Modified(entry), fontSize - 1f, palette.TextDim, centerInRect: false);

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
