namespace Guinevere;

/// <summary>
/// Supplies filesystem data to a <see cref="FileBrowser"/>. Implementations may represent the local
/// disk, an archive, a remote asset store, or a test fixture.
/// </summary>
public interface IFileDialogFileSystem
{
    /// <summary>Resolves a directory, or the parent of a file, to an absolute provider path.</summary>
    ValueTask<string?> ResolveDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Reads one directory. The returned snapshot may be filtered and sorted by the browser.</summary>
    ValueTask<IReadOnlyList<FileEntry>> EnumerateAsync(string path,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a directory and returns its normalized path.</summary>
    ValueTask<string> CreateDirectoryAsync(string parentPath, string name,
        CancellationToken cancellationToken = default);

    /// <summary>Reports whether a file currently exists.</summary>
    ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Reports whether a directory currently exists.</summary>
    ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Returns the parent path, or null for a root.</summary>
    string? GetParent(string path);

    /// <summary>Splits a path into clickable breadcrumb segments.</summary>
    IReadOnlyList<(string Label, string Path)> GetBreadcrumbs(string path);

    /// <summary>Returns platform roots and quick-access locations.</summary>
    ValueTask<IReadOnlyList<FilePlace>> GetPlacesAsync(CancellationToken cancellationToken = default);
}
