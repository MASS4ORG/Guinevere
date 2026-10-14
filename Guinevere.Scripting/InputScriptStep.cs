namespace Guinevere;

/// <summary>One action in an <see cref="InputScript"/>. Only the fields an action uses are read.</summary>
public sealed record InputScriptStep
{
    /// <summary>Controller identity for device actions.</summary>
    public string DeviceId { get; init; } = "controller-0";
    /// <summary>Mapped controller button.</summary>
    public GameButton GameButton { get; init; }
    /// <summary>Whether a controller button is held.</summary>
    public bool Down { get; init; }
    /// <summary>Controller axis index.</summary>
    public int Axis { get; init; }
    /// <summary>Controller axis value.</summary>
    public float AxisValue { get; init; }
    /// <summary>Touch contact identity.</summary>
    public long ContactId { get; init; }
    /// <summary>Touch lifecycle phase.</summary>
    public TouchPhase Phase { get; init; }
    /// <summary>Touch pressure, when available.</summary>
    public float? Pressure { get; init; }
    /// <summary>High-level gesture kind.</summary>
    public TouchGestureKind GestureKind { get; init; }
    /// <summary>Gesture scale ratio.</summary>
    public float Scale { get; init; } = 1;
    /// <summary>Gesture rotation delta in radians.</summary>
    public float Rotation { get; init; }
    /// <summary>What this step does.</summary>
    public InputAction Action { get; init; }

    /// <summary>Pointer X for <see cref="InputAction.Move"/>.</summary>
    public float X { get; init; }

    /// <summary>Pointer Y for <see cref="InputAction.Move"/>.</summary>
    public float Y { get; init; }

    /// <summary>Which button a press, release or click uses.</summary>
    public MouseButton Button { get; init; } = MouseButton.Left;

    /// <summary>Which key a key, hold or unhold uses.</summary>
    public KeyboardKey Key { get; init; }

    /// <summary>Characters for <see cref="InputAction.Type"/>.</summary>
    public string? Text { get; init; }

    /// <summary>Output path for <see cref="InputAction.Dump"/>.</summary>
    public string? Path { get; init; }

    /// <summary>The expected value for an <c>Expect*</c> action.</summary>
    public string? Value { get; init; }

    /// <summary>Frame count for <see cref="InputAction.Frames"/>.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Wheel movement for <see cref="InputAction.Scroll"/>.</summary>
    public float Delta { get; init; }

    /// <summary>Free-text note, ignored during playback.</summary>
    public string? Comment { get; init; }
}
