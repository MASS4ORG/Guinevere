namespace MASS4.Attributes;

/// <summary>
/// Creates a menu item that will execute a method.
/// Use the path to the menu item, which is separated by '/'.
/// </summary>
///
/// <example>
/// This example demonstrates using the MenuItemAttribute for a method within an assembly:
/// <code>
/// var methods = Assembly
///     .GetExecutingAssembly()
///     .GetTypes()
///     .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
///     .Where(method => method.GetCustomAttributes(typeof(MenuItemAttribute), false).Length > 0)
///     .ToList();
/// </code>
/// </example>
///
/// <example>
/// This example demonstrates using the MenuItemAttribute within a class to create a menu option:
/// <code>
/// internal sealed partial class AboutDialog : Window
/// {
///     [MenuItem("Help/About")]
///     public static void ShowAbout(Window parent)
///     {
///         new AboutDialog().ShowDialog(parent);
///     }
///
///     public AboutDialog()
///     {
///         InitializeComponent();
///         //...
///     }
///     //...
/// }
/// </code>
/// </example>
///
/// <seealso cref="PanelAttribute"/>
/// <remarks>
/// Creates a menu item that will execute a method.
/// </remarks>
/// <param name="path">Path to the menu item.</param>
[SuppressPrivate]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MenuItemAttribute(string path) : Attribute
{
    /// <summary>
    /// The path to the menu item. The path is separated by '/'.
    /// </summary>
    ///
    /// <example>
    /// "File/Open"
    /// </example>
    public string Path { get; } = path;
}
