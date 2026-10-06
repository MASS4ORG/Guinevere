using OpenTK.Windowing.Common.Input;

namespace Guinevere;

public partial class GuiWindow
{
    /// <inheritdoc />
    public void SetIcon(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaPixels.Length != checked(width * height * 4))
            throw new ArgumentException("The icon must contain four channels per pixel.", nameof(rgbaPixels));
        Icon = new WindowIcon(new Image(width, height, rgbaPixels.ToArray()));
    }
}
