namespace Guinevere;

/// <summary>The local <see cref="System.IO"/> implementation used by default.</summary>
public sealed class PhysicalFileDialogFileSystem : IFileDialogFileSystem
{
    /// <summary>A shared stateless instance.</summary>
    public static PhysicalFileDialogFileSystem Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<string?> ResolveDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        Background(() => ResolveDirectory(path), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<FileEntry>> EnumerateAsync(string path,
        CancellationToken cancellationToken = default) => Background<IReadOnlyList<FileEntry>>(() =>
        {
            var entries = new List<FileEntry>();
            foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(Describe(info));
            }

            return entries;
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<string> CreateDirectoryAsync(string parentPath, string name,
        CancellationToken cancellationToken = default) => Background(() =>
        Directory.CreateDirectory(Path.Combine(parentPath, name)).FullName, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
        Background(() => File.Exists(path), cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
        Background(() => Directory.Exists(path), cancellationToken);

    /// <inheritdoc />
    public string? GetParent(string path) => Directory.GetParent(path)?.FullName;

    /// <inheritdoc />
    public IReadOnlyList<(string Label, string Path)> GetBreadcrumbs(string path)
    {
        var crumbs = new List<(string, string)>();
        for (var directory = path.Length > 0 ? new DirectoryInfo(path) : null;
             directory is not null;
             directory = directory.Parent)
            crumbs.Insert(0, (directory.Parent is null ? directory.FullName : directory.Name, directory.FullName));
        return crumbs;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<FilePlace>> GetPlacesAsync(CancellationToken cancellationToken = default) =>
        Background(FilePlace.Default, cancellationToken);

    static string? ResolveDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full)) return full;
        return File.Exists(full) ? Path.GetDirectoryName(full) : null;
    }

    static FileEntry Describe(FileSystemInfo info)
    {
        var isDirectory = info is DirectoryInfo;
        long size = -1;
        var modified = DateTime.MinValue;
        try
        {
            if (info is FileInfo file) size = file.Length;
            modified = info.LastWriteTime;
        }
        catch (IOException)
        {
            // Keep dangling and temporarily unavailable entries in the listing.
        }

        return new FileEntry(info.FullName, info.Name, isDirectory, size, modified,
            IsHidden(info));
    }

    static bool IsHidden(FileSystemInfo info)
    {
        if (info.Name.StartsWith('.')) return true;
        try
        {
            return (info.Attributes & FileAttributes.Hidden) != 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    static ValueTask<T> Background<T>(Func<T> operation, CancellationToken cancellationToken) =>
        new(Task.Run(operation, cancellationToken));
}
