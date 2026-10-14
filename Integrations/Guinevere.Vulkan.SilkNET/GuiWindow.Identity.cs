using Silk.NET.Core;

namespace Guinevere;

public partial class GuiWindow
{
    RawImage? _pendingIcon;

    /// <inheritdoc />
    public string Title
    {
        get => _window.Title;
        set => _window.Title = value;
    }

    /// <inheritdoc />
    public void SetIcon(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaPixels.Length != checked(width * height * 4))
        {
            throw new ArgumentException("The icon must contain four channels per pixel.", nameof(rgbaPixels));
        }

        _pendingIcon = new RawImage(width, height, rgbaPixels.ToArray());
        ApplyPendingIcon();
    }

    void ApplyPendingIcon()
    {
        if (!_window.IsInitialized)
        {
            return;
        }

        SetPendingIcon();
    }

    void SetPendingIcon()
    {
        if (!_pendingIcon.HasValue)
        {
            return;
        }

        var icon = _pendingIcon.Value;
        _pendingIcon = null;
        _window.SetWindowIcon([icon]);
    }
}
