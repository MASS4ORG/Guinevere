namespace Guinevere;

/// <summary>
/// The directory a file dialog is showing, and everything it lets the user do to get to another one:
/// navigation with back/forward history, sorting, a search term, a filter and hidden-entry visibility.
/// Holds no drawing state, so it can be driven and tested without a <see cref="Gui"/>.
/// </summary>
public sealed class FileBrowser
{
    readonly IFileDialogFileSystem fileSystem;
    readonly List<string> history = [];
    readonly List<FileEntry> loaded = [];
    CancellationTokenSource? loading;

    int historyIndex = -1;
    FileSortColumn sort = FileSortColumn.Name;
    bool ascending = true;
    bool showHidden;
    string search = "";
    FileDialogFilter filter = FileDialogFilter.All;

    /// <summary>Creates a browser over the local filesystem.</summary>
    public FileBrowser() : this(PhysicalFileDialogFileSystem.Instance) { }

    /// <summary>Creates a browser over an injectable filesystem provider.</summary>
    public FileBrowser(IFileDialogFileSystem fileSystem) =>
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>The provider supplying entries and platform roots.</summary>
    public IFileDialogFileSystem FileSystem => fileSystem;

    /// <summary>Whether a directory read is in flight.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>The directory being shown, absolute. Empty until the first navigation.</summary>
    public string CurrentPath { get; private set; } = "";

    /// <summary>Why the last listing or operation failed, or null when it succeeded.</summary>
    public string? Error { get; private set; }

    /// <summary>The current directory's entries, filtered, searched and sorted for display.</summary>
    public IReadOnlyList<FileEntry> Entries { get; private set; } = [];

    /// <summary>Whether <see cref="GoBackAsync"/> has somewhere to go.</summary>
    public bool CanGoBack => historyIndex > 0;

    /// <summary>Whether <see cref="GoForwardAsync"/> has somewhere to go.</summary>
    public bool CanGoForward => historyIndex >= 0 && historyIndex < history.Count - 1;

    /// <summary>Whether the current directory has a parent.</summary>
    public bool CanGoUp => CurrentPath.Length > 0 && fileSystem.GetParent(CurrentPath) is not null;

    /// <summary>
    /// Shows a path without blocking the calling thread. A file path resolves to its parent.
    /// A newer navigation cancels and supersedes an older directory read.
    /// </summary>
    public async ValueTask<bool> NavigateAsync(string path, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource next;
        lock (loaded)
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            next = loading;
            IsLoading = true;
            Error = null;
        }

