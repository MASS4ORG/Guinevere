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
    const float sidebarWidth = 168f;

    /// <summary>Height of one listing row, one sidebar entry and the toolbar's buttons.</summary>
    const float rowHeight = 22f;

    /// <summary>The gap between the dialog's regions.</summary>
    const float regionGap = 6f;

    /// <summary>
    /// Draws the file dialog <paramref name="state"/> is showing, and does nothing when it is closed.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="state">The dialog's state, owned by the host across frames.</param>
    /// <param name="width">The dialog's width.</param>
    /// <param name="height">The dialog's body height, before its title bar and footer.</param>
    /// <param name="fontSize">Base text size for the listing, sidebar and fields.</param>
    public static void FileDialog(this Gui gui, FileDialogState state,
        float width = 840f, float height = 460f, float fontSize = 13f)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);

        var isOpen = state.IsOpen;
        var title = state.Request?.Title ?? "";

        gui.Dialog(ref isOpen, title,
            () => Body(gui, state, fontSize),
            width, height,
            () => Footer(gui, state, fontSize),
            footerHeight: 44f);

        // Escape and the title bar's × write straight into isOpen, so that is how the dialog reports
        // a cancellation: the request's callback still has to hear about it.
        if (!isOpen && state.IsOpen) state.Close(null);
    }

    static void Body(Gui gui, FileDialogState state, float fontSize)
    {
        if (state.Request is not { } request) return;

        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(regionGap).Enter())
        {
            Toolbar(gui, state, fontSize);

            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(regionGap).Enter())
            {
                Sidebar(gui, state, fontSize);
                Listing(gui, state, fontSize);
            }

            NameRow(gui, state, request, fontSize);
            Status(gui, state, fontSize);

            if (gui.Pass == Pass.Pass2Render && gui.Input.IsKeyPressed(KeyboardKey.Enter))
                _ = ConfirmAsync(state);
        }
    }

    /// <summary>The name field, and the filter dropdown when the request offers a choice.</summary>
    static void NameRow(Gui gui, FileDialogState state, FileDialogRequest request, float fontSize)
    {
        var named = request.Mode is FileDialogMode.SaveFile or FileDialogMode.CreateFolder;
        var filtered = request.Filters.Count > 1;
        if (!named && !filtered) return;

        var height = rowHeight + 6f;

        using (gui.Node(-1, height).ExpandWidth().Direction(Axis.Horizontal).Gap(regionGap)
                   .ContentAlignY(0.5f).Enter())
        {
            if (named)
            {
                using (gui.Node(52f, height).ContentAlignY(0.5f).Enter())
                    gui.DrawText(request.Mode == FileDialogMode.CreateFolder ? "Folder" : "Name",
                        fontSize, gui.ControlStyle.TextDim, centerInRect: false);

                using (gui.Node().Expand().Enter())
                    state.Name = gui.TextInput(state.Name, width: 0, height: height,
                        placeholder: "Name", fontSize: fontSize, padding: 5, id: state.ControlId("name"));
            }

            if (!filtered) return;

            using (gui.Node().Expand().Enter()) { }

            var index = state.FilterIndex;
            gui.Dropdown([.. request.Filters.Select(filter => filter.Label)], ref index,
                width: 200f, height: height, fontSize: fontSize);

            if (index == state.FilterIndex) return;

            state.FilterIndex = index;
            state.Browser.Filter = request.Filters[index];
        }
    }

    /// <summary>The line under the listing: whatever refused the last confirmation, or a read error.</summary>
    static void Status(Gui gui, FileDialogState state, float fontSize)
    {
        var message = state.Message ?? state.Browser.Error;
        if (message is null) return;

        using (gui.Node(-1, rowHeight).ExpandWidth().ContentAlignY(0.5f).Enter())
            gui.DrawText(message, fontSize - 1f, gui.ControlStyle.Negative, centerInRect: false);
    }

    static void Footer(Gui gui, FileDialogState state, float fontSize)
    {
        if (!state.IsOpen) return;

        if (gui.Button("Cancel", width: 90f, height: 28f, fontSize: fontSize)) state.Close(null);

        var choice = state.Choice();
        if (gui.Button(state.ConfirmLabel(), width: 120f, height: 28f, fontSize: fontSize,
                backgroundColor: choice is null ? null : gui.ControlStyle.Accent,
                enabled: choice is not null))
            _ = ConfirmAsync(state);
    }

    /// <summary>
    /// Accepts what the dialog is pointing at, unless the mode or the request's own check refuses it.
    /// A refusal is shown under the listing and the dialog stays open.
    /// </summary>
    internal static async ValueTask ConfirmAsync(FileDialogState state)
    {
        if (state.Request is not { } request) return;

        if (state.Choice() is not { } choice)
        {
            state.Message = request.Mode switch
            {
                FileDialogMode.OpenFile => "Select a file.",
                FileDialogMode.SelectFolder => "Select a folder.",
                _ => "Enter a name.",
            };
            return;
        }

        if (request.Mode == FileDialogMode.OpenFile &&
            !await state.Browser.FileSystem.FileExistsAsync(choice).ConfigureAwait(false))
        {
            state.Message = "That file no longer exists.";
            return;
        }

        if (request.Mode == FileDialogMode.SaveFile && state.PendingOverwrite != choice &&
            await state.Browser.FileSystem.FileExistsAsync(choice).ConfigureAwait(false))
        {
            state.PendingOverwrite = choice;
            state.Message = $"'{Path.GetFileName(choice)}' already exists. Choose Overwrite to replace it.";
            return;
        }

        if (request.Validate?.Invoke(choice) is { } refusal)
        {
            state.Message = refusal;
            return;
        }

        if (request.Mode == FileDialogMode.CreateFolder &&
            !await state.Browser.FileSystem.DirectoryExistsAsync(choice).ConfigureAwait(false))
        {
            var name = Path.GetFileName(choice);
            if (await state.Browser.CreateFolderAsync(name).ConfigureAwait(false) is null)
            {
                state.Message = state.Browser.Error;
                return;
            }
        }

        state.Close(choice);
    }

    /// <summary>
    /// Opens a directory row, or accepts a file one. What a double click and the Enter key both do.
    /// </summary>
    static void Activate(FileDialogState state, FileEntry entry)
    {
        if (entry.IsDirectory)
        {
            _ = state.Browser.NavigateAsync(entry.FullPath);
            state.Selected = null;
            state.Message = null;
            return;
        }

        state.Selected = entry;
        _ = ConfirmAsync(state);
    }

    /// <summary>Draws the same browser inline in normal layout flow.</summary>
    public static void FileBrowser(this Gui gui, FileDialogState state, float width = 840f,
        float height = 460f, float fontSize = 13f)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        using (gui.Node(width, height, state.ControlId("root")).Direction(Axis.Vertical).Gap(regionGap).Enter())
        {
            Body(gui, state, fontSize);
            using (gui.Node(-1, 36f).ExpandWidth().Direction(Axis.Horizontal).Gap(regionGap)
                       .ContentAlignX(1f).ContentAlignY(0.5f).Enter())
                Footer(gui, state, fontSize);
        }
    }
}
