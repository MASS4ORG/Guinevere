using Silk.NET.OpenGL;
using SkiaSharp;

namespace Guinevere;

/// <summary>
/// Renders Guinevere frames into the window's OpenGL default framebuffer. Skia draws on the GPU through a GL context;
/// when one cannot be created, or <c>GUINEVERE_RENDERER=raster</c> is set, Skia rasterizes on the CPU and the result is
/// uploaded as a texture and drawn as a full-screen quad.
/// </summary>
public class CanvasRenderer : ICanvasRenderer
{
    const uint Rgba8 = 0x8058;
    const GLEnum StencilAttachment = (GLEnum)0x1802;
    const GLEnum StencilSize = (GLEnum)0x2217;

    SKSurface? _surface;
    SKCanvas? _canvas;
    GL _gl = null!;
    GRContext? _grContext;
    bool _resetGpuState;
    uint _texture;
    uint _shaderProgram;
    uint _vao, _vbo;
    int _width;
    int _height;

    /// <summary>Whether Skia draws on the GPU; false when it rasterizes on the CPU and the result is uploaded.</summary>
    public bool IsGpuAccelerated => _grContext is not null;

    /// <summary>
    /// Configures and initializes the renderer with a specified width, height, and OpenGL (GL) context.
    /// </summary>
    /// <param name="width">The width of the rendering surface in pixels.</param>
    /// <param name="height">The height of the rendering surface in pixels.</param>
    /// <param name="gl">The OpenGL context used for rendering operations.</param>
    public void Initialize(int width, int height, GL gl)
    {
        _gl = gl;
        if (Environment.GetEnvironmentVariable("GUINEVERE_RENDERER") != "raster") CreateGpuContext();

        Initialize(width, height);

        if (IsGpuAccelerated) return;
        // Enable blending for transparency
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    void CreateGpuContext()
    {
        try
        {
            using var glInterface = GRGlInterface.Create(name => _gl.Context.GetProcAddress(name));
            _grContext = glInterface is null ? null : GRContext.CreateGl(glInterface);
        }
        catch (Exception)
        {
            _grContext = null;
        }
    }

    /// <summary>Wraps the default framebuffer, with the sample count and stencil depth the window was created with.</summary>
    void CreateGpuSurface()
    {
        _gl.GetInteger(GetPName.Samples, out var samples);
        _gl.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer, StencilAttachment, StencilSize,
            out var stencil);
        using var target = new GRBackendRenderTarget(_width, _height, samples, stencil,
            new GRGlFramebufferInfo(0, Rgba8));
        _surface = SKSurface.Create(_grContext!, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface?.Canvas;
    }

    unsafe void SetupTexture()
    {
        // Create an empty texture of the right size
        _texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        // Initialize with empty data. The Skia surface is RGBA8888 and uploads are RGBA, so the
        // placeholder allocation matches.
        _gl.TexImage2D(TextureTarget.Texture2D,
            0, InternalFormat.Rgba,
            (uint)_width, (uint)_height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    }

    void SetupShaders()
    {
        // Load Shaders and compile the program
        var vertexShaderSource = GuiWindow.GetStringResource("Guinevere.shader.vert");
        var fragmentShaderSource = GuiWindow.GetStringResource("Guinevere.shader.frag");

        // Create the vertex shader
        var vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);

        // Check compilation
        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out var vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Vertex shader failed to compile: " + _gl.GetShaderInfoLog(vertexShader));

        // Create the fragment shader
        var fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        // Check compilation
        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out var fStatus);
        if (fStatus != (int)GLEnum.True)
            throw new Exception("Fragment shader failed to compile: " + _gl.GetShaderInfoLog(fragmentShader));

        // Create and link the program
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);
    }

    unsafe void SetupQuad()
    {
        // Full screen quad vertices (position + texture coordinates)
        float[] vertices =
        [
            // positions     // texture coords
            1.0f, 1.0f, 1.0f, 1.0f, // top right
            1.0f, -1.0f, 1.0f, 0.0f, // bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, // bottom left
            -1.0f, 1.0f, 0.0f, 1.0f // top left
        ];

        uint[] indices =
        [
            0, 1, 3, // first triangle
            1, 2, 3 // second triangle
        ];
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        var ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* buffer = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                buffer,
                BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* buffer = indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)),
                buffer,
                BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        _gl.BindVertexArray(0);
    }

    /// <inheritdoc/>
    public unsafe void Render(Action<SKCanvas> draw)
    {
        if (_canvas == null || _surface == null)
            return;

        if (_grContext is not null)
        {
            // The window changes GL state (viewport) on resize, outside Skia's tracking.
            if (_resetGpuState) _grContext.ResetContext();
            _resetGpuState = false;
            _canvas.Clear(SKColors.Black);
            draw(_canvas);
            _grContext.Flush();
            return;
        }

        // Clear canvas and draw using Skia
        _canvas.Clear(SKColors.Black);
        draw(_canvas);
        _canvas.Flush();

        // The surface is read in place; it is not drawn again until the upload below has consumed it.
        using var pixels = _surface.PeekPixels();
        if (pixels == null) return;

        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        var pixelFormat = PixelFormat.Rgba;

        _gl.TexSubImage2D(TextureTarget.Texture2D,
            0, 0, 0,
            (uint)pixels.Width, (uint)pixels.Height,
            pixelFormat, PixelType.UnsignedByte,
            (void*)pixels.GetPixels());

        _gl.Clear(ClearBufferMask.ColorBufferBit);

        // Draw the quad with the texture
        _gl.UseProgram(_shaderProgram);
        _gl.BindVertexArray(_vao);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.DrawElements(PrimitiveType.Triangles,
            6, DrawElementsType.UnsignedInt, null);
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == _width && height == _height))
            return;

        _width = width;
        _height = height;

        _surface?.Dispose();
        if (_grContext is not null)
        {
            _resetGpuState = true;
            CreateGpuSurface();
            return;
        }

        _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888));
        _canvas = _surface?.Canvas;

        // Recreate texture with a new size
        _gl.DeleteTexture(_texture);
        SetupTexture();
    }

    /// <inheritdoc/>
    public void Initialize(int width, int height)
    {
        _width = width;
        _height = height;

        if (_grContext is not null)
        {
            CreateGpuSurface();
            return;
        }

        _surface = SKSurface.Create(
            new SKImageInfo(width, height, SKColorType.Rgba8888));
        _canvas = _surface?.Canvas;

        // Set up OpenGL resources
        SetupTexture();
        SetupShaders();
        SetupQuad();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _canvas = null;
        _surface?.Dispose();
        if (_grContext is not null)
        {
            _grContext.Dispose();
            _grContext = null;
            return;
        }

        // Clean up OpenGL resources
        _gl.DeleteTexture(_texture);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteProgram(_shaderProgram);
    }
}
