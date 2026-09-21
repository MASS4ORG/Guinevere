namespace Guinevere;

/// <summary>What one showing of a file dialog asks for, and what to do with the answer.</summary>
public sealed record FileDialogRequest
{
    /// <summary>Whether the user is choosing a file to open, a file to write, or a directory.</summary>
    public required FileDialogMode Mode { get; init; }

    /// <summary>The dialog's title bar text.</summary>
    public string Title { get; init; } = "Select";

    /// <summary>The directory to open on, or a file inside it. Falls back to the user's home.</summary>
    public string? StartPath { get; init; }

    /// <summary>The name the field starts with, for a save or a new folder.</summary>
    public string InitialName { get; init; } = "";

    /// <summary>The confirming button's label. Defaults to one that suits <see cref="Mode"/>.</summary>
    public string? ConfirmLabel { get; init; }

    /// <summary>The extension filters offered. The first is selected; none means every file.</summary>
    public IReadOnlyList<FileDialogFilter> Filters { get; init; } = [];

    /// <summary>The sidebar's shortcuts. Defaults to <see cref="FilePlace.Default"/>.</summary>
    public IReadOnlyList<FilePlace>? Places { get; init; }

    /// <summary>
    /// Called once the dialog closes, with the chosen absolute path, or null when it was cancelled.
    /// </summary>
    public Action<string?>? OnComplete { get; init; }

    /// <summary>
    /// Called once with an explicit selected or cancelled result. Prefer this when cancellation must
    /// be distinct in the consuming API; <see cref="OnComplete"/> remains a compact convenience.
    /// </summary>
    public Action<FileDialogResult>? OnClosed { get; init; }

    /// <summary>
    /// Rejects a choice before the dialog closes. Returning a message keeps the dialog open and shows
    /// it; returning null accepts. Lets a host demand, say, an empty directory without reimplementing
    /// the dialog.
    /// </summary>
    public Func<string, string?>? Validate { get; init; }
}
