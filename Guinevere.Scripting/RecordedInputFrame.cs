using System.Collections.ObjectModel;

namespace Guinevere;

/// <summary>Owns the input delivered at one frame boundary, including native event order when available.</summary>
public sealed class RecordedInputFrame
{
    /// <summary>Copies a frame and its device snapshots so later polling cannot mutate playback.</summary>
    public RecordedInputFrame(double deltaSeconds, int width, int height, float scaleFactor,
        Vector2 position, Vector2 previous, float wheel, string text,
        IEnumerable<KeyboardKey> heldKeys, IEnumerable<KeyboardKey> pressedKeys,
        IEnumerable<MouseButton> heldButtons, IEnumerable<MouseButton> pressedButtons,
        IEnumerable<GuiInputEvent>? events = null, IEnumerable<GameInputDevice>? devices = null,
        IEnumerable<GameDeviceConnection>? connections = null, IEnumerable<TouchContact>? contacts = null,
        IEnumerable<TouchGesture>? gestures = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(heldKeys);
        ArgumentNullException.ThrowIfNull(pressedKeys);
        ArgumentNullException.ThrowIfNull(heldButtons);
        ArgumentNullException.ThrowIfNull(pressedButtons);
        ValidateViewport(deltaSeconds, width, height, scaleFactor);
        DeltaSeconds = deltaSeconds;
        Width = width;
        Height = height;
        ScaleFactor = scaleFactor;
        Position = position;
        Previous = previous;
        Wheel = wheel;
        Text = text;
        HeldKeys = Array.AsReadOnly(heldKeys.ToArray());
        PressedKeys = Array.AsReadOnly(pressedKeys.ToArray());
        HeldButtons = Array.AsReadOnly(heldButtons.ToArray());
        PressedButtons = Array.AsReadOnly(pressedButtons.ToArray());
        Events = Copy(events);
        Devices = Copy(devices?.Select(CopyDevice));
        Connections = Copy(connections);
        Contacts = Copy(contacts);
        Gestures = Copy(gestures);
    }

    static ReadOnlyCollection<T> Copy<T>(IEnumerable<T>? values) => Array.AsReadOnly(values?.ToArray() ?? []);

    static void ValidateViewport(double deltaSeconds, int width, int height, float scaleFactor)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 || width <= 0 || height <= 0
            || !float.IsFinite(scaleFactor) || scaleFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Invalid frame time or viewport.");
        }
    }

    static GameInputDevice CopyDevice(GameInputDevice device) => device with
    {
        Buttons = Array.AsReadOnly(device.Buttons.ToArray()),
        Axes = Array.AsReadOnly(device.Axes.ToArray()),
        Hats = Array.AsReadOnly(device.Hats.ToArray()),
        TouchpadContacts = Array.AsReadOnly(device.TouchpadContacts.ToArray())
    };

    /// <summary>Recorded target time delta.</summary>
    public double DeltaSeconds { get; }
    /// <summary>Recorded logical viewport width.</summary>
    public int Width { get; }
    /// <summary>Recorded logical viewport height.</summary>
    public int Height { get; }
    /// <summary>Recorded display scale.</summary>
    public float ScaleFactor { get; }
    /// <summary>Pointer position.</summary>
    public Vector2 Position { get; }
    /// <summary>Previous pointer position.</summary>
    public Vector2 Previous { get; }
    /// <summary>Wheel delta.</summary>
    public float Wheel { get; }
    /// <summary>Typed characters for the frame.</summary>
    public string Text { get; }
    /// <summary>Held keyboard keys.</summary>
    public ReadOnlyCollection<KeyboardKey> HeldKeys { get; }
    /// <summary>Keyboard press edges.</summary>
    public ReadOnlyCollection<KeyboardKey> PressedKeys { get; }
    /// <summary>Held pointer buttons.</summary>
    public ReadOnlyCollection<MouseButton> HeldButtons { get; }
    /// <summary>Pointer press edges.</summary>
    public ReadOnlyCollection<MouseButton> PressedButtons { get; }
    /// <summary>Native journal in arrival order; empty for polling-only hosts.</summary>
    public ReadOnlyCollection<GuiInputEvent> Events { get; }
    /// <summary>Copied controller snapshots, including raw hats and touchpad contacts.</summary>
    public ReadOnlyCollection<GameInputDevice> Devices { get; }
    /// <summary>Device connection edges.</summary>
    public ReadOnlyCollection<GameDeviceConnection> Connections { get; }
    /// <summary>Ordered touch contacts.</summary>
    public ReadOnlyCollection<TouchContact> Contacts { get; }
    /// <summary>Host gestures.</summary>
    public ReadOnlyCollection<TouchGesture> Gestures { get; }

    /// <summary>Copies this input with the virtual time actually delivered to the target.</summary>
    public RecordedInputFrame WithDelta(double deltaSeconds) => new(deltaSeconds, Width, Height, ScaleFactor,
        Position, Previous, Wheel, Text, HeldKeys, PressedKeys, HeldButtons, PressedButtons, Events,
        Devices, Connections, Contacts, Gestures);

    /// <summary>Conservative payload charge for recording limits, excluding consumer references.</summary>
    public long RetainedBytes => 512L + Text.Length * 2L + (HeldKeys.Count + PressedKeys.Count) * 4L
        + (HeldButtons.Count + PressedButtons.Count) * 4L + Events.Sum(static e => 96L + (e.Text?.Length ?? 0) * 2L)
        + Devices.Sum(static d => 256L + (d.Id.Length + d.Name.Length) * 2L + d.Buttons.Count * 32L
            + d.Axes.Count * 4L + d.Hats.Count * 8L + d.TouchpadContacts.Count * 32L)
        + Connections.Sum(static c => 48L + c.DeviceId.Length * 2L) + Contacts.Count * 32L + Gestures.Count * 48L;
}

/// <summary>Copies polling state once while preserving a host's separately collected native journal.</summary>
public static class InputFrameRecorder
{
    static readonly KeyboardKey[] Keys = [.. Enum.GetValues<KeyboardKey>().Where(static key => (int)key >= 0)];
    static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();

    /// <summary>Captures this boundary; typed text is consumed exactly once from the source handler.</summary>
    public static RecordedInputFrame Capture(IInputHandler input, double deltaSeconds, int width, int height,
        float scaleFactor = 1, IEnumerable<GuiInputEvent>? events = null,
        IGameInputCapability? game = null, ITouchCapability? touch = null) =>
        new(deltaSeconds, width, height, scaleFactor, input.MousePosition, input.PrevMousePosition,
            input.MouseWheelDelta, input.GetTypedCharacters(), Keys.Where(input.IsKeyDown),
            Keys.Where(input.IsKeyPressed), Buttons.Where(input.IsMouseButtonDown),
            Buttons.Where(input.IsMouseButtonPressed), events, game?.Devices, game?.Connections,
            touch?.Contacts, touch?.Gestures);
}
