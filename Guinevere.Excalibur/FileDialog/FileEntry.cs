namespace Guinevere;

/// <summary>One row of a browsed directory: a file or a subdirectory.</summary>
/// <param name="FullPath">The entry's absolute path.</param>
/// <param name="Name">The file or directory name, without its parent.</param>
/// <param name="IsDirectory">Whether the entry is a directory.</param>
/// <param name="Size">Length in bytes, or -1 for a directory and for a file that could not be read.</param>
/// <param name="Modified">Last write time, local.</param>
/// <param name="IsHidden">Whether the provider considers the entry hidden.</param>
public sealed record FileEntry(string FullPath, string Name, bool IsDirectory, long Size, DateTime Modified,
    bool IsHidden = false);
