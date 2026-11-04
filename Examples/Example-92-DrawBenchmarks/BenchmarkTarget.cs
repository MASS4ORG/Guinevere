extern alias OpenGlBackend;
extern alias VulkanBackend;

using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;

namespace Guinevere;

/// <summary>Creates an offscreen raster surface or a hidden window using the production GPU integrations.</summary>
sealed class BenchmarkTarget : IDisposable
{
    SKSurface? _surface;
    readonly IWindow? _window;
    readonly ICanvasRenderer? _renderer;
    readonly GL? _gl;
    readonly bool _synchronize;

    /// <summary>Creates the requested backend and rejects silent CPU fallback in GPU measurements.</summary>
    public BenchmarkTarget(int width, int height, string backend, bool synchronize = false)
    {
        _synchronize = synchronize;
        if (backend == "raster")
        {
            _surface = SKSurface.Create(new SKImageInfo(width, height));
            return;
        }
        if (backend is not ("opengl" or "vulkan"))
        {
            throw new ArgumentException("Backend must be raster, opengl or vulkan.", nameof(backend));
        }

        var options = backend == "vulkan" ? WindowOptions.DefaultVulkan : WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.IsVisible = false;
        options.VSync = false;
        _window = Window.Create(options);
        try
        {
            _window.Initialize();
            if (backend == "opengl")
            {
                _gl = _window.CreateOpenGL();
                var renderer = new OpenGlBackend::Guinevere.CanvasRenderer();
                _renderer = renderer;
                renderer.Initialize(width, height, _gl);
                if (!renderer.IsGpuAccelerated)
                {
                    throw new InvalidOperationException("OpenGL Skia GPU unavailable.");
                }
            }
            else
            {
                var renderer = new VulkanBackend::Guinevere.CanvasRenderer();
                _renderer = renderer;
                renderer.Initialize(width, height, _window);
                if (!renderer.IsGpuAccelerated)
                {
                    throw new InvalidOperationException("Vulkan Skia GPU unavailable.");
                }
            }
            if (synchronize)
            {
                _renderer.Render(canvas =>
                {
                    var context = canvas.Surface?.Context ??
                                  throw new InvalidOperationException("GPU context unavailable.");
                    _surface = SKSurface.Create(context, false, new SKImageInfo(width, height)) ??
                               throw new InvalidOperationException("Offscreen GPU surface unavailable.");
                });
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Draws and flushes the frame using the selected production backend.</summary>
    public void Render(Action<SKCanvas> draw)
    {
        if (_surface is not null)
        {
            draw(_surface.Canvas);
            if (_synchronize)
            {
                (_surface.Context as GRContext)?.Flush(true, true);
            }
        }
        else if (_renderer is not null)
        {
            _renderer.Render(draw);
        }
        else
        {
            draw(_surface!.Canvas);
        }
    }

    /// <summary>Disposes native graphics resources before destroying their window context.</summary>
    public void Dispose()
    {
        _surface?.Dispose();
        _renderer?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();
    }
}
