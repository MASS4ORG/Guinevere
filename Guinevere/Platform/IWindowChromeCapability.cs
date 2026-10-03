namespace Guinevere;

/// <summary>Optional desktop window operations used by an application-drawn title bar.</summary>
public interface IWindowChromeCapability : IWindowHandler
{
    /// <summary>Whether the window currently fills its desktop work area.</summary>
    bool IsMaximized { get; }

    /// <summary>Whether this backend can move the window in desktop coordinates.</summary>
    bool CanMove { get; }

    /// <summary>The content area's origin in desktop coordinates; requires <see cref="CanMove"/>.</summary>
    Vector2 Position { get; set; }

    /// <summary>The pointer in desktop coordinates; requires <see cref="CanMove"/>.</summary>
    Vector2 PointerPosition { get; }

    /// <summary>Minimizes the window.</summary>
    void Minimize();

    /// <summary>Maximizes the window to the desktop work area.</summary>
    void Maximize();

    /// <summary>Returns the window to its normal size and state.</summary>
    void Restore();

    /// <summary>Requests a close through the application's unsaved-work guard.</summary>
    void RequestClose();
}
