namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// The bar above the listing: history and parent navigation, the path itself — as breadcrumbs, or
    /// as an editable box once the user clicks the pencil — a new-folder button and the search box.
    /// </summary>
    static void Toolbar(Gui gui, FileDialogState state, float fontSize)
    {
        var browser = state.Browser;
        var height = rowHeight + 6f;

        using (gui.Node(-1, height).ExpandWidth().Direction(Axis.Horizontal).Gap(4f)
                   .ContentAlignY(0.5f).Enter())
        {
            if (gui.IconButton("⬅", size: height, fontSize: fontSize, enabled: browser.CanGoBack))
                Move(() => browser.GoBackAsync());

            if (gui.IconButton("➡", size: height, fontSize: fontSize, enabled: browser.CanGoForward))
                Move(() => browser.GoForwardAsync());

            if (gui.IconButton("⬆", size: height, fontSize: fontSize, enabled: browser.CanGoUp))
                Move(async () => await browser.GoUpAsync());

            if (gui.IconButton("🔄", size: height, fontSize: fontSize)) _ = browser.RefreshAsync();

            PathBar(gui, state, height, fontSize);

            if (gui.IconButton(state.EditingPath is null ? "✏" : "✔", size: height, fontSize: fontSize))
                TogglePathEditing(state);

            if (gui.IconButton("➕", size: height, fontSize: fontSize)) _ = NewFolderAsync(state);

            if (gui.IconButton("👁", size: height, fontSize: fontSize,
                    color: browser.ShowHidden ? gui.Controls.Accent : null))
                browser.ShowHidden = !browser.ShowHidden;

            using (gui.Node(190f, height).Enter())
            {
                var search = gui.TextInput(state.Search, width: 190f, height: height,
                    placeholder: "Search", fontSize: fontSize, padding: 5, id: state.ControlId("search"));

                if (search != state.Search)
                {
                    state.Search = search;
                    browser.Search = search;
                    state.Selected = null;
                }
            }
        }

        void Move(Func<ValueTask> navigate)
        {
            _ = navigate();
            state.Selected = null;
            state.Message = null;
        }
    }

    /// <summary>The path as clickable crumbs, or as a text box while it is being edited.</summary>
    static void PathBar(Gui gui, FileDialogState state, float height, float fontSize)
    {
        using (gui.Node().Expand().Enter())
        {
            if (state.EditingPath is { } editing)
            {
                state.EditingPath = gui.TextInput(editing, width: 0, height: height,
                    placeholder: "Path", fontSize: fontSize, padding: 5, id: state.ControlId("path"),
                    grabFocus: true);
                return;
            }

            gui.ClipContent();
            gui.ScrollX();
            gui.Breadcrumb(Crumbs(state, Open), height, fontSize);
        }

        void Open(string path)
        {
            _ = state.Browser.NavigateAsync(path);
            state.Selected = null;
            state.Message = null;
        }
    }

    /// <summary>How many trailing path segments the crumb bar shows before it starts eliding.</summary>
    const int visibleCrumbs = 4;

    /// <summary>
    /// The crumb trail, cut to its last <see cref="visibleCrumbs"/> segments. A deep path would
    /// otherwise overflow the bar and push the folder the user is actually in out of sight; the
    /// leading "…" steps back into what was dropped, and the whole trail is still there to scroll.
    /// </summary>
    static IReadOnlyList<BreadcrumbItem> Crumbs(FileDialogState state, Action<string> open)
    {
        var crumbs = state.Browser.Breadcrumbs();
        var items = new List<BreadcrumbItem>();

        if (crumbs.Count > visibleCrumbs)
        {
            var elided = crumbs[^(visibleCrumbs + 1)];
            items.Add(new BreadcrumbItem("…", () => open(elided.Path)));
        }

        foreach (var crumb in crumbs.TakeLast(visibleCrumbs))
            items.Add(new BreadcrumbItem(crumb.Label, () => open(crumb.Path),
                IsCurrent: crumb.Path == state.Browser.CurrentPath));

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
        if (typed.Trim().Length == 0) return;

        _ = NavigateTypedAsync(state, typed.Trim());
    }

    /// <summary>
    /// Creates a folder under the current one and selects it, so the user can rename it by typing in
    /// the name field or step into it. Names it the way a file manager does when the name is taken.
    /// </summary>
    static async ValueTask NavigateTypedAsync(FileDialogState state, string path)
    {
        if (!await state.Browser.NavigateAsync(path).ConfigureAwait(false))
            state.Message = $"'{path}' is not a folder.";
        else state.Selected = null;
    }

    static async ValueTask NewFolderAsync(FileDialogState state)
    {
        var browser = state.Browser;
        var name = "New Folder";
        for (var suffix = 2;
             await browser.FileSystem.DirectoryExistsAsync(Path.Combine(browser.CurrentPath, name)).ConfigureAwait(false);
             suffix++)
            name = $"New Folder {suffix}";

        state.Message = null;
        if (await browser.CreateFolderAsync(name).ConfigureAwait(false) is not { } created)
        {
            state.Message = browser.Error;
            return;
        }

        state.Selected = browser.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullPath, created, StringComparison.Ordinal));

        if (state.Request?.Mode is FileDialogMode.SaveFile or FileDialogMode.CreateFolder)
            state.Name = name;
    }
}
