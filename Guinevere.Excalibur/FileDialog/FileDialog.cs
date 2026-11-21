namespace Guinevere;

/// <summary>
/// A file and folder picker drawn by Guinevere itself, so it looks and behaves the same on every
/// platform the host runs on and needs nothing from the desktop environment.
/// </summary>
/// <remarks>
/// The dialog is modal: a host calls <see cref="FileDialogState.Open"/> and then this every frame,
/// and hears the answer through the request's callback. Only one dialog should be drawn at a time —
/// the underlying <c>Dialog</c> identifies its state by call site, so two would share it.
/// </remarks>
public static partial class ControlsExtensions
{
    /// <summary>Width of the shortcuts column.</summary>
    const float SidebarWidth = 168f;

    /// <summary>Height of one listing row, one sidebar entry and the toolbar's buttons.</summary>
    const float RowHeight = 22f;

    /// <summary>The gap between the dialog's regions.</summary>
    const float RegionGap = 6f;

    /// <summary>
    /// Draws the file dialog <paramref name="state"/> is showing, and does nothing when it is closed.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="state">The dialog's state, owned by the host across frames.</param>
    /// <param name="classes">Stylesheet classes for the dialog.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <returns>
    /// The dialog's node: 840 wide with a 460 high body unless a sheet or the node says otherwise. Its text is one
    /// size under the control font size.
    /// </returns>
    public static LayoutNode FileDialog(this Gui gui, FileDialogState state,
        IReadOnlyList<string>? classes = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        state.Attach(gui);

        var isOpen = state.IsOpen;
        var title = state.Request?.Title ?? "";
        var fontSize = FileFontSize(gui);
        var chrome = DialogTitleHeight(gui) + DialogFooterHeight(gui);

        // Escape belongs to the path box while it is being edited: it cancels the edit, and only the next
        // press closes the dialog.
        var dialog = DialogCore(gui, ref isOpen, title,
            () => Body(gui, state, fontSize),
            () => Footer(gui, state, fontSize),
            showCloseButton: true, draggable: true, closeOnEscape: state.EditingPath is null,
            closeOnClickOutside: false,
            classes: classes is null ? ["file"] : ["file", .. classes],
            id: id ?? state.ControlId("dialog"),
            width: FileBrowserWidth, height: FileBrowserHeight + chrome,
            fallback: new Vector2(FileBrowserWidth, FileBrowserHeight + chrome));

        // Escape and the title bar's × write straight into isOpen, so that is how the dialog reports
        // a cancellation: the request's callback still has to hear about it.
        if (!isOpen && state.IsOpen)
        {
            state.Close(null);
        }

        return dialog;
    }

    /// <summary>Default width of the file dialog and the inline browser.</summary>
    const float FileBrowserWidth = 840f;

    /// <summary>Default height of the browser body, before a dialog's title bar and footer.</summary>
    const float FileBrowserHeight = 460f;

    /// <summary>The base text size of the file browser, one under the control font size.</summary>
    static float FileFontSize(Gui gui) => gui.ControlStyle.FontSize - 1f;

    static void Body(Gui gui, FileDialogState state, float fontSize)
    {
        if (state.Request is not { } request)
        {
            return;
        }

        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(RegionGap).Enter())
        {
            // The dialog's text size is the control size of the fields and dropdowns inside it.
            gui.CurrentNodeScope.Set(ControlStyles.Value<ControlFontSize, float>(fontSize));
            Toolbar(gui, state, fontSize);

            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(RegionGap).Enter())
            {
                Sidebar(gui, state, fontSize);
                Listing(gui, state, fontSize);
            }

