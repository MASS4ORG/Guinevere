namespace Guinevere.Tests.Mocks;

/// <summary>
/// A folder tree held in memory, so file dialog tests never touch the disk. Paths are built with
/// <see cref="Path"/> under <see cref="Root"/>, which keeps them valid on every platform.
/// </summary>
public sealed class InMemoryFileDialogFileSystem : IFileDialogFileSystem
{
    readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    readonly HashSet<string> _files = new(StringComparer.Ordinal);
    readonly HashSet<string> _denied = new(StringComparer.Ordinal);

    /// <summary>Creates a tree holding only its root.</summary>
    public InMemoryFileDialogFileSystem()
    {
        Root = Path.GetFullPath(OperatingSystem.IsWindows() ? @"C:\fake" : "/fake");
        _directories.Add(Root);
    }

    /// <summary>The folder every other path hangs from.</summary>
    public string Root { get; }

    /// <summary>When set, <see cref="EnumerateAsync"/> waits for it before answering.</summary>
    public Task? Gate { get; set; }

    /// <summary>How many times a folder was listed.</summary>
    public int Enumerations { get; private set; }

    /// <summary>The path made of <paramref name="parts"/> under <see cref="Root"/>.</summary>
    public string P(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Adds a folder and every folder above it.</summary>
    public InMemoryFileDialogFileSystem AddDirectory(params string[] parts)
    {
        for (var path = P(parts); path.Length > Root.Length; path = Path.GetDirectoryName(path)!)
        {
            _directories.Add(path);
        }

        return this;
    }

    /// <summary>Adds a file and the folders above it.</summary>
    public InMemoryFileDialogFileSystem AddFile(params string[] parts)
    {
        var path = P(parts);
        if (!string.Equals(Path.GetDirectoryName(path), Root, StringComparison.Ordinal))
        {
            AddDirectory(Path.GetRelativePath(Root, Path.GetDirectoryName(path)!));
        }

        _files.Add(path);
        return this;
    }

    /// <summary>Makes listing the folder throw <see cref="UnauthorizedAccessException"/>.</summary>
    public InMemoryFileDialogFileSystem Deny(params string[] parts)
    {
        _denied.Add(P(parts));
        return this;
    }

    /// <inheritdoc />
    public ValueTask<string?> ResolveDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        if (_directories.Contains(trimmed))
        {
            return ValueTask.FromResult<string?>(trimmed);
        }

        return ValueTask.FromResult(_files.Contains(trimmed) ? Path.GetDirectoryName(trimmed) : null);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<FileEntry>> EnumerateAsync(string path,
        CancellationToken cancellationToken = default)
    {
        Enumerations++;
        if (Gate is { } gate)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_denied.Contains(path))
        {
            throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
        }

        var entries = new List<FileEntry>();
        entries.AddRange(_directories.Where(directory => IsChild(path, directory))
            .Select(static directory => new FileEntry(directory, Path.GetFileName(directory), true, -1,
                DateTime.MinValue, Path.GetFileName(directory).StartsWith('.'))));
        entries.AddRange(_files.Where(file => IsChild(path, file))
            .Select(static file => new FileEntry(file, Path.GetFileName(file), false, 1, DateTime.MinValue)));
        return entries;
    }

    /// <inheritdoc />
    public ValueTask<string> CreateDirectoryAsync(string parentPath, string name,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(parentPath, name);
        _directories.Add(path);
        return ValueTask.FromResult(path);
    }

    /// <inheritdoc />
    public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_files.Contains(path));

    /// <inheritdoc />
    public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_directories.Contains(path));

    /// <inheritdoc />
    public string? GetParent(string path) => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));

    /// <inheritdoc />
    public IReadOnlyList<(string Label, string Path)> GetBreadcrumbs(string path)
    {
        var crumbs = new List<(string, string)>();
        for (var current = path; current.Length > 0; current = Path.GetDirectoryName(current) ?? "")
        {
            crumbs.Insert(0, (Path.GetFileName(current).Length > 0 ? Path.GetFileName(current) : current, current));
        }

        return crumbs;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<FilePlace>> GetPlacesAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<FilePlace>>([new FilePlace("Fake", Root)]);

    static bool IsChild(string parent, string path) =>
        string.Equals(Path.GetDirectoryName(path), parent, StringComparison.Ordinal);
}
