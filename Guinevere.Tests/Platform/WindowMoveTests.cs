using System.Reflection;
using System.Runtime.CompilerServices;
using Silk.NET.Windowing;
using NativeMouseButton = Silk.NET.Input.MouseButton;

namespace Guinevere.Tests.Platform;

/// <summary>Checks native move boundaries without creating a desktop window or issuing native messages.</summary>
public sealed class WindowMoveTests
{
    /// <summary>A missing X11 display or window rejects movement before calling into Xlib.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void X11MoveRejectsMissingNativeHandles(int display, int window)
    {
        Assert.False(X11WindowPosition.TryBeginMove(display, (nuint)window, new Vector2(-1920, 40)));
    }

    /// <summary>A missing Windows window never starts the native interactive move loop.</summary>
    [Fact]
    public void WindowsMoveRejectsMissingNativeHandle()
    {
        Assert.False(WindowsWindowMove.TryBegin(0));
    }

    /// <summary>Other operating systems reject Windows handles before loading user32.</summary>
    [Fact]
    public void WindowsMoveRejectsUnsupportedOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(WindowsWindowMove.TryBegin(1));
    }

    /// <summary>An integration without native handles can decline movement before its input is initialized.</summary>
    [Fact]
    public void SilkWindowMoveRejectsUninitializedNativeWindow()
    {
        var window = CreateWindow();
        Assert.False(window.TryBeginMove());
    }

    /// <summary>A new left press resets the previous native drag and records the fresh gesture.</summary>
    [Fact]
    public void SilkLeftPressResetsNativeMoveAndRecordsInput()
    {
        var window = CreateWindow();
        SetField(window, "_nativeX11Move", true);

        InvokeMouseEvent(window, "OnMouseDown", NativeMouseButton.Left);

        Assert.False(GetNativeMove(window));
        Assert.True(window.IsMouseButtonPressed(MouseButton.Left));
        Assert.True(window.IsMouseButtonDown(MouseButton.Left));
    }

    /// <summary>Left release clears held input and completes any pending native drag without a native handle.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SilkLeftReleaseClearsHeldInputAndNativeMove(bool pendingMove)
    {
        var window = CreateWindow();
        InvokeMouseEvent(window, "OnMouseDown", NativeMouseButton.Left);
        SetField(window, "_nativeX11Move", pendingMove);

        InvokeMouseEvent(window, "OnMouseUp", NativeMouseButton.Left);

        Assert.False(window.IsMouseButtonDown(MouseButton.Left));
        Assert.False(GetNativeMove(window));
    }

    /// <summary>Releasing another button preserves the left gesture owned by the desktop.</summary>
    [Fact]
    public void SilkRightReleasePreservesPendingLeftMove()
    {
        var window = CreateWindow();
        InvokeMouseEvent(window, "OnMouseDown", NativeMouseButton.Left);
        InvokeMouseEvent(window, "OnMouseDown", NativeMouseButton.Right);
        SetField(window, "_nativeX11Move", true);

        InvokeMouseEvent(window, "OnMouseUp", NativeMouseButton.Right);

        Assert.False(window.IsMouseButtonDown(MouseButton.Right));
        Assert.True(window.IsMouseButtonDown(MouseButton.Left));
        Assert.True(GetNativeMove(window));
    }

    static GuiWindow CreateWindow()
    {
        var window = (GuiWindow)RuntimeHelpers.GetUninitializedObject(typeof(GuiWindow));
        SetField(window, "_window", Substitute.For<IWindow>());
        SetField(window, "_pressedButtons", new HashSet<NativeMouseButton>());
        SetField(window, "_heldButtons", new HashSet<NativeMouseButton>());
        SetField(window, "<Pacing>k__BackingField", new FramePacer());
        return window;
    }

    static void SetField(GuiWindow window, string name, object value) =>
        typeof(GuiWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

    static bool GetNativeMove(GuiWindow window) =>
        (bool)typeof(GuiWindow).GetField("_nativeX11Move", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;

    static void InvokeMouseEvent(GuiWindow window, string name, NativeMouseButton button) =>
        typeof(GuiWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [null, button]);

    /// <summary>Existing chrome implementations opt out of interactive movement by default.</summary>
    [Fact]
    public void ChromeCapabilityDefaultsToUnsupportedNativeMovement()
    {
        IWindowChromeCapability chrome = new LegacyChrome();
        Assert.False(chrome.TryBeginMove());
    }

    sealed class LegacyChrome : IWindowChromeCapability
    {
        /// <inheritdoc />
        public bool IsMaximized => false;
        /// <inheritdoc />
        public bool CanMove => true;
        /// <inheritdoc />
        public Vector2 Position { get; set; }
        /// <inheritdoc />
        public Vector2 PointerPosition => Vector2.Zero;
        /// <inheritdoc />
        public void DrawWindowTitlebar(bool show) { }
        /// <inheritdoc />
        public void Minimize() { }
        /// <inheritdoc />
        public void Maximize() { }
        /// <inheritdoc />
        public void Restore() { }
        /// <inheritdoc />
        public void RequestClose() { }
    }
}
