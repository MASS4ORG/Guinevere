namespace Guinevere;

/// <summary>The explicit outcome of a file dialog.</summary>
/// <param name="WasCancelled">Whether the user dismissed the dialog without choosing.</param>
/// <param name="Path">The selected path, or null when cancelled.</param>
public sealed record FileDialogResult(bool WasCancelled, string? Path)
{
    /// <summary>A cancelled dialog result.</summary>
    public static FileDialogResult Cancelled { get; } = new(true, null);

    /// <summary>Creates a successful selection result.</summary>
    public static FileDialogResult Selected(string path) => new(false, path);
}