            NameRow(gui, state, request, fontSize);
            Status(gui, state, fontSize);

            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            PlaceInitialFocus(gui, state, request);
            HandlePathShortcuts(gui, state);
            if (gui.Input.IsKeyPressed(KeyboardKey.Enter) && EnterConfirms(gui, state))
            {
                _ = ConfirmAsync(state).AsTask();
            }
        }
    }

    /// <summary>
    /// Whether Enter accepts the dialog: only while the listing or the name field has focus, or nothing does.
    /// Any other focused control, the path and search boxes included, uses Enter for itself.
    /// </summary>
    static bool EnterConfirms(Gui gui, FileDialogState state)
    {
        if (state.EditingPath is not null)
        {
            return false;
        }

        var focused = gui.Focus.CurrentFocusedId;
        return focused is null || focused == state.ControlId("listing") || focused == state.ControlId("name");
    }

    /// <summary>
    /// When a showing starts or path editing ends, focuses the name field where a name is asked for and the listing
    /// otherwise, so typing and Enter go where the user expects instead of to the first button.
    /// </summary>
    static void PlaceInitialFocus(Gui gui, FileDialogState state, FileDialogRequest request)
    {
        // A request made while the path box still holds focus would be lost with the box, so it waits a frame.
        if (!state.FocusPending || gui.HasFocus(state.ControlId("path")))
        {
            return;
        }

        state.FocusPending = false;
        var named = request.Mode is FileDialogMode.SaveFile or FileDialogMode.CreateFolder;
        gui.RequestFocus(state.ControlId(named ? "name" : "listing"));
    }

    /// <summary>The name field, and the filter dropdown when the request offers a choice.</summary>
    static void NameRow(Gui gui, FileDialogState state, FileDialogRequest request, float fontSize)
    {
        var named = request.Mode is FileDialogMode.SaveFile or FileDialogMode.CreateFolder;
        var filtered = request.Filters.Count > 1;
        if (!named && !filtered)
        {
            return;
        }

        var height = RowHeight + 6f;

        using (gui.Node(-1, height).ExpandWidth().Direction(Axis.Horizontal).Gap(RegionGap)
                   .AlignContentY(0.5f).Enter())
        {
            if (named)
            {
                NameField(gui, state, request, fontSize, height);
            }

            if (filtered)
            {
                FilterDropdown(gui, state, request, height);
            }
        }
    }

    /// <summary>The label and text box for the name a save or new folder dialog asks for.</summary>
    static void NameField(Gui gui, FileDialogState state, FileDialogRequest request, float fontSize, float height)
    {
        using (gui.StyledNode("file-label").Width(52f).Height(height).AlignContentY(0.5f).Enter())
        {
            gui.DrawText(request.Mode == FileDialogMode.CreateFolder ? "Folder" : "Name",
                fontSize, centerInRect: false);
        }

        using (gui.Node().Expand().Enter())
        {
            var name = state.Name;
            gui.TextInput(ref name, placeholder: "Name", id: state.ControlId("name")).Width(Size.Expand())
                .Height(height);
            state.Name = name;
        }
    }

    /// <summary>The filter dropdown at the end of the name row; picking one re-filters the listing.</summary>
    static void FilterDropdown(Gui gui, FileDialogState state, FileDialogRequest request, float height)
    {
        using (gui.Node().Expand().Enter()) { }

        var index = state.FilterIndex;
        gui.Dropdown([.. request.Filters.Select(static filter => filter.Label)], ref index).Width(200f)
            .Height(height);

        if (index == state.FilterIndex)
        {
            return;
        }

        state.FilterIndex = index;
        state.Browser.Filter = request.Filters[index];
    }

    /// <summary>The line under the listing: whatever refused the last confirmation, or a read error.</summary>
    static void Status(Gui gui, FileDialogState state, float fontSize)
    {
        var message = state.Message ?? state.Browser.Error;
        if (message is null)
        {
            return;
        }

        using (gui.StyledNode("file-status").Height(RowHeight).ExpandWidth().AlignContentY(0.5f).Enter())
        {
            gui.DrawText(message, fontSize - 1f, centerInRect: false);
        }
    }

    static readonly string[] PrimaryClass = ["primary"];

    static void Footer(Gui gui, FileDialogState state, float fontSize)
    {
        if (!state.IsOpen)
        {
            return;
        }

        gui.SetTextSize(fontSize);
        if (gui.Button("Cancel").Width(90f).Height(28f).Clicked)
        {
            state.Close(null);
        }

        var choice = state.Choice();
        if (gui.Button(state.ConfirmLabel(), enabled: choice is not null, classes: PrimaryClass)
                .Width(120f).Height(28f).Clicked)
        {
            _ = ConfirmAsync(state).AsTask();
        }
    }

    /// <summary>
    /// Accepts what the dialog is pointing at, unless the mode or the request's own check refuses it.
    /// A refusal is shown under the listing and the dialog stays open.
    /// </summary>
    internal static async ValueTask ConfirmAsync(FileDialogState state)
    {
        if (state.Request is not { } request)
        {
            return;
        }

        if (state.Choice() is not { } choice)
        {
            state.Message = MissingChoiceMessage(request.Mode);
            return;
        }

        // Everything after an await that may have left the UI thread is handed back before it touches the state or
        // calls the host.
        var checksFile = ChecksFile(state, request, choice);
        var fileExists = checksFile && await state.Browser.FileSystem.FileExistsAsync(choice).ConfigureAwait(false);
        await state.ToUiThread();
        if (!ReferenceEquals(state.Request, request) || RefusesChoice(state, request, choice, checksFile, fileExists))
        {
            return;
        }

        if (request.Mode == FileDialogMode.CreateFolder && !await CreateChosenFolderAsync(state, request, choice))
        {
            return;
        }

        state.Close(choice);
    }

    /// <summary>What to ask for when the dialog has nothing to confirm yet, by mode.</summary>
    static string MissingChoiceMessage(FileDialogMode mode) => mode switch
    {
        FileDialogMode.OpenFile => "Select a file.",
        FileDialogMode.SelectFolder => "Select a folder.",
        _ => "Enter a name.",
    };

    /// <summary>Whether confirming must check the file exists: opening one, or saving over an unconfirmed one.</summary>
    static bool ChecksFile(FileDialogState state, FileDialogRequest request, string choice) =>
        request.Mode == FileDialogMode.OpenFile ||
        (request.Mode == FileDialogMode.SaveFile && state.PendingOverwrite != choice);

    /// <summary>
    /// Shows why the choice cannot be accepted and returns true: a vanished file, an overwrite still to be confirmed,
    /// or the request's own refusal.
    /// </summary>
    static bool RefusesChoice(FileDialogState state, FileDialogRequest request, string choice, bool checksFile,
        bool fileExists)
    {
        if (request.Mode == FileDialogMode.OpenFile && !fileExists)
        {
            state.Message = "That file no longer exists.";
            return true;
        }

        if (request.Mode == FileDialogMode.SaveFile && checksFile && fileExists)
        {
            state.PendingOverwrite = choice;
            state.Message = $"'{Path.GetFileName(choice)}' already exists. Choose Overwrite to replace it.";
            return true;
        }

        if (request.Validate?.Invoke(choice) is { } refusal)
        {
            state.Message = refusal;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates the folder a create-folder dialog is about to return, unless it already exists. Reports why it could
    /// not and returns false when the dialog should stay open.
    /// </summary>
    static async ValueTask<bool> CreateChosenFolderAsync(FileDialogState state, FileDialogRequest request,
        string choice)
    {
        var browser = state.Browser;
        var created = (string?)choice;
        if (!await browser.FileSystem.DirectoryExistsAsync(choice).ConfigureAwait(false))
        {
            created = await browser.CreateFolderAsync(Path.GetFileName(choice)).ConfigureAwait(false);
        }

        await state.ToUiThread();
        if (!ReferenceEquals(state.Request, request))
        {
            return false;
        }

        if (created is null)
        {
            state.Message = browser.Error;
        }

        return created is not null;
    }

    /// <summary>
    /// Opens a directory row, or accepts a file one. What a double click and the Enter key both do.
    /// </summary>
    static void Activate(FileDialogState state, FileEntry entry)
    {
        if (entry.IsDirectory)
        {
            _ = state.Browser.NavigateAsync(entry.FullPath).AsTask();
            state.Selected = null;
            state.Message = null;
            return;
        }

        state.Selected = entry;
        _ = ConfirmAsync(state).AsTask();
    }

    /// <summary>
    /// Draws the same browser inline in normal layout flow, 840 by 460 unless a sheet or the returned node says
    /// otherwise.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="state">The browser's state, owned by the host across frames.</param>
    /// <param name="classes">Stylesheet classes for the browser.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <returns>The browser's node.</returns>
    public static LayoutNode FileBrowser(this Gui gui, FileDialogState state,
        IReadOnlyList<string>? classes = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        ExcaliburStyles.Ensure(gui);
        state.Attach(gui);
        var fontSize = FileFontSize(gui);
        var browser = gui.StyledNode("file-browser", classes, id ?? state.ControlId("root"));
        SizeUnlessStyled(browser, FileBrowserWidth, FileBrowserHeight);
        using (browser.Direction(Axis.Vertical).Gap(RegionGap).Enter())
        {
            Body(gui, state, fontSize);
            using (gui.Node(-1, 36f).ExpandWidth().Direction(Axis.Horizontal).Gap(RegionGap)
                       .AlignContentX(1f).AlignContentY(0.5f).Enter())
            {
                Footer(gui, state, fontSize);
            }
        }

        return browser;
    }
}
