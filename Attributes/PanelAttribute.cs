namespace MASS4.Attributes;

/// <summary>
/// Marks a class as a panel, as it will be placed in the dock. Also, create a menu item that will open the panel.
/// </summary>
///
/// <remarks>
/// Path is the path to the menu item. The path is separated by '/'.
/// </remarks>
///
/// <example>
/// ```csharp
/// [Panel("App Settings")]
/// public class AppSettingsPanel : Panel
/// {
///    public AppSettingsPanel(IAppSettings appSettings)
///    {
///    // Handle the Open menu item click event here.
///    Console.WriteLine("Open menu item clicked.");
///    }
/// }
/// ```
/// </example>
[SuppressPrivate]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PanelAttribute : Attribute
{
    /// <summary>
    /// The path to the menu item. The path is separated by '/'.
    /// </summary>
    ///
    /// <example>
    /// "File/Open"
    /// </example>
    public string Path { get; }

    /// <summary>
    /// The name of the panel. It will be used as the title of the panel and the menu item name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Create the panel using the default menu item path `Window/{name}`.
    /// </summary>
    /// <param name="name"></param>
    public PanelAttribute(string name)
    {
        Name = name;
        Path = $"Windows/{name}";
    }

    /// <summary>
    /// Create the panel using the specified menu item path.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="path"></param>
    /// <seealso cref="MenuItemAttribute"/>
    public PanelAttribute(string name, string path)
    {
        Name = name;
        Path = path;
    }
}
