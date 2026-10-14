namespace Guinevere;

/// <summary>Standard mapped controller buttons, independent of a backend's raw button numbers.</summary>
public enum GameButton
{
    /// <summary>Bottom face button.</summary>
    South,
    /// <summary>Right face button.</summary>
    East,
    /// <summary>Left face button.</summary>
    West,
    /// <summary>Top face button.</summary>
    North,
    /// <summary>Left shoulder button.</summary>
    LeftShoulder,
    /// <summary>Right shoulder button.</summary>
    RightShoulder,
    /// <summary>Back or select button.</summary>
    Back,
    /// <summary>Start or menu button.</summary>
    Start,
    /// <summary>Guide button.</summary>
    Guide,
    /// <summary>Left stick button.</summary>
    LeftStick,
    /// <summary>Right stick button.</summary>
    RightStick,
    /// <summary>D-pad up.</summary>
    Up,
    /// <summary>D-pad right.</summary>
    Right,
    /// <summary>D-pad down.</summary>
    Down,
    /// <summary>D-pad left.</summary>
    Left
}

/// <summary>A button's held state, frame edges and elapsed hold duration.</summary>
public readonly record struct GameButtonState(bool Down, bool Pressed, bool Released, double HeldSeconds);

/// <summary>An immutable sample of one connected joystick or mapped gamepad.</summary>
public sealed record GameInputDevice(
    string Id, string Name, bool IsMapped,
    IReadOnlyList<GameButtonState> Buttons, IReadOnlyList<float> Axes,
    IReadOnlyList<Vector2> Hats, IReadOnlyList<TouchContact> TouchpadContacts)
{
    /// <summary>Reads a mapped button, or an empty state when unavailable.</summary>
    public GameButtonState Button(GameButton button) =>
        IsMapped && (uint)button < (uint)Buttons.Count ? Buttons[(int)button] : default;

    /// <summary>Reads an axis with a scaled dead zone. Mapped axes are left X/Y, right X/Y and triggers.</summary>
    public float Axis(int index, float deadZone = 0.2f)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (!float.IsFinite(deadZone) || deadZone < 0 || deadZone >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(deadZone));
        }

        var value = index < Axes.Count ? Axes[index] : 0;
        return MathF.Abs(value) <= deadZone ? 0 : MathF.CopySign((MathF.Abs(value) - deadZone) / (1 - deadZone), value);
    }
}

/// <summary>A connection change accumulated until the next rendered frame.</summary>
public readonly record struct GameDeviceConnection(string DeviceId, bool Connected);

/// <summary>A raw poll result copied into an independent device snapshot.</summary>
public sealed record GameInputSample(string Id, string Name, bool IsMapped, bool[] Buttons, float[] Axes,
    Vector2[] Hats)
{
    /// <summary>Optional SDL-compatible device GUID used to recognize raw controller layouts.</summary>
    public string? MappingId { get; init; }
}

/// <summary>Optional device-level controller and joystick input.</summary>
public interface IGameInputCapability : IPlatformCapability
{
    /// <summary>Independent snapshots of all connected devices in stable connection order.</summary>
    IReadOnlyList<GameInputDevice> Devices { get; }
    /// <summary>Connect and disconnect edges for the current frame.</summary>
    IReadOnlyList<GameDeviceConnection> Connections { get; }
}

/// <summary>Collects backend polls and retains button edges until both GUI passes finish.</summary>
public sealed class GameInputState : IGameInputCapability
{
    readonly Dictionary<string, DeviceState> _devices = new();
    readonly List<GameDeviceConnection> _connections = [];
    readonly HashSet<string> _seen = [];

    /// <inheritdoc />
    public IReadOnlyList<GameInputDevice> Devices => _devices.Values.Select(state => state.Sample).ToArray();
    /// <inheritdoc />
    public IReadOnlyList<GameDeviceConnection> Connections => _connections.AsReadOnly();
    /// <summary>Whether a poll changed state or has an unconsumed edge.</summary>
    public bool Changed { get; private set; }
    /// <summary>Whether any controller button is held.</summary>
    public bool AnyButtonDown => _devices.Values.Any(state => state.Sample.Buttons.Any(button => button.Down));

    /// <summary>Whether a mapped stick is displaced enough to request continuous input frames.</summary>
    public bool AnyStickDown => _devices.Values.Any(state => state.Sample.IsMapped
        && state.Sample.Axes.Take(4).Any(axis => MathF.Abs(axis) > 0.2f));

    /// <summary>Decodes the GLFW hat bitmask to a direction with downward-positive Y.</summary>
    public static Vector2 DecodeHat(byte hat) => new(
        ((hat & 2) != 0 ? 1 : 0) - ((hat & 8) != 0 ? 1 : 0),
        ((hat & 4) != 0 ? 1 : 0) - ((hat & 1) != 0 ? 1 : 0));

    /// <summary>Starts a full device enumeration.</summary>
    public void BeginUpdate() => _seen.Clear();

