using System.Reflection;

namespace Example_76_Odin_Attributes;

/// <summary>One entry of the tour: a sidebar group, a title, a one-line summary and the demo class it shows.</summary>
/// <param name="Group">The sidebar group.</param>
/// <param name="Title">The sidebar label and page heading.</param>
/// <param name="Summary">What the page demonstrates.</param>
/// <param name="Demo">The demo class, or null for an attribute that is coming soon.</param>
public sealed record DemoPage(string Group, string Title, string Summary, Type? Demo = null)
{
    /// <summary>Whether the page has a working demo.</summary>
    public bool IsAvailable => Demo is not null;

    /// <summary>
    /// The demo class's source, read from the embedded copy of its file without the namespace preamble, so the
    /// code on screen is the code that builds the form.
    /// </summary>
    public string Source()
    {
        if (Demo is null) return "";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Demos.{Demo.Name}.cs");
        if (stream is null) return "";

        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().ReplaceLineEndings("\n").Split('\n')
            .SkipWhile(line => line.StartsWith("using ", StringComparison.Ordinal)
                               || line.StartsWith("namespace ", StringComparison.Ordinal)
                               || line.Length == 0);
        return string.Join('\n', lines).TrimEnd();
    }
}
