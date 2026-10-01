namespace Guinevere.Forms;

/// <summary>
/// Places a popup against the control that opened it. <c>Popup</c> takes a screen position and otherwise opens
/// at the window's top-left corner, nowhere near the field being edited.
/// </summary>
public static class PopupAnchor
{
    /// <summary>Height <c>Popup</c> adds above its content for the title bar.</summary>
    const float titleBarHeight = 30f;

    /// <summary>Just under <paramref name="anchor"/>, pulled back inside the window where it would overflow.</summary>
    /// <param name="gui">The GUI for this frame, for the window size.</param>
    /// <param name="anchor">Screen rect of the control the popup belongs to.</param>
    /// <param name="width">The popup's width.</param>
    /// <param name="height">The popup's content height, excluding its title bar.</param>
    /// <returns>The screen position to open at.</returns>
    public static Vector2 Below(Gui gui, Rect anchor, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var screen = gui.ScreenRect;

        return new Vector2(
            Math.Clamp(anchor.X, 0f, Math.Max(0f, screen.W - width)),
            Math.Clamp(anchor.Y + anchor.H, 0f, Math.Max(0f, screen.H - height - titleBarHeight)));
    }
}
