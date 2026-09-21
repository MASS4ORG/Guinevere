namespace Guinevere;

/// <summary>
/// A named set of extensions a file dialog narrows its listing to, such as "Images" over
/// <c>.png</c> and <c>.jpg</c>. An empty extension list matches everything.
/// </summary>
/// <param name="Label">What the filter dropdown shows.</param>
/// <param name="Extensions">Extensions to accept, each written with its leading dot.</param>
public sealed record FileDialogFilter(string Label, IReadOnlyList<string> Extensions)
{
    /// <summary>A filter that hides nothing.</summary>
    public static FileDialogFilter All { get; } = new("All files", []);

    /// <summary>A filter over one or more extensions, each written with its leading dot.</summary>
    /// <param name="label">What the filter dropdown shows.</param>
    /// <param name="extensions">Extensions to accept.</param>
    /// <returns>The filter.</returns>
    public static FileDialogFilter Of(string label, params string[] extensions) => new(label, extensions);

    /// <summary>Whether a file name passes this filter.</summary>
    /// <param name="fileName">The name to test, with or without its directory.</param>
    /// <returns>True when the filter accepts the name.</returns>
    public bool Matches(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (Extensions.Count == 0) return true;

        var extension = Path.GetExtension(fileName);
        return Extensions.Any(candidate => extension.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
