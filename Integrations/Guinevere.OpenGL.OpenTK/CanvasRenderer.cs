using OpenTK.Graphics.OpenGL4;
using SkiaSharp;

namespace Guinevere;

/// <inheritdoc />
public class CanvasRenderer : ICanvasRenderer
{
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private uint _texture;
    private int _vao, _vbo, _shaderProgram;
    private int _width, _height;

    /// <inheritdoc />
    public void Initialize(int width, int height)
    {
        _width = width;
        _height = height;

        // Init Skia
        _surface = SKSurface.Create(
            new SKImageInfo(width, height, SKColorType.Rgba8888));
        _canvas = _surface?.Canvas;

        SetupTexture();
        SetupShaders();
        SetupQuad();
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
        _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888));
        _canvas = _surface?.Canvas;

        // Update texture
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexImage2D(TextureTarget.Texture2D,
            0, PixelInternalFormat.Rgba,
            _width, _height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            IntPtr.Zero);

        // Update viewport
        GL.Viewport(0, 0, width, height);
    }

    private void SetupQuad()
    {
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

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        var ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            vertices.Length * sizeof(float),
            vertices,
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer,
            indices.Length * sizeof(uint),
            indices,
            BufferUsageHint.StaticDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        GL.BindVertexArray(0);
    }

    private void SetupTexture()
    {
        // Create an empty texture of the right size
        _texture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _texture);

        // Initialize with empty data
        GL.TexImage2D(TextureTarget.Texture2D,
            0, PixelInternalFormat.Rgba,
            _width, _height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    }

    private void SetupShaders()
    {
        var vertexShaderSource = GuiWindow.GetStringResource("Guinevere.shader.vert");
        var fragmentShaderSource = GuiWindow.GetStringResource("Guinevere.shader.frag");
        _shaderProgram = CompileShader(vertexShaderSource, fragmentShaderSource);
    }

    /// <inheritdoc />
    public void Render(Action<SKCanvas> draw)
    {
        if (_canvas == null || _surface == null)
            return;

        _canvas?.Clear(SKColors.Black);
        draw(_canvas!);
        _canvas?.Flush();

        using var image = _surface?.Snapshot();
        using var pixels = image?.PeekPixels();
        if (pixels == null) return;

        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexSubImage2D(TextureTarget.Texture2D,
            0, 0, 0,
            pixels.Width, pixels.Height,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            pixels.GetPixels());

        GL.Clear(ClearBufferMask.ColorBufferBit);

        // Draw the quad with the texture
        GL.UseProgram(_shaderProgram);
        GL.BindVertexArray(_vao);
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.DrawElements(PrimitiveType.Triangles,
            6, DrawElementsType.UnsignedInt, 0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _canvas = null;
        _surface?.Dispose();
        GL.DeleteTexture(_texture);
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shaderProgram);
    }

    private int CompileShader(string vsSrc, string fsSrc)
    {
        var vs = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vs, vsSrc);
        GL.CompileShader(vs);
        CheckShader(vs);

        var fs = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fs, fsSrc);
        GL.CompileShader(fs);
        CheckShader(fs);

        var prog = GL.CreateProgram();
        GL.AttachShader(prog, vs);
        GL.AttachShader(prog, fs);
        GL.LinkProgram(prog);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        return prog;
    }

    private void CheckShader(int shader)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var ok);
        if (ok == 0)
            throw new Exception(GL.GetShaderInfoLog(shader));
    }
}
