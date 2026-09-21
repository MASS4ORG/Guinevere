namespace Guinevere;

/// <summary>
/// One file dialog's state, owned by whatever hosts it: the request being answered, the directory
/// being browsed, and what the user has typed and selected. A host keeps a single instance, calls
/// <see cref="Open"/> to raise the dialog and <c>gui.FileDialog(state)</c> every frame.
/// </summary>
public sealed class FileDialogState
{
    static int nextId;

    internal float ListingScrollY { get; set; }
    internal float ListingViewportHeight { get; set; } = 460f;
    internal string IdPrefix { get; }
    internal string ControlId(string suffix) => $"{IdPrefix}/{suffix}";
    /// <summary>The directory being browsed. Outlives a single showing, so the next one starts where
    /// the last left off unless the request names a path.</summary>
    public FileBrowser Browser { get; }

    /// <summary>Creates state over the local filesystem.</summary>
    public FileDialogState() : this(PhysicalFileDialogFileSystem.Instance) { }

    /// <summary>Creates state over an injectable filesystem.</summary>
    public FileDialogState(IFileDialogFileSystem fileSystem)
    {
        Browser = new FileBrowser(fileSystem);
        IdPrefix = $"filedialog/{Interlocked.Increment(ref nextId)}";
    }

    /// <summary>What is being asked for, or null while the dialog is closed.</summary>
    public FileDialogRequest? Request { get; private set; }

    /// <summary>Whether the dialog is currently showing.</summary>
    public bool IsOpen => Request is not null;

    /// <summary>The entry the user has clicked, or null when the selection was cleared.</summary>
    public FileEntry? Selected { get; set; }

    /// <summary>The name field's text, used by the save and new-folder modes.</summary>
    public string Name { get; set; } = "";

    /// <summary>The search box's text, mirrored into <see cref="FileBrowser.Search"/>.</summary>
    public string Search { get; set; } = "";

    /// <summary>The index into the request's filters that is in use.</summary>
    public int FilterIndex { get; set; }

    /// <summary>The path bar's text while the user is editing it, or null while it shows breadcrumbs.</summary>
    public string? EditingPath { get; set; }

    /// <summary>Why the last confirmation was refused, or null. Shown under the listing.</summary>
    public string? Message { get; set; }

    /// <summary>The save target awaiting a second confirmation because it already exists.</summary>
    public string? PendingOverwrite { get; set; }

    /// <summary>The shortcuts the sidebar lists, resolved once per showing.</summary>
    public IReadOnlyList<FilePlace> Places { get; private set; } = [];

    /// <summary>
    /// Shows the dialog. Any showing already in progress is cancelled first, so a host that raises a
    /// second one never strands the first request's callback.
    /// </summary>
    /// <param name="request">What to ask for.</param>
    public void Open(FileDialogRequest request)
    {
        _ = OpenSafelyAsync(request);
    }

    async ValueTask OpenSafelyAsync(FileDialogRequest request)
    {
        try
        {
            await OpenAsync(request).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            Message = exception.Message;
        }
    }

    /// <summary>Shows the dialog and asynchronously loads its places and initial directory.</summary>
    public async ValueTask OpenAsync(FileDialogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsOpen) Close(null);

        Request = request;
        Places = request.Places ?? [];
        Selected = null;
        Name = request.InitialName;
        Search = "";
        FilterIndex = 0;
        EditingPath = null;
        Message = null;
        PendingOverwrite = null;

        Browser.Search = "";
        Browser.Filter = request.Filters.Count > 0 ? request.Filters[0] : FileDialogFilter.All;

        if (request.Places is null)
            Places = await Browser.FileSystem.GetPlacesAsync(cancellationToken).ConfigureAwait(false);

        var start = request.StartPath ?? Browser.CurrentPath;
        if (!await Browser.NavigateAsync(start, cancellationToken).ConfigureAwait(false))
        {
            var fallback = Places.FirstOrDefault()?.Path;
            if (fallback is not null)
                await Browser.NavigateAsync(fallback, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the dialog and reports <paramref name="result"/> to the request's callback. The request
    /// is cleared before the callback runs, so a callback that opens another dialog works.
    /// </summary>
    /// <param name="result">The chosen path, or null when the dialog was cancelled.</param>
    public void Close(string? result)
    {
        var request = Request;
        Request = null;
        Selected = null;
        EditingPath = null;
        Message = null;
        PendingOverwrite = null;
        request?.OnComplete?.Invoke(result);
        request?.OnClosed?.Invoke(result is null ? FileDialogResult.Cancelled : FileDialogResult.Selected(result));
    }

    /// <summary>
    /// The path the confirming button would return, or null when the dialog cannot be confirmed yet —
    /// nothing is selected, or the name field is empty in a mode that needs one.
    /// </summary>
    /// <returns>The absolute path, or null.</returns>
    public string? Choice()
    {
        if (Request is null) return null;

        return Request.Mode switch
        {
            FileDialogMode.OpenFile => Selected is { IsDirectory: false } file ? file.FullPath : null,
            FileDialogMode.SelectFolder => Selected is { IsDirectory: true } folder
                ? folder.FullPath
                : Browser.CurrentPath.Length > 0 ? Browser.CurrentPath : null,
            _ => Name.Trim().Length > 0 ? Path.Combine(Browser.CurrentPath, Name.Trim()) : null,
        };
    }

    /// <summary>The confirming button's label: the request's own, or one that suits the mode.</summary>
    /// <returns>The label.</returns>
    public string ConfirmLabel() => PendingOverwrite is not null ? "Overwrite" : Request?.ConfirmLabel ?? Request?.Mode switch
    {
        FileDialogMode.SaveFile => "Save",
        FileDialogMode.SelectFolder => "Select Folder",
        FileDialogMode.CreateFolder => "Create",
        _ => "Open",
    };
}