    /// <summary>Enumerates backend slots, preserving edges and removing disconnected devices.</summary>
    public void Poll(Func<int, GameInputSample?> readSlot, int slotCount, double elapsed)
    {
        BeginUpdate();
        for (var slot = 0; slot < slotCount; slot++)
        {
            if (readSlot(slot) is { } sample)
            {
                sample = GamepadMappings.Normalize(sample);
                UpdateDevice(sample.Id, sample.Name, sample.IsMapped, sample.Buttons, sample.Axes, sample.Hats, elapsed);
            }
        }

        EndUpdate();
    }

    /// <summary>Waits in short event slices and polls devices so controller input can wake an idle host.</summary>
    public static void WaitForInput(Func<double> waitSeconds, Action<double> waitEvents, Action poll)
    {
        double wait;
        while ((wait = waitSeconds()) > 0)
        {
            waitEvents(Math.Min(wait, 0.008));
            poll();
        }
    }

    /// <summary>Copies native button bytes to managed held states.</summary>
    public static bool[] CopyButtons(ReadOnlySpan<byte> buttons)
    {
        var result = new bool[buttons.Length];
        for (var i = 0; i < buttons.Length; i++)
        {
            result[i] = buttons[i] != 0;
        }

        return result;
    }

    /// <summary>Copies native GLFW hat bitmasks to window-space directions.</summary>
    public static Vector2[] CopyHats(ReadOnlySpan<byte> hats)
    {
        var result = new Vector2[hats.Length];
        for (var i = 0; i < hats.Length; i++)
        {
            result[i] = DecodeHat(hats[i]);
        }

        return result;
    }

    /// <summary>Copies a device sample. Identifiers must remain stable for the lifetime of a connection.</summary>
    public void UpdateDevice(string id, string name, bool mapped, ReadOnlySpan<bool> buttons,
        ReadOnlySpan<float> axes, ReadOnlySpan<Vector2> hats, double elapsed,
        IReadOnlyList<TouchContact>? touchpadContacts = null)
    {
        _seen.Add(id);
        var state = GetOrConnect(id, name, mapped);
        var next = new GameButtonState[buttons.Length];
        for (var i = 0; i < buttons.Length; i++)
        {
            next[i] = UpdateButton(state, i, buttons[i], elapsed);
        }

        var cleanAxes = CleanAxes(axes);
        var nextHats = hats.ToArray();
        Changed |= !cleanAxes.SequenceEqual(state.Sample.Axes);
        Changed |= !nextHats.SequenceEqual(state.Sample.Hats);
        state.Sample = new GameInputDevice(id, name, mapped, Array.AsReadOnly(next), Array.AsReadOnly(cleanAxes),
            Array.AsReadOnly(nextHats), Array.AsReadOnly(touchpadContacts?.ToArray() ?? []));
    }

    DeviceState GetOrConnect(string id, string name, bool mapped)
    {
        if (!_devices.TryGetValue(id, out var state))
        {
            state = new DeviceState(new GameInputDevice(id, name, mapped, [], [], [], []));
            _devices.Add(id, state);
            _connections.Add(new GameDeviceConnection(id, true));
            Changed = true;
        }
        return state;
    }

    GameButtonState UpdateButton(DeviceState state, int index, bool down, double elapsed)
    {
        var previous = index < state.Sample.Buttons.Count ? state.Sample.Buttons[index] : default;
        var pressed = down && !previous.Down;
        var released = !down && previous.Down;
        if (pressed)
        {
            state.Started[index] = elapsed;
        }

        var held = down ? Math.Max(0, elapsed - state.Started[index]) : 0;
        Changed |= down != previous.Down;
        return new GameButtonState(down, previous.Pressed | pressed, previous.Released | released, held);
    }

    static float[] CleanAxes(ReadOnlySpan<float> axes)
    {
        var cleanAxes = axes.ToArray();
        for (var i = 0; i < cleanAxes.Length; i++)
        {
            cleanAxes[i] = float.IsFinite(cleanAxes[i]) ? Math.Clamp(cleanAxes[i], -1, 1) : 0;
        }

        return cleanAxes;
    }

    /// <summary>Disconnects devices missing from the enumeration and discards their held state.</summary>
    public void EndUpdate()
    {
        foreach (var id in _devices.Keys.Where(id => !_seen.Contains(id)).ToArray())
        {
            Disconnect(id);
        }
    }

    /// <summary>Disconnects a device and removes every held button and touchpad contact.</summary>
    public void Disconnect(string id)
    {
        if (!_devices.Remove(id))
        {
            return;
        }

        _connections.Add(new GameDeviceConnection(id, false));
        Changed = true;
    }

    /// <summary>Clears connection and button edges after a rendered frame.</summary>
    public void NewFrame()
    {
        _connections.Clear();
        Changed = false;
        foreach (var state in _devices.Values)
        {
            state.Sample = state.Sample with
            {
                Buttons = Array.AsReadOnly([
                    .. state.Sample.Buttons.Select(button => button with
                    {
                        Pressed = false, Released = false
                    })
                ])
            };
        }
    }

    sealed class DeviceState(GameInputDevice sample)
    {
        public GameInputDevice Sample { get; set; } = sample;
        public Dictionary<int, double> Started { get; } = new();
    }
}
