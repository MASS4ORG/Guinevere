namespace Guinevere;

/// <summary>
/// Defines an interface for handling window-specific operations and management.
/// Provides methods for controlling window appearance and behavior across different rendering backends.
/// </summary>
public interface IWindowHandler : IPlatformCapability
{
    /// <summary>
    /// Shows or hides the window title bar.
    /// </summary>
    /// <param name="show">True to show the title bar; false to hide it.</param>
    void DrawWindowTitlebar(bool show);
}
