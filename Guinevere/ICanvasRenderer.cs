namespace Guinevere;

/// <summary>
/// Defines the interface for rendering graphics to a canvas. Provides methods for initializing,
/// resizing, and rendering content onto the canvas surface.
/// </summary>
public interface ICanvasRenderer : IDisposable, IPlatformCapability
{
    /// <summary>
    /// Initializes the canvas with the specified width and height.
    /// </summary>
    /// <param name="width">The width of the canvas in pixels. Must be greater than zero.</param>
    /// <param name="height">The height of the canvas in pixels. Must be greater than zero.</param>
    void Initialize(int width, int height);

    /// <summary>
    /// Resizes the canvas to the specified width and height.
    /// </summary>
    /// <param name="width">The new width of the canvas. Must be greater than zero.</param>
    /// <param name="height">The new height of the canvas. Must be greater than zero.</param>
    void Resize(int width, int height);

    /// <summary>
    /// Renders the graphical content to the canvas using the provided draw action.
    /// </summary>
    /// <param name="draw">The action that defines the drawing logic on the canvas.</param>
    void Render(Action<SKCanvas> draw);
}
