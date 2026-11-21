namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Id suffix of the button that starts and finishes path editing.</summary>
    const string PathToggleId = "path-toggle";

    /// <summary>How many folder names the status line lists when Tab finds several.</summary>
    const int HintedMatches = 5;

    /// <summary>
    /// The path box while it is being edited. Enter goes to the typed path, Escape and moving focus elsewhere
    /// cancel the edit, and Tab completes the last segment against the folders that exist.
    /// </summary>
    static void PathBox(Gui gui, FileDialogState state, float height)
    {
        var session = state.PathEdit ??= new PathEditSession(state.Browser.CurrentPath, selectAll: true);
        var id = state.ControlId("path");
        var text = state.EditingPath ?? "";
        var box = gui.TextInput(ref text, placeholder: "Path", id: id).Width(Size.Expand()).Height(height);
        state.EditingPath = text;

        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        gui.Focus.SetClaimsTab(id, true);
        if (box.Changed)
        {
            NoteTyped(state, session);
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Escape))
        {
            CancelPathEdit(state, restoreFocus: true);
            return;
        }

        if (LeftPathEdit(gui, state, session, id, text))
        {
            CancelPathEdit(state);
            return;
        }

        HandleBoxKeys(gui, state, id, box.Submitted);
    }

    /// <summary>Typing starts any Tab cycle over and clears a completion hint the status line was showing.</summary>
    static void NoteTyped(FileDialogState state, PathEditSession session)
    {
        session.ResetCycle();
        if (session.HintShown)
        {
            session.HintShown = false;
            state.Message = null;
        }
    }

    /// <summary>
    /// Whether the edit is over: the browser landed in another folder by any other route — back, up, a crumb, a
    /// place — or focus went somewhere it may not.
    /// </summary>
    static bool LeftPathEdit(Gui gui, FileDialogState state, PathEditSession session, string id, string text) =>
        !string.Equals(state.Browser.CurrentPath, session.Origin, StringComparison.Ordinal) ||
        !KeepsFocus(gui, state, session, id, text);

    /// <summary>Enter goes to the typed path; Tab, with the box focused, completes it, Shift+Tab going backwards.</summary>
    static void HandleBoxKeys(Gui gui, FileDialogState state, string id, bool submitted)
    {
        if (submitted)
        {
            CommitPathEdit(state);
            return;
        }

        if (!gui.Input.IsKeyPressed(KeyboardKey.Tab) || !gui.Focus.HasFocus(id))
        {
            return;
        }

        var backwards = gui.Input.IsKeyDown(KeyboardKey.LeftShift) || gui.Input.IsKeyDown(KeyboardKey.RightShift);
        _ = CompleteAsync(state, backwards).AsTask();
    }

    /// <summary>
    /// Gives the box focus until it lands, then reports false once focus has gone anywhere but the edit button.
    /// The button is the one place focus may sit while the typed path is being resolved.
    /// </summary>
    static bool KeepsFocus(Gui gui, FileDialogState state, PathEditSession session, string id, string text)
    {
        var focused = gui.Focus.CurrentFocusedId;
        if (focused == id)
        {
            if (session.Grab)
            {
                session.Grab = false;
                var edit = TextEditor.State(gui, id, text);
                if (session.SelectAll)
                {
                    edit.SelectAll();
                }
                else
                {
                    edit.MoveTo(edit.Text.Length, extend: false);
                }
            }

            session.FocusSeen = true;
            return true;
        }

        if (session.Grab)
        {
            gui.RequestFocus(id);
            return true;
        }

        return !session.FocusSeen || focused == state.ControlId(PathToggleId);
    }

    /// <summary>
    /// Ctrl+L starts editing the path with all of it selected, wherever focus is, and typing <c>/</c> or <c>~</c>
    /// while the listing has focus starts it with that text.
    /// </summary>
    static void HandlePathShortcuts(Gui gui, FileDialogState state)
    {
        var input = gui.Input;
        var control = input.IsKeyDown(KeyboardKey.LeftControl) || input.IsKeyDown(KeyboardKey.RightControl);
        if (control && input.IsKeyPressed(KeyboardKey.L))
        {
            SelectOrBeginPathEdit(state);
        }
        else if (!control && TypedPathStart(gui, state) is { } typed)
        {
            BeginPathEdit(state, typed);
        }
    }

    /// <summary>Selects the whole typed path when it is being edited, or else starts editing it.</summary>
    static void SelectOrBeginPathEdit(FileDialogState state)
    {
        if (state.EditingPath is not null && state.PathEdit is { } session)
        {
            session.Grab = true;
            session.SelectAll = true;
        }
        else
        {
            BeginPathEdit(state);
        }
    }

    /// <summary>The text typed into the listing when it starts a path (<c>/</c> or <c>~</c>), otherwise null.</summary>
    static string? TypedPathStart(Gui gui, FileDialogState state)
    {
        if (state.EditingPath is not null || gui.Focus.CurrentFocusedId != state.ControlId("listing"))
        {
            return null;
        }

        return gui.Input.GetTypedCharacters() is { Length: > 0 } typed && (typed[0] is '/' or '~') ? typed : null;
    }

    /// <summary>
    /// Shows the path as editable text: the current folder with all of it selected, or <paramref name="typed"/>
    /// with the caret after it.
    /// </summary>
    internal static void BeginPathEdit(FileDialogState state, string? typed = null)
    {
        state.PathEdit?.Dispose();
        state.PathEdit = new PathEditSession(state.Browser.CurrentPath, selectAll: typed is null);
        state.Message = null;
        state.EditingPath = typed ?? state.Browser.CurrentPath;
    }

    /// <summary>
    /// Drops the typed text and goes back to the crumbs, leaving the folder as it was. With
    /// <paramref name="restoreFocus"/> focus goes back to where the dialog normally keeps it, rather than being
    /// left wherever the user moved it.
    /// </summary>
    internal static void CancelPathEdit(FileDialogState state, bool restoreFocus = false)
    {
        if (state.PathEdit?.Failed == true)
        {
            state.Browser.ClearError();
        }

        state.Message = null;
        state.EditingPath = null;
        state.FocusPending |= restoreFocus;
        EndPathEdit(state);
    }

    /// <summary>Releases what an edit was holding once the bar shows crumbs again.</summary>
    static void EndPathEdit(FileDialogState state)
    {
        state.PathEdit?.Dispose();
        state.PathEdit = null;
    }

    /// <summary>The edit button: starts editing, or goes to the typed path when it is already being edited.</summary>
    static void TogglePathEditing(FileDialogState state)
    {
        if (state.EditingPath is null)
        {
            BeginPathEdit(state);
        }
        else
        {
            CommitPathEdit(state);
        }
    }

    /// <summary>Goes to the typed path; a blank box just cancels. The box stays until the path was reached.</summary>
    internal static void CommitPathEdit(FileDialogState state)
    {
        if (state.EditingPath is not { } typed)
        {
            return;
        }

        if (typed.Trim().Length == 0)
        {
            CancelPathEdit(state);
            return;
        }

        _ = NavigateTypedAsync(state, typed).AsTask();
    }

    /// <summary>
    /// Opens the typed path. A folder is shown; a file shows its folder with that file selected, and in a save
    /// dialog fills the name field with it. A path that is missing or unreadable is reported under the listing and the
    /// box keeps what was typed, focused, so the typo can be fixed.
    /// </summary>
    internal static async ValueTask NavigateTypedAsync(FileDialogState state, string typed)
    {
        var session = state.PathEdit;
        var (path, isFile, failure) = await ResolveTypedAsync(state, typed).ConfigureAwait(false);

        await state.ToUiThread();

        state.Message = failure;
        if (failure is not null)
        {
            KeepEditingAfterFailure(state, session);
            return;
        }

        state.Selected = isFile ? FindFile(state.Browser, path) : null;
        if (isFile && state.Request?.Mode == FileDialogMode.SaveFile)
        {
            state.Name = Path.GetFileName(path);
        }

        state.EditingPath = null;
        state.FocusPending = true;
    }

    /// <summary>
    /// Navigates to the typed path off the UI thread. Returns the normalized path, whether it is a file, and why it
    /// could not be shown, or null when it was.
    /// </summary>
    static async ValueTask<(string Path, bool IsFile, string? Failure)> ResolveTypedAsync(FileDialogState state,
        string typed)
    {
        var browser = state.Browser;
        var fileSystem = browser.FileSystem;
        var path = "";
        try
        {
            path = fileSystem.NormalizePath(typed, browser.CurrentPath);
            if (path.Length == 0)
            {
                return (path, false, "Enter a path.");
            }

            var isFile = await fileSystem.FileExistsAsync(path).ConfigureAwait(false);
            var reached = await browser.NavigateAsync(path).ConfigureAwait(false);
            return (path, isFile, reached ? null : browser.Error ?? $"'{typed.Trim()}' does not exist.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            return (path, false, exception.Message);
        }
    }

    /// <summary>Leaves the typed text in the box, focused and unselected, unless that edit has since ended.</summary>
    static void KeepEditingAfterFailure(FileDialogState state, PathEditSession? session)
    {
        if (session is null || !ReferenceEquals(state.PathEdit, session))
        {
            return;
        }

        session.Failed = true;
        session.HintShown = false;
        session.Grab = true;
        session.SelectAll = false;
    }

    /// <summary>The listed file called like <paramref name="path"/>'s last segment, or null when it is not listed.</summary>
    static FileEntry? FindFile(FileBrowser browser, string path)
    {
        var name = Path.GetFileName(path);
        var files = browser.Entries.Where(static entry => !entry.IsDirectory).ToList();
        return files.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))
               ?? files.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Completes the last segment of the typed path against the folders of the one before it. A single match is
    /// written out and closed with a separator; several are cycled through by repeated Tab, and listed in the status
    /// line. The folders are read through the file system, off the UI thread.
    /// </summary>
    internal static async ValueTask CompleteAsync(FileDialogState state, bool backwards)
    {
        if (state.EditingPath is not { } text || state.PathEdit is not { } session)
        {
            return;
        }

        if (session.IsCycling(text))
        {
            state.EditingPath = session.Step(backwards);
            return;
        }

        var browser = state.Browser;
        var (head, prefix) = FilePathText.SplitLastSegment(text);
        var query = session.StartQuery();
        var token = query.Token;
        IReadOnlyList<string> matches;
        try
        {
            var parent = browser.FileSystem.NormalizePath(head.Length == 0 ? "." : head, browser.CurrentPath);
            matches = parent.Length == 0
                ? []
                : await browser.FileSystem.CompleteFoldersAsync(parent, prefix, browser.ShowHidden, token)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            matches = [];
        }

        await state.ToUiThread();
        if (token.IsCancellationRequested || !string.Equals(state.EditingPath, text, StringComparison.Ordinal))
        {
            return;
        }

        ApplyCompletion(state, session, head, prefix, matches);
    }

    /// <summary>Writes the first match into the box, or says why there is none.</summary>
    static void ApplyCompletion(FileDialogState state, PathEditSession session, string head, string prefix,
        IReadOnlyList<string> matches)
    {
        session.ResetCycle();
        if (matches.Count == 0)
        {
            state.Message = prefix.Length == 0 ? "There are no folders here." : $"No folder starts with '{prefix}'.";
            session.HintShown = true;
            return;
        }

        if (matches.Count == 1)
        {
            state.EditingPath = head + matches[0] + Path.DirectorySeparatorChar;
            return;
        }

        state.EditingPath = session.Begin(head, matches);
        var listed = string.Join(", ", matches.Take(HintedMatches));
        state.Message = $"{matches.Count} folders match: {listed}{(matches.Count > HintedMatches ? ", …" : "")}";
        session.HintShown = true;
    }
}
