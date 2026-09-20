using Raylib_cs;
using SkiaSharp;

namespace Guinevere;

/// <inheritdoc />
public class CanvasRenderer : ICanvasRenderer
{
    SKSurface? _surface;
    SKCanvas? _canvas;
    Texture2D _texture;
    bool _textureLoaded;
    int _width, _height;

    /// <inheritdoc />
    public void Initialize(int width, int height)
    {
        _width = width;
        _height = height;

        // Create CPU-based surface for Raylib integration
        var imageInfo = new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(imageInfo);
        _canvas = _surface?.Canvas;
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        if (_width == width && _height == height)
            return;

        _width = width;
        _height = height;

        // Dispose old surface and canvas
        _canvas = null;
        _surface?.Dispose();
        ReleaseTexture();

        // Create new surface with new dimensions
        var imageInfo = new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(imageInfo);
        _canvas = _surface?.Canvas;
    }

    /// <inheritdoc />
    public void Render(Action<SKCanvas> draw)
    {
        if (_canvas == null || _surface == null)
            return;

        _canvas.Clear(SKColors.Black);
        draw(_canvas);
        _canvas.Flush();

        using var image = _surface.Snapshot();
        using var pixels = image.PeekPixels();

        if (pixels != null)
        {
            unsafe
            {
                // Rgba8888 exposes bytes in R, G, B, A order, exactly matching Raylib's
                // UncompressedR8G8B8A8 input. Upload directly while the pixmap is alive.
                var pixelData = (byte*)pixels.GetPixels().ToPointer();
                var width = pixels.Width;
                var height = pixels.Height;
                if (pixels.RowBytes != width * 4)
                    throw new InvalidOperationException("Raylib requires tightly packed RGBA pixels.");

                var rlImg = new Image
                {
                    Data = pixelData,
                    Width = width,
                    Height = height,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };

                if (_textureLoaded)
                    Raylib.UpdateTexture(_texture, pixelData);
                else
                {
                    _texture = Raylib.LoadTextureFromImage(rlImg);
                    _textureLoaded = true;
                }
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Raylib_cs.Color.Black);
                Raylib.DrawTexture(_texture, 0, 0, Raylib_cs.Color.White);
                Raylib.EndDrawing();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _canvas = null;
        _surface?.Dispose();
        ReleaseTexture();
    }

    void ReleaseTexture()
    {
        if (!_textureLoaded) return;
        Raylib.UnloadTexture(_texture);
        _textureLoaded = false;
        _texture = default;
    }
}
