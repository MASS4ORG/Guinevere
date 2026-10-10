using System.Runtime.InteropServices;
using Raylib_cs;
using SkiaSharp;

namespace Guinevere;

/// <summary>
/// Renders Guinevere frames into Raylib's window. Skia draws on the GPU into Raylib's OpenGL default framebuffer; when a
/// Skia GL context cannot be created, or <c>GUINEVERE_RENDERER=raster</c> is set, Skia rasterizes on the CPU and the
/// result is uploaded as a Raylib texture.
/// </summary>
public class CanvasRenderer : ICanvasRenderer
{
    SKSurface? _surface;
    SKCanvas? _canvas;
    SkiaGlTarget? _gpu;
    Texture2D _texture;
    bool _textureLoaded;
    int _width, _height;

    /// <summary>Whether Skia draws on the GPU; false when it rasterizes on the CPU and the result is uploaded.</summary>
    public bool IsGpuAccelerated => _gpu is not null;

    /// <inheritdoc />
    public void Initialize(int width, int height)
    {
        _width = width;
        _height = height;

        _gpu = SkiaGlTarget.TryCreate(GetProcAddress);
        if (_gpu is not null)
        {
            _gpu.Resize(width, height);
            return;
        }

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

        if (_gpu is not null)
        {
            _gpu.Resize(width, height);
            return;
        }

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
        if (_gpu is not null)
        {
            Raylib.BeginDrawing();
            // Raylib changes GL state Skia does not track, so Skia re-sends its state every frame.
            _gpu.Render(draw, SKColors.Black, resetState: true);
            Raylib.EndDrawing();
            return;
        }

        if (_canvas == null || _surface == null)
            return;

        _canvas.Clear(SKColors.Black);
        draw(_canvas);
        _canvas.Flush();

        // The surface is read in place; it is not drawn again until the upload below has consumed it.
        using var pixels = _surface.PeekPixels();

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
        _gpu?.Dispose();
        _gpu = null;
        ReleaseTexture();
    }

    void ReleaseTexture()
    {
        if (!_textureLoaded) return;
        Raylib.UnloadTexture(_texture);
        _textureLoaded = false;
        _texture = default;
    }

    /// <summary>Raylib's GL loader; resolves entry points of its current context.</summary>
    [DllImport("raylib", EntryPoint = "rlGetProcAddress")]
    static extern IntPtr GetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
}
