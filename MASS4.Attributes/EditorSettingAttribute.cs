namespace MASS4.Attributes;

/// <summary>
/// Declares a page of editor settings when applied to a class, or titles and describes one option
/// when applied to a member of that class. The studio instantiates an annotated class once, restores
/// its persisted values and shows its public members as a page in the Settings panel; a member with
/// no attribute of its own is still shown, titled from its name.
/// </summary>
/// <example>
/// <code>
/// [EditorSetting("My Tools/Formatting", Description = "How generated files are laid out.")]
/// public sealed class FormattingSettings
/// {
///     [EditorSetting("Indent Width", Description = "Spaces per indent level.")]
///     public int IndentWidth { get; set; } = 4;
/// }
/// </code>
/// </example>
/// <param name="path">
/// On a class: the '/'-separated place in the category tree, e.g. <c>My Tools/Formatting</c>, whose
/// last segment is the page title. On a member: the label drawn above its editor, or empty to keep
/// the humanized member name.
/// </param>
[SuppressPrivate]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false, Inherited = false)]
[PublicAPI]
public sealed class EditorSettingAttribute(string path = "") : Attribute
{
    /// <summary>The category path (on a class), or the display label (on a member).</summary>
    public string Path { get; } = path;

    /// <summary>A sentence shown under the page title, or under an option's own title.</summary>
    public string Description { get; set; } = "";

    /// <summary>Sort order among sibling pages; ties fall back to the title. Meaningful only on a class.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Stores the page with the project rather than with the user, so everyone who opens the project
    /// gets the same values. Off by default: a preference follows the person, not the work. Meaningful
    /// only on a class.
    /// </summary>
    public bool Workspace { get; set; }
}
