using SkiaSharp;

namespace Guinevere;

/// <summary>
/// Skia drawing on the GPU into the current OpenGL context's default framebuffer. Shared by the OpenGL integrations,
/// which supply the context's procedure lookup; GL is queried through raw entry points so no binding is needed.
/// </summary>
sealed unsafe class SkiaGlTarget : IDisposable
{
    const uint Rgba8 = 0x8058;
    const uint Samples = 0x80A9;
    const uint Framebuffer = 0x8D40;
    const uint Stencil = 0x1802;
    const uint StencilSize = 0x2217;

    readonly GRContext _context;
    readonly delegate* unmanaged<uint, int*, void> _getIntegerv;
    readonly delegate* unmanaged<uint, uint, uint, int*, void> _getAttachmentParameter;
    SKSurface? _surface;
    bool _resetState;

    SkiaGlTarget(GRContext context, IntPtr getIntegerv, IntPtr getAttachmentParameter)
    {
        _context = context;
        _getIntegerv = (delegate* unmanaged<uint, int*, void>)getIntegerv;
        _getAttachmentParameter = (delegate* unmanaged<uint, uint, uint, int*, void>)getAttachmentParameter;
    }

    /// <summary>
    /// Creates the target on the current context, or returns null when GPU drawing is unavailable or
    /// <c>GUINEVERE_RENDERER=raster</c> asks for CPU rasterization.
    /// </summary>
    /// <param name="getProcAddress">Looks up a GL entry point of the current context by name.</param>
    public static SkiaGlTarget? TryCreate(Func<string, IntPtr> getProcAddress)
    {
        if (Environment.GetEnvironmentVariable("GUINEVERE_RENDERER") == "raster") return null;
        try
        {
            var getIntegerv = getProcAddress("glGetIntegerv");
            var getAttachment = getProcAddress("glGetFramebufferAttachmentParameteriv");
            if (getIntegerv == IntPtr.Zero || getAttachment == IntPtr.Zero) return null;
            // GLX-based lookups return a stub for any name; a fake EGL query crashes Skia, which needs no EGL here.
            using var glInterface = GRGlInterface.Create(name =>
                name.StartsWith("egl", StringComparison.Ordinal) ? IntPtr.Zero : getProcAddress(name));
            if (glInterface is null) return null;
            var context = GRContext.CreateGl(glInterface);
            return context is null ? null : new SkiaGlTarget(context, getIntegerv, getAttachment);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Wraps the default framebuffer at the new size, with its actual sample count and stencil depth.</summary>
    public void Resize(int width, int height)
    {
        _surface?.Dispose();
        int samples = 0, stencil = 0;
        _getIntegerv(Samples, &samples);
        _getAttachmentParameter(Framebuffer, Stencil, StencilSize, &stencil);
        using var target = new GRBackendRenderTarget(width, height, samples, stencil,
            new GRGlFramebufferInfo(0, Rgba8));
        _surface = SKSurface.Create(_context, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        // The window changes GL state (viewport) on resize, outside Skia's tracking.
        _resetState = true;
    }

    /// <summary>Draws one frame and submits it; the caller swaps buffers.</summary>
    /// <param name="draw">The frame's drawing.</param>
    /// <param name="clear">The color the frame starts from.</param>
    /// <param name="resetState">Whether other code touched GL state since the last frame.</param>
    public void Render(Action<SKCanvas> draw, SKColor clear, bool resetState = false)
    {
        if (_surface is null) return;
        if (resetState || _resetState) _context.ResetContext();
        _resetState = false;
        var canvas = _surface.Canvas;
        canvas.Clear(clear);
        draw(canvas);
        _context.Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;
        _context.Dispose();
    }
}
