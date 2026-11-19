namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// The bar above the listing: history and parent navigation, the path itself — as breadcrumbs, or
    /// as an editable box once the user clicks the pencil — a new-folder button and the search box.
    /// </summary>
    static readonly string[] CheckedClass = [StyleModifiers.Checked];

    static readonly Icon BackIcon = Icon.FromGlyph("⬅");
    static readonly Icon ForwardIcon = Icon.FromGlyph("➡");
    static readonly Icon UpIcon = Icon.FromGlyph("⬆");
    static readonly Icon RefreshIcon = Icon.FromGlyph("🔄");
    static readonly Icon EditIcon = Icon.FromGlyph("✏");
    static readonly Icon DoneIcon = Icon.FromGlyph("✔");
    static readonly Icon NewFolderIcon = Icon.FromGlyph("➕");
    static readonly Icon HiddenIcon = Icon.FromGlyph("👁");

    static void Toolbar(Gui gui, FileDialogState state, float fontSize)
    {
        var height = RowHeight + 6f;

        using (gui.Node(-1, height).ExpandWidth().Direction(Axis.Horizontal).Gap(4f)
                   .AlignContentY(0.5f).Enter())
        {
            gui.SetTextSize(fontSize);
            HistoryButtons(gui, state, height);
            PathBar(gui, state, height);
            FolderButtons(gui, state, height);
            SearchField(gui, state);
        }
    }

    /// <summary>The back, forward, up and refresh buttons that open the toolbar.</summary>
    static void HistoryButtons(Gui gui, FileDialogState state, float height)
    {
        var browser = state.Browser;
        if (gui.Button(BackIcon.Labeled("Back"), browser.CanGoBack).Width(height).Height(height).Clicked)
        {
            Move(state, () => browser.GoBackAsync());
        }

        if (gui.Button(ForwardIcon.Labeled("Forward"), browser.CanGoForward).Width(height).Height(height).Clicked)
        {
            Move(state, () => browser.GoForwardAsync());
        }

        if (gui.Button(UpIcon.Labeled("Up"), browser.CanGoUp).Width(height).Height(height).Clicked)
        {
            Move(state, async () => await browser.GoUpAsync());
        }

        if (gui.Button(RefreshIcon.Labeled("Refresh")).Width(height).Height(height).Clicked)
        {
            _ = browser.RefreshAsync().AsTask();
        }
    }

    /// <summary>The path edit toggle, the new folder button and the show hidden toggle after the path bar.</summary>
    static void FolderButtons(Gui gui, FileDialogState state, float height)
    {
        var browser = state.Browser;
        var editIcon = state.EditingPath is null ? EditIcon.Labeled("Edit path") : DoneIcon.Labeled("Go to path");
        if (gui.Button(editIcon).Width(height).Height(height).Clicked)
        {
            TogglePathEditing(state);
        }

        if (gui.Button(NewFolderIcon.Labeled("New folder")).Width(height).Height(height).Clicked)
        {
            _ = NewFolderAsync(state).AsTask();
        }

        if (gui.Button(HiddenIcon.Labeled("Show hidden"), classes: browser.ShowHidden ? CheckedClass : null)
                .Width(height).Height(height).Clicked)
        {
            browser.ShowHidden = !browser.ShowHidden;
        }
    }

    /// <summary>The search box at the end of the toolbar, which filters the listing as it changes.</summary>
    static void SearchField(Gui gui, FileDialogState state)
    {
        var browser = state.Browser;
        using (gui.Node(190f, RowHeight + 6f).Enter())
        {
            var search = state.Search;
            gui.TextInput(ref search, placeholder: "Search", id: state.ControlId("search")).Width(190f)
                .Height(RowHeight + 6f);

            if (search != state.Search)
            {
                state.Search = search;
                browser.Search = search;
                state.Selected = null;
            }
        }
    }

    /// <summary>Runs a history or parent navigation, then clears the selection and message it leaves.</summary>
    static void Move(FileDialogState state, Func<ValueTask> navigate)
    {
        _ = navigate().AsTask();
        state.Selected = null;
        state.Message = null;
    }

    /// <summary>The path as clickable crumbs, or as a text box while it is being edited.</summary>
    static void PathBar(Gui gui, FileDialogState state, float height)
    {
        using (gui.Node().Expand().Enter())
        {
            if (state.EditingPath is { } editing)
            {
                gui.TextInput(ref editing, placeholder: "Path", grabFocus: true, id: state.ControlId("path"))
                    .Width(Size.Expand()).Height(height);
                state.EditingPath = editing;
                return;
            }

            gui.ClipContent();
            gui.ScrollX();
            gui.Breadcrumb(Crumbs(state, Open)).Height(height);
        }

        void Open(string path)
        {
            _ = state.Browser.NavigateAsync(path).AsTask();
            state.Selected = null;
            state.Message = null;
        }
    }

    /// <summary>How many trailing path segments the crumb bar shows before it starts eliding.</summary>
    const int VisibleCrumbs = 4;

    /// <summary>
    /// The crumb trail, cut to its last <see cref="VisibleCrumbs"/> segments. A deep path would
    /// otherwise overflow the bar and push the folder the user is actually in out of sight; the
    /// leading "…" steps back into what was dropped, and the whole trail is still there to scroll.
    /// </summary>
    static IReadOnlyList<BreadcrumbItem> Crumbs(FileDialogState state, Action<string> open)
    {
        var crumbs = state.Browser.Breadcrumbs();
        var items = new List<BreadcrumbItem>();

        if (crumbs.Count > VisibleCrumbs)
        {
            var elided = crumbs[^(VisibleCrumbs + 1)];
            items.Add(new BreadcrumbItem("…", () => open(elided.Path)));
        }

        foreach (var crumb in crumbs.TakeLast(VisibleCrumbs))
        {
            items.Add(new BreadcrumbItem(crumb.Label, () => open(crumb.Path),
                IsCurrent: crumb.Path == state.Browser.CurrentPath));
        }

        return items;
    }

    /// <summary>
    /// Switches the path bar between crumbs and a text box. Leaving the box navigates to what it
    /// holds, and says so when that path does not exist rather than silently staying put.
    /// </summary>
    static void TogglePathEditing(FileDialogState state)
    {
        if (state.EditingPath is not { } typed)
        {
            state.EditingPath = state.Browser.CurrentPath;
            return;
        }

        state.EditingPath = null;
        state.Message = null;
        if (typed.Trim().Length == 0)
        {
            return;
        }

        _ = NavigateTypedAsync(state, typed.Trim()).AsTask();
    }

    /// <summary>
    /// Creates a folder under the current one and selects it, so the user can rename it by typing in
    /// the name field or step into it. Names it the way a file manager does when the name is taken.
    /// </summary>
    static async ValueTask NavigateTypedAsync(FileDialogState state, string path)
    {
        if (!await state.Browser.NavigateAsync(path).ConfigureAwait(false))
        {
            state.Message = $"'{path}' is not a folder.";
        }
        else
        {
            state.Selected = null;
        }
    }

    static async ValueTask NewFolderAsync(FileDialogState state)
    {
        var browser = state.Browser;
        var name = "New Folder";
        for (var suffix = 2;
             await browser.FileSystem.DirectoryExistsAsync(Path.Combine(browser.CurrentPath, name)).ConfigureAwait(false);
             suffix++)
        {
            name = $"New Folder {suffix}";
        }

        state.Message = null;
        if (await browser.CreateFolderAsync(name).ConfigureAwait(false) is not { } created)
        {
            state.Message = browser.Error;
            return;
        }

        state.Selected = browser.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullPath, created, StringComparison.Ordinal));

        if (state.Request?.Mode is FileDialogMode.SaveFile or FileDialogMode.CreateFolder)
        {
            state.Name = name;
        }
    }
}
