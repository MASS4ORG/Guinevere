using Raylib_cs;
using SkiaSharp;

namespace Guinevere;

/// <inheritdoc />
public class CanvasRenderer : ICanvasRenderer
{
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private int _width, _height;

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
                // Get pixel data and copy it to a managed array for format conversion
                var pixelData = (byte*)pixels.GetPixels().ToPointer();
                var width = pixels.Width;
                var height = pixels.Height;
                var bytesPerPixel = 4; // RGBA
                var totalBytes = width * height * bytesPerPixel;

                // Create a byte array to hold the converted pixel data
                var convertedData = new byte[totalBytes];

                // Convert RGBA to RGBA (handle potential endianness issues)
                for (var i = 0; i < totalBytes; i += 4)
                {
                    // Skia uses BGRA on little-endian systems, Raylib expects RGBA
                    convertedData[i] = pixelData[i + 2]; // R = B from Skia
                    convertedData[i + 1] = pixelData[i + 1]; // G = G from Skia
                    convertedData[i + 2] = pixelData[i]; // B = R from Skia
                    convertedData[i + 3] = pixelData[i + 3]; // A = A from Skia
                }

                fixed (byte* dataPtr = convertedData)
                {
                    var rlImg = new Image
                    {
                        Data = dataPtr,
                        Width = width,
                        Height = height,
                        Mipmaps = 1,
                        Format = PixelFormat.UncompressedR8G8B8A8
                    };

                    var tex = Raylib_cs.Raylib.LoadTextureFromImage(rlImg);

                    Raylib_cs.Raylib.BeginDrawing();
                    Raylib_cs.Raylib.ClearBackground(Raylib_cs.Color.Black);
                    Raylib_cs.Raylib.DrawTexture(tex, 0, 0, Raylib_cs.Color.White);

                    Raylib_cs.Raylib.EndDrawing();
                    Raylib_cs.Raylib.UnloadTexture(tex);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _canvas = null;
        _surface?.Dispose();
    }
}
