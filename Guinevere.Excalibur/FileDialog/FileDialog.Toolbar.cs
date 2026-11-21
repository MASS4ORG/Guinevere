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
        if (gui.Button(editIcon, id: state.ControlId(PathToggleId)).Width(height).Height(height).Clicked)
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

    /// <summary>
    /// The path as clickable crumbs, or as a text box while it is being edited. Clicking the bar's empty area
    /// starts editing, as it does in a file manager.
    /// </summary>
    static void PathBar(Gui gui, FileDialogState state, float height)
    {
        using (gui.Node().Expand().Enter())
        {
            if (state.EditingPath is not null)
            {
                PathBox(gui, state, height);
                return;
            }

            EndPathEdit(state);
            var bar = gui.GetInteractable();
            gui.ClipContent();
            gui.ScrollX();
            var crumbs = gui.Breadcrumb(Crumbs(state, Open)).Height(height);

            if (gui.Pass == Pass.Pass2Render && bar.OnClick() && !crumbs.Rect.Contains(gui.Input.MousePosition))
            {
                BeginPathEdit(state);
            }
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
    /// Creates a folder under the current one and selects it, so the user can rename it by typing in
    /// the name field or step into it. Names it the way a file manager does when the name is taken.
    /// </summary>
    internal static async ValueTask NewFolderAsync(FileDialogState state)
    {
        var browser = state.Browser;
        var parent = browser.CurrentPath;
        var name = "New Folder";
        for (var suffix = 2;
             await browser.FileSystem.DirectoryExistsAsync(Path.Combine(parent, name)).ConfigureAwait(false);
             suffix++)
        {
            name = $"New Folder {suffix}";
        }

        var created = await browser.CreateFolderAsync(name).ConfigureAwait(false);
        await state.ToUiThread();

        state.Message = null;
        if (created is null)
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
