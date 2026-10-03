namespace Guinevere;

public static partial class ControlsExtensions
{
    static void AppBarResizeHandles(Gui gui, AppBarState state, IWindowChromeCapability? window,
        bool enabled, Vector2? minimumWindowSize, string id)
    {
        var minimum = minimumWindowSize ?? new Vector2(160, 100);
        ValidateWindowMinimum(minimum);
        if (gui.Pass == Pass.Pass1Build) PrepareWindowResize(gui, state, window, enabled);
        if (!state.ResizeEnabled) return;

        foreach (var handle in ResizeGeometry.Handles)
        {
            var rect = ResizeGeometry.HandleRect(gui.ScreenRect, handle.Edge);
            using var node = gui.Node(rect.W, rect.H, $"{id}/resize/{handle.Edge}")
                .AbsoluteScreen(rect.X, rect.Y).BlockInput().Cursor(handle.Cursor).Enter();
            gui.SetZIndex(int.MaxValue - 1);
            gui.SetEscapesAncestorClips();
            if (gui.Pass == Pass.Pass2Render)
                DragWindowResize(gui, state, window!, handle.Edge, minimum);
        }
    }

    static void ValidateWindowMinimum(Vector2 minimum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum.X);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum.Y);
        if (!float.IsFinite(minimum.X) || !float.IsFinite(minimum.Y))
            throw new ArgumentOutOfRangeException(nameof(minimum));
    }

    static void PrepareWindowResize(Gui gui, AppBarState state, IWindowChromeCapability? window, bool enabled)
    {
        gui.Platform.TryGet<IWindowResizeCapability>(out var resize);
        state.ResizeWindow = resize;
        state.ResizeEnabled = enabled && window is { CanMove: true, IsMaximized: false }
            && resize?.CanResize == true;
    }

    static void DragWindowResize(Gui gui, AppBarState state, IWindowChromeCapability window,
        ResizeEdge edge, Vector2 minimum)
    {
        if (!gui.GetInteractable().OnDrag(out _)) return;
        var resize = state.ResizeWindow!;
        var pointer = window.PointerPosition;
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            state.ResizeBounds = new Rect(window.Position.X, window.Position.Y, resize.ClientSize.X, resize.ClientSize.Y);
            state.ResizePointer = pointer;
        }

        var bounds = ResizeGeometry.Resize(state.ResizeBounds, pointer - state.ResizePointer, edge, minimum);
        var size = new Vector2(bounds.W, bounds.H);
        if (resize.ClientSize != size) resize.ClientSize = size;
        if (window.Position != bounds.Position) window.Position = bounds.Position;
    }
}
