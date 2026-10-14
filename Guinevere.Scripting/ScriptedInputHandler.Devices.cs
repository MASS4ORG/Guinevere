namespace Guinevere;

public sealed partial class ScriptedInputHandler : IGameInputCapability, ITouchCapability
{
    readonly GameInputState _gameInput = new();
    readonly TouchInputState _touchInput = new();
    readonly Dictionary<string, ScriptedController> _controllers = new();
    readonly Dictionary<long, TouchContact> _contacts = new();
    double _deviceElapsed;
    /// <inheritdoc />
    public IReadOnlyList<GameInputDevice> Devices => _gameInput.Devices;
    /// <inheritdoc />
    public IReadOnlyList<GameDeviceConnection> Connections => _gameInput.Connections;
    /// <inheritdoc />
    public IReadOnlyList<TouchContact> Contacts => _touchInput.Contacts;
    /// <inheritdoc />
    public IReadOnlyList<TouchGesture> Gestures => _touchInput.Gestures;

    /// <summary>Connects a mapped controller with a stable script-owned identifier.</summary>
    public void ConnectController(string id, string name = "Scripted controller")
    {
        if (_controllers.ContainsKey(id)) return;
        _controllers[id] = new ScriptedController(name);
        PublishController(id);
    }

    /// <summary>Disconnects a controller and clears its held state.</summary>
    public void DisconnectController(string id)
    {
        _controllers.Remove(id);
        _gameInput.Disconnect(id);
    }

    /// <summary>Sets a mapped button on a connected controller.</summary>
    public void SetControllerButton(string id, GameButton button, bool down)
    {
        _controllers[id].Buttons[(int)button] = down;
        PublishController(id);
    }

    /// <summary>Sets an axis on a connected controller.</summary>
    public void SetControllerAxis(string id, int axis, float value)
    {
        _controllers[id].Axes[axis] = value;
        PublishController(id);
    }

    /// <summary>Queues a raw contact update for the next frame.</summary>
    public void Touch(TouchContact contact)
    {
        if (contact.Phase is TouchPhase.Ended or TouchPhase.Canceled) _contacts.Remove(contact.Id);
        else _contacts[contact.Id] = contact;
        _touchInput.AddContact(contact);
    }

    /// <summary>Queues a high-level gesture supplied by the host.</summary>
    public void Gesture(TouchGesture gesture) => _touchInput.AddGesture(gesture);

    void PublishController(string id)
    {
        var controller = _controllers[id];
        _gameInput.UpdateDevice(id, controller.Name, true, controller.Buttons, controller.Axes, [], _deviceElapsed);
    }

    void AdvanceDevices()
    {
        _deviceElapsed += 1d / 60;
        _gameInput.NewFrame();
        foreach (var id in _controllers.Keys) PublishController(id);
        _touchInput.NewFrame();
    }

    void ResetDevices()
    {
        foreach (var id in _controllers.Keys.ToArray()) DisconnectController(id);
        foreach (var contact in _contacts.Values.ToArray())
            Touch(contact with { Phase = TouchPhase.Canceled });
    }

    sealed class ScriptedController(string name)
    {
        public string Name { get; } = name;
        public bool[] Buttons { get; } = new bool[15];
        public float[] Axes { get; } = new float[6];
    }
}
