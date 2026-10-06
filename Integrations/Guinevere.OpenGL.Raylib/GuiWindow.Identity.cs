using Raylib_cs;

namespace Guinevere;

public partial class GuiWindow
{
    string _title = "";

    /// <inheritdoc />
    public string Title
    {
        get => _title;
        set
        {
            Raylib.SetWindowTitle(value);
            _title = value;
        }
    }

    /// <inheritdoc />
    public unsafe void SetIcon(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaPixels.Length != checked(width * height * 4))
            throw new ArgumentException("The icon must contain four channels per pixel.", nameof(rgbaPixels));
        fixed (byte* pixels = rgbaPixels)
        {
            var icon = new Image
            {
                Data = pixels,
                Width = width,
                Height = height,
                Mipmaps = 1,
                Format = PixelFormat.UncompressedR8G8B8A8
            };
            Raylib.SetWindowIcon(icon);
        }
    }
}