        try
        {
            var directory = await fileSystem.ResolveDirectoryAsync(path, next.Token).ConfigureAwait(false);
            if (directory is null) return false;
            var entries = await fileSystem.EnumerateAsync(directory, next.Token).ConfigureAwait(false);

            lock (loaded)
            {
                if (!ReferenceEquals(loading, next)) return false;
                CurrentPath = directory;
                loaded.Clear();
                loaded.AddRange(entries);
                if (historyIndex < history.Count - 1)
                    history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                if (history.Count == 0 || !string.Equals(history[^1], directory, StringComparison.Ordinal))
                    history.Add(directory);
                historyIndex = history.Count - 1;
                Rebuild();
                return true;
            }
        }
        catch (OperationCanceledException) when (next.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            if (ReferenceEquals(loading, next)) Error = exception.Message;
            return false;
        }
        finally
        {
            lock (loaded)
            {
                if (ReferenceEquals(loading, next)) IsLoading = false;
            }
        }
    }

    /// <summary>Re-reads the current path asynchronously.</summary>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentPath.Length == 0) return;
        await LoadCurrentAsync(CurrentPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a folder without blocking, then refreshes the listing.</summary>
    public async ValueTask<string?> CreateFolderAsync(string name, CancellationToken cancellationToken = default)
    {
        Error = ValidateName(name);
        if (Error is not null) return null;
        try
        {
            var candidate = Path.Combine(CurrentPath, name.Trim());
            if (await fileSystem.DirectoryExistsAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                Error = $"'{name}' already exists.";
                return null;
            }

            var path = await fileSystem.CreateDirectoryAsync(CurrentPath, name.Trim(), cancellationToken)
                .ConfigureAwait(false);
            await LoadCurrentAsync(CurrentPath, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            Error = exception.Message;
            return null;
        }
    }

    async ValueTask LoadCurrentAsync(string path, CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var entries = await fileSystem.EnumerateAsync(path, cancellationToken).ConfigureAwait(false);
            lock (loaded)
            {
                loaded.Clear();
                loaded.AddRange(entries);
                Rebuild();
            }
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            Error = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>The column the listing is ordered by.</summary>
    public FileSortColumn Sort => sort;

    /// <summary>Whether the listing is ordered ascending.</summary>
    public bool Ascending => ascending;

    /// <summary>Whether entries the platform marks hidden are listed.</summary>
    public bool ShowHidden
    {
        get => showHidden;
        set
        {
            if (showHidden == value) return;
            showHidden = value;
            Rebuild();
        }
    }

    /// <summary>A substring the listed names must contain. Empty lists everything.</summary>
    public string Search
    {
        get => search;
        set
        {
            var next = value;
            if (search == next) return;
            search = next;
            Rebuild();
        }
    }

    /// <summary>The extensions files are narrowed to. Never hides directories.</summary>
    public FileDialogFilter Filter
    {
        get => filter;
        set
        {
            var next = value ?? FileDialogFilter.All;
            if (ReferenceEquals(filter, next)) return;
            filter = next;
            Rebuild();
        }
    }

    /// <summary>Moves to the previous history entry asynchronously.</summary>
    public async ValueTask GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoBack) return;
        await NavigateHistoryAsync(historyIndex - 1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves to the next history entry asynchronously.</summary>
    public async ValueTask GoForwardAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoForward) return;
        await NavigateHistoryAsync(historyIndex + 1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves to the parent directory asynchronously.</summary>
    public ValueTask<bool> GoUpAsync(CancellationToken cancellationToken = default) =>
        fileSystem.GetParent(CurrentPath) is { } parent
            ? NavigateAsync(parent, cancellationToken)
            : ValueTask.FromResult(false);

    async ValueTask NavigateHistoryAsync(int index, CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var target = history[index];
            var entries = await fileSystem.EnumerateAsync(target, cancellationToken).ConfigureAwait(false);
            lock (loaded)
            {
                historyIndex = index;
                CurrentPath = target;
                loaded.Clear();
                loaded.AddRange(entries);
                Rebuild();
            }
        }
        catch (Exception exception) when (IsFileSystemError(exception))
        {
            Error = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Orders by <paramref name="column"/>, or reverses the order when it is already the one in use.
    /// </summary>
    /// <param name="column">The column to order by.</param>
    public void SortBy(FileSortColumn column)
    {
        if (sort == column) ascending = !ascending;
        else
        {
            sort = column;
            ascending = true;
        }

        Rebuild();
    }

    /// <summary>
    /// The current directory split into its segments, each paired with the path that opens it, for a
    /// breadcrumb bar. The first segment is the root.
    /// </summary>
    /// <returns>Every segment from the root to the current directory.</returns>
    public IReadOnlyList<(string Label, string Path)> Breadcrumbs()
        => fileSystem.GetBreadcrumbs(CurrentPath);

    /// <summary>Applies the search, the filter and the ordering to what the last read returned.</summary>
    void Rebuild()
    {
        lock (loaded)
        {
            var visible = loaded.Where(entry =>
                (showHidden || !entry.IsHidden) &&
                (search.Length == 0 || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
                (entry.IsDirectory || filter.Matches(entry.Name)));

            // Directories lead regardless of the column, which is what every file manager does.
            var ordered = visible.OrderBy(entry => entry.IsDirectory ? 0 : 1);

            Entries = [.. sort switch
            {
                FileSortColumn.Size => ascending
                    ? ordered.ThenBy(entry => entry.Size)
                    : ordered.ThenByDescending(entry => entry.Size),
                FileSortColumn.Modified => ascending
                    ? ordered.ThenBy(entry => entry.Modified)
                    : ordered.ThenByDescending(entry => entry.Modified),
                _ => ascending
                    ? ordered.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    : ordered.ThenByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            }];
        }
    }

    /// <summary>A byte count as the listing shows it: whole units, one decimal above kilobytes.</summary>
    /// <param name="bytes">The length, or -1 for something that has none.</param>
    /// <returns>The formatted size, or an empty string when there is none.</returns>
    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "";
        if (bytes < 1024) return $"{bytes} B";

        string[] units = ["KB", "MB", "GB", "TB", "PB"];
        double size = bytes;
        var unit = -1;
        do
        {
            size /= 1024;
            unit++;
        } while (size >= 1024 && unit < units.Length - 1);

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{size:0.#} {units[unit]}");
    }

    static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "A folder needs a name.";
        return name.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            ? $"'{name}' is not a valid folder name."
            : null;
    }

    static bool IsFileSystemError(Exception exception) => exception is IOException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException;
}
