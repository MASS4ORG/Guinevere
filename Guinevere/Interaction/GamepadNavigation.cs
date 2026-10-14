namespace Guinevere;

/// <summary>Shared controller navigation settings in window-independent units.</summary>
public sealed record GamepadNavigationOptions(float DeadZone = 0.25f, double RepeatDelay = 0.4,
    double RepeatInterval = 0.08);

/// <summary>Maps device snapshots to semantic actions with identical dead zones and repeats on every backend.</summary>
public sealed class GamepadNavigation : ISemanticInputCapability
{
    readonly HashSet<SemanticAction> _pressed = [];
    readonly Dictionary<(string Device, SemanticAction Action), double> _repeats = new();
    readonly HashSet<(string Device, SemanticAction Action)> _held = [];
    /// <summary>Settings sampled on each update.</summary>
    public GamepadNavigationOptions Options { get; set; } = new();
    /// <summary>Whether default controller navigation is enabled.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Whether a held direction needs future frames for repeat processing.</summary>
    public bool HasPendingRepeat => _repeats.Count > 0;
    /// <summary>Application-editable button bindings; navigation directions use D-pad and left stick.</summary>
    public IDictionary<SemanticAction, GameButton> Buttons { get; } = new Dictionary<SemanticAction, GameButton>
    {
        [SemanticAction.Accept] = GameButton.South,
        [SemanticAction.Cancel] = GameButton.East,
        [SemanticAction.Context] = GameButton.West,
        [SemanticAction.PreviousTab] = GameButton.LeftShoulder,
        [SemanticAction.NextTab] = GameButton.RightShoulder
    };
    /// <inheritdoc />
    public InputDevice ActiveDevice { get; private set; } = new("keyboard-mouse", InputDeviceKind.KeyboardMouse);
    /// <inheritdoc />
    public bool IsPressed(SemanticAction action) => _pressed.Contains(action);

    /// <summary>Samples all devices once before a GUI frame, using the host's monotonic time.</summary>
    public void Update(IGameInputCapability input, double elapsed)
    {
        ValidateOptions();
        _pressed.Clear();
        ForgetDisconnected(input.Connections);
        var current = new HashSet<(string Device, SemanticAction Action)>();
        if (Enabled)
            foreach (var device in input.Devices)
                if (device.IsMapped) UpdateDevice(device, elapsed, current);
        foreach (var key in _repeats.Keys.Where(key => !current.Contains(key)).ToArray()) _repeats.Remove(key);
        _held.Clear();
        _held.UnionWith(current);
        if (!input.Devices.Any(device => device.Id == ActiveDevice.Id))
            ActiveDevice = new InputDevice("keyboard-mouse", InputDeviceKind.KeyboardMouse);
    }

    void ForgetDisconnected(IReadOnlyList<GameDeviceConnection> connections)
    {
        foreach (var connection in connections)
        {
            if (connection.Connected) continue;
            _held.RemoveWhere(key => key.Device == connection.DeviceId);
            foreach (var key in _repeats.Keys.Where(key => key.Device == connection.DeviceId).ToArray())
                _repeats.Remove(key);
        }
    }

    void ValidateOptions()
    {
        if (!float.IsFinite(Options.DeadZone) || Options.DeadZone < 0 || Options.DeadZone >= 1)
            throw new ArgumentOutOfRangeException(nameof(Options));
        if (!double.IsFinite(Options.RepeatDelay) || Options.RepeatDelay < 0)
            throw new ArgumentOutOfRangeException(nameof(Options));
        if (!double.IsFinite(Options.RepeatInterval) || Options.RepeatInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(Options));
    }

    void UpdateDevice(GameInputDevice device, double elapsed,
        HashSet<(string Device, SemanticAction Action)> current)
    {
        foreach (var (action, button) in Buttons)
        {
            var state = device.Button(button);
            if (state.Pressed) Press(device.Id, action);
        }
        var direction = GetDirection(device);
        if (direction is { } navigation) UpdateRepeat(device.Id, navigation, elapsed, current);
    }

    SemanticAction? GetDirection(GameInputDevice device)
    {
        var vector = GetDpadDirection(device);
        if (vector == Vector2.Zero)
            vector = new Vector2(device.Axis(0, Options.DeadZone), device.Axis(1, Options.DeadZone));
        if (vector == Vector2.Zero) return null;
        if (MathF.Abs(vector.X) > MathF.Abs(vector.Y))
            return vector.X < 0 ? SemanticAction.Left : SemanticAction.Right;
        return vector.Y < 0 ? SemanticAction.Up : SemanticAction.Down;
    }

    static Vector2 GetDpadDirection(GameInputDevice device) => new(
        (device.Button(GameButton.Right).Down ? 1 : 0) - (device.Button(GameButton.Left).Down ? 1 : 0),
        (device.Button(GameButton.Down).Down ? 1 : 0) - (device.Button(GameButton.Up).Down ? 1 : 0));

    void UpdateRepeat(string id, SemanticAction action, double elapsed,
        HashSet<(string Device, SemanticAction Action)> current)
    {
        var key = (id, action);
        current.Add(key);
        if (!_held.Contains(key))
        {
            Press(id, action);
            _repeats[key] = elapsed + Options.RepeatDelay;
        }
        else if (elapsed + 1e-9 >= _repeats[key])
        {
            _pressed.Add(action);
            _repeats[key] = elapsed + Options.RepeatInterval;
        }
    }

    void Press(string id, SemanticAction action)
    {
        _pressed.Add(action);
        ActiveDevice = new InputDevice(id, InputDeviceKind.Controller);
    }
}
