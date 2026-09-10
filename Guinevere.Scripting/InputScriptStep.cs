namespace Guinevere;

/// <summary>One action in an <see cref="InputScript"/>. Only the fields an action uses are read.</summary>
public sealed record InputScriptStep
{
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
