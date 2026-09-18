using JetBrains.Annotations;

namespace Guinevere.Tests.Mocks;

/// <summary>
/// Mock implementation of ICanvasRenderer for testing rendering behavior.
/// </summary>
public class MockCanvasRenderer : ICanvasRenderer
{
    SKSurface? _surface;
    SKCanvas? _canvas;

    /// <summary>
    /// Gets the width of the canvas surface.
    /// </summary>
    [UsedImplicitly]
    public int Width { get; private set; }

    /// <summary>
    /// Gets the height of the canvas surface.
    /// </summary>
    [UsedImplicitly]
    public int Height { get; private set; }

    /// <summary>
    /// Gets the underlying SkiaSharp canvas.
    /// </summary>
    public SKCanvas Canvas => _canvas ?? throw new InvalidOperationException("Canvas not initialized");

    /// <summary>
    /// Initializes the mock canvas with the specified dimensions.
    /// </summary>
    public void Initialize(int width, int height)
    {
        Width = width;
        Height = height;
        CreateSurface(width, height);
    }

    /// <summary>
    /// Resizes the canvas surface to the new dimensions.
    /// </summary>
    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        _surface?.Dispose();
        _canvas?.Dispose();
        CreateSurface(width, height);
    }

    /// <summary>
    /// Executes a draw action on the canvas.
    /// </summary>
    public void Render(Action<SKCanvas> draw)
    {
        if (_canvas == null)
            throw new InvalidOperationException("Canvas not initialized");

        draw(_canvas);
    }

    void CreateSurface(int width, int height)
    {
        var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(imageInfo);
        _canvas = _surface.Canvas;

        // Clear any existing clip and set exact bounds
        _canvas.RestoreToCount(0);
        _canvas.Save();
        _canvas.ClipRect(new SKRect(0, 0, width, height));
    }

    /// <summary>
    /// Disposes the canvas and surface resources.
    /// </summary>
    public void Dispose()
    {
        _canvas?.Dispose();
        _surface?.Dispose();
    }
}
