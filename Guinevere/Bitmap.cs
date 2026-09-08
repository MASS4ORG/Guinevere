namespace Guinevere;

/// <summary>
/// A GPU-agnostic image handle backed by an <see cref="SKImage"/>. Load one from a file, a stream,
/// encoded bytes, or raw pixels, then draw it with <see cref="Gui.DrawImage(SKImage, Rect, Rect?, Color?, float)"/>
/// or the <c>Image</c> control.
/// </summary>
public sealed class Bitmap : IDisposable
{
    private bool _disposed;

    /// <summary>The underlying Skia image.</summary>
    public SKImage Image { get; }

    /// <summary>Image width in pixels.</summary>
    public int Width => Image.Width;

    /// <summary>Image height in pixels.</summary>
    public int Height => Image.Height;

    private Bitmap(SKImage image) => Image = image;

    /// <summary>Wraps an existing <see cref="SKImage"/>. The texture takes ownership and disposes it.</summary>
    /// <param name="image">The image to wrap.</param>
    public static Bitmap FromImage(SKImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new Bitmap(image);
    }

    /// <summary>Loads an encoded image (PNG, JPEG, WebP, …) from a file.</summary>
    /// <param name="path">Path to the image file.</param>
    public static Bitmap FromFile(string path)
    {
        using var data = SKData.Create(path);
        return Decode(data, path);
    }

    /// <summary>Loads an encoded image from a stream.</summary>
    /// <param name="stream">A readable stream positioned at the start of the encoded image.</param>
    public static Bitmap FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var data = SKData.Create(stream);
        return Decode(data, "<stream>");
    }

    /// <summary>Loads an encoded image from a byte span.</summary>
    /// <param name="encoded">The encoded image bytes.</param>
    public static Bitmap FromEncoded(ReadOnlySpan<byte> encoded)
    {
        using var data = SKData.CreateCopy(encoded.ToArray());
        return Decode(data, "<bytes>");
    }

    /// <summary>Builds a texture from raw, tightly-packed pixels.</summary>
    /// <param name="info">Describes the pixel dimensions and format.</param>
    /// <param name="pixels">Tightly-packed pixel data matching <paramref name="info"/>.</param>
    public static Bitmap FromPixels(SKImageInfo info, ReadOnlySpan<byte> pixels)
    {
        var expected = info.RowBytes * info.Height;
        if (pixels.Length < expected)
            throw new ArgumentException($"Expected at least {expected} bytes, got {pixels.Length}", nameof(pixels));

        using var skData = SKData.CreateCopy(pixels.ToArray());
        var image = SKImage.FromPixels(info, skData, info.RowBytes)
                    ?? throw new InvalidOperationException("Could not create an image from the supplied pixels");
        return new Bitmap(image);
    }

    private static Bitmap Decode(SKData? data, string source)
    {
        var image = data is null ? null : SKImage.FromEncodedData(data);
        return image is null
            ? throw new InvalidOperationException($"Could not decode an image from '{source}'")
            : new Bitmap(image);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Image.Dispose();
    }
}
