namespace Guinevere;

/// <summary>The column a browsed directory is ordered by. Directories always sort before files.</summary>
public enum FileSortColumn
{
    /// <summary>The entry name, case-insensitively.</summary>
    Name,

    /// <summary>The file length.</summary>
    Size,

    /// <summary>The last write time.</summary>
    Modified,
}
