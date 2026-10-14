using Guinevere;

namespace Example_01;

public partial class Shared
{
    static readonly int[,] ControllerGridNeighbors =
    {
        { 0, 1, 0, 3 },
        { 0, 2, 1, 4 },
        { 1, 2, 2, 4 },
        { 3, 4, 0, 3 },
        { 3, 4, 1, 4 }
    };
    static readonly FocusDirection[] ControllerGridDirections =
        [FocusDirection.Left, FocusDirection.Right, FocusDirection.Up, FocusDirection.Down];

    /// <summary>Displays connected controller state and a touch gesture surface.</summary>
    internal static void InputDevices(Gui gui, ColorPalette p)
    {
        gui.GestureRecognizer ??= new TouchGestureRecognizer();
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            var state = gui.GetData(new InputDeviceDemoState());
            gui.ScrollY(p.Track, p.Border);
            gui.DrawText("Connect a USB controller; move its sticks and press its buttons.", 15, p.Text);
            DrawControllerGrid(gui, p, state);
            DrawConnectedDevices(gui, p);

            var node = gui.Node().ExpandWidth().Height(180);
            using (node.Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 8);
                gui.On<TouchEvent>(e =>
                {
                    e.CapturePointer();
                    state.Contact = $"Contact {e.Contact.Id}: {e.Contact.Phase}, pressure {e.Contact.Pressure?.ToString("0.00") ?? "unavailable"}";
                });
                gui.On<TouchGestureEvent>(e =>
                    state.Gesture = $"{e.Gesture.Kind}: scale {e.Gesture.Scale:0.00}, rotation {e.Gesture.Rotation:0.00}");
                gui.DrawText(state.Contact, 14, p.TextDim);
                gui.DrawText(state.Gesture, 16, p.Accent);
            }
            if (!gui.Platform.Supports<ITouchCapability>())
                gui.DrawText("This backend exposes tablet input through the mouse; separate touch contacts are unavailable.",
                    14, p.TextDim);
        }
    }

    static void DrawConnectedDevices(Gui gui, ColorPalette p)
    {
        if (!gui.Platform.TryGet<IGameInputCapability>(out var game) || game is null)
        {
            gui.DrawText("Controller capability unavailable", 14, p.TextDim);
            return;
        }
        if (game.Devices.Count == 0) gui.DrawText("No controllers connected", 14, p.TextDim);
        foreach (var device in game.Devices) DrawConnectedDevice(gui, p, device);
    }

    static void DrawConnectedDevice(Gui gui, ColorPalette p, GameInputDevice device)
    {
        using var key = gui.ItemKey(device.Id);
        using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Padding(12).Gap(6).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 8);
            gui.DrawText($"{device.Name} [{device.Id}]", 15, p.Text);
            gui.DrawText(device.IsMapped ? "Mapped gamepad: navigation enabled" : "Raw joystick: application mapping required",
                14, p.TextDim);
            gui.DrawText($"Axes: {string.Join(", ", device.Axes.Select(axis => $"{axis:0.00}"))}", 14, p.TextDim);
            gui.DrawText($"Buttons: {string.Join(", ", device.Buttons.Select((button, index) =>
                button.Down ? $"{index} ({button.HeldSeconds:0.0}s)" : "").Where(value => value.Length > 0))}",
                14, p.TextDim);
            gui.DrawText($"Hats: {string.Join(", ", device.Hats)}; touchpad contacts: {device.TouchpadContacts.Count}",
                14, p.TextDim);
        }
    }

    static void DrawControllerGrid(Gui gui, ColorPalette p, InputDeviceDemoState state)
    {
        gui.DrawText($"D-pad / left stick: move focus; A: activate; B: cancel. Activations: {state.Activations}",
            14, p.Text);
        using (gui.Node(id: "controller-navigation-grid").ExpandWidth().Height(140).Enter())
        {
            var controls = new LayoutNode[5];
            for (var i = 0; i < 5; i++)
            {
                using var key = gui.ItemKey(i);
                var node = gui.Node(90, 36).Absolute(10 + i % 3 * 110, 10 + i / 3 * 65 + i % 2 * 10);
                controls[i] = node;
                using (node.Enter())
                {
                    if (gui.Pressable().Activated) state.Activations++;
                    gui.DrawBackgroundRect(gui.HasFocus() ? p.Accent : p.SurfaceRaised, 8);
                    gui.DrawText($"Control {i + 1}", 14, gui.HasFocus() ? p.AccentText : p.Text);
                }
            }
            LinkControllerGrid(gui, controls);
        }
    }

    /// <summary>Keeps directional travel inside the grid, retaining focus when an edge has no neighbor.</summary>
    static void LinkControllerGrid(Gui gui, LayoutNode[] controls)
    {
        for (var i = 0; i < controls.Length; i++)
            for (var direction = 0; direction < ControllerGridDirections.Length; direction++)
                gui.Focus.SetDirectionalNeighbor(controls[i].Id, ControllerGridDirections[direction],
                    controls[ControllerGridNeighbors[i, direction]].Id);
    }

    sealed class InputDeviceDemoState
    {
        public string Contact { get; set; } = "No touch contact";
        public string Gesture { get; set; } = "Touch the surface to see gestures";
        public int Activations { get; set; }
    }
}
