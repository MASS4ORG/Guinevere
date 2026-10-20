namespace Guinevere;

/// <summary>Modifier keys held with a key.</summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Control, or Command on macOS.</summary>
    Control = 1,

    /// <summary>Shift.</summary>
    Shift = 2,

    /// <summary>Alt, or Option on macOS.</summary>
    Alt = 4,
}

/// <summary>A key pressed with a set of modifiers.</summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifiers held with it.</param>
public readonly record struct KeyChord(KeyboardKey Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>The modifiers held now; Command counts as Control on macOS.</summary>
    public static KeyModifiers Held(IInputHandler input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = OperatingSystem.IsMacOS()
            && EitherKeyDown(input, KeyboardKey.LeftSuper, KeyboardKey.RightSuper);
        return (command || EitherKeyDown(input, KeyboardKey.LeftControl, KeyboardKey.RightControl)
                   ? KeyModifiers.Control
                   : KeyModifiers.None)
               | (EitherKeyDown(input, KeyboardKey.LeftShift, KeyboardKey.RightShift)
                   ? KeyModifiers.Shift
                   : KeyModifiers.None)
               | (EitherKeyDown(input, KeyboardKey.LeftAlt, KeyboardKey.RightAlt)
                   ? KeyModifiers.Alt
                   : KeyModifiers.None);
    }

    static bool EitherKeyDown(IInputHandler input, KeyboardKey left, KeyboardKey right)
        => input.IsKeyDown(left) || input.IsKeyDown(right);
}

/// <summary>Binds key chords to editor command names; each editor can carry its own copy.</summary>
public sealed class EditorKeymap
{
    const KeyModifiers Ctrl = KeyModifiers.Control;
    const KeyModifiers Shift = KeyModifiers.Shift;
    const KeyModifiers Alt = KeyModifiers.Alt;

    readonly Dictionary<KeyChord, string> _bindings = [];

    /// <summary>Every binding.</summary>
    public IReadOnlyDictionary<KeyChord, string> Bindings => _bindings;

    /// <summary>Binds a chord to a command, replacing any previous binding.</summary>
    public EditorKeymap Bind(KeyChord chord, string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _bindings[chord] = command;
        return this;
    }

    /// <summary>Removes the binding of a chord.</summary>
    public bool Unbind(KeyChord chord) => _bindings.Remove(chord);

    /// <summary>The command bound to a chord, or null.</summary>
    public string? CommandFor(KeyChord chord) => _bindings.GetValueOrDefault(chord);

    /// <summary>Bindings in the style of Visual Studio Code.</summary>
    public static EditorKeymap CreateDefault()
    {
        var map = new EditorKeymap();
        BindMovement(map, KeyboardKey.Left, "cursorLeft", "cursorWordLeft");
        BindMovement(map, KeyboardKey.Right, "cursorRight", "cursorWordRight");
        BindMovement(map, KeyboardKey.Up, "cursorUp", null);
        BindMovement(map, KeyboardKey.Down, "cursorDown", null);
        BindMovement(map, KeyboardKey.Home, "cursorHome", "cursorTop");
        BindMovement(map, KeyboardKey.End, "cursorEnd", "cursorBottom");
        BindMovement(map, KeyboardKey.PageUp, "cursorPageUp", null);
        BindMovement(map, KeyboardKey.PageDown, "cursorPageDown", null);
        return map
            .Bind(new(KeyboardKey.Backspace), "deleteLeft")
            .Bind(new(KeyboardKey.Backspace, Shift), "deleteLeft")
            .Bind(new(KeyboardKey.Backspace, Ctrl), "deleteWordLeft")
            .Bind(new(KeyboardKey.Delete), "deleteRight")
            .Bind(new(KeyboardKey.Delete, Ctrl), "deleteWordRight")
            .Bind(new(KeyboardKey.Enter), "newline")
            .Bind(new(KeyboardKey.KeyPadEnter), "newline")
            .Bind(new(KeyboardKey.Enter, Shift), "newline")
            .Bind(new(KeyboardKey.Tab), "tab")
            .Bind(new(KeyboardKey.Tab, Shift), "outdent")
            .Bind(new(KeyboardKey.RightBracket, Ctrl), "indentLines")
            .Bind(new(KeyboardKey.LeftBracket, Ctrl), "outdentLines")
            .Bind(new(KeyboardKey.A, Ctrl), "selectAll")
            .Bind(new(KeyboardKey.L, Ctrl), "selectLine")
            .Bind(new(KeyboardKey.Z, Ctrl), "undo")
            .Bind(new(KeyboardKey.Z, Ctrl | Shift), "redo")
            .Bind(new(KeyboardKey.Y, Ctrl), "redo")
            .Bind(new(KeyboardKey.C, Ctrl), "copy")
            .Bind(new(KeyboardKey.Insert, Ctrl), "copy")
            .Bind(new(KeyboardKey.X, Ctrl), "cut")
            .Bind(new(KeyboardKey.V, Ctrl), "paste")
            .Bind(new(KeyboardKey.Insert, Shift), "paste")
            .Bind(new(KeyboardKey.K, Ctrl | Shift), "deleteLines")
            .Bind(new(KeyboardKey.Down, Shift | Alt), "copyLinesDown")
            .Bind(new(KeyboardKey.Up, Shift | Alt), "copyLinesUp")
            .Bind(new(KeyboardKey.Up, Alt), "moveLinesUp")
            .Bind(new(KeyboardKey.Down, Alt), "moveLinesDown")
            .Bind(new(KeyboardKey.Slash, Ctrl), "toggleLineComment")
            .Bind(new(KeyboardKey.Up, Ctrl | Alt), "addCursorAbove")
            .Bind(new(KeyboardKey.Down, Ctrl | Alt), "addCursorBelow")
            .Bind(new(KeyboardKey.D, Ctrl), "selectNextOccurrence")
            .Bind(new(KeyboardKey.L, Ctrl | Shift), "selectAllOccurrences")
            .Bind(new(KeyboardKey.Escape), "cancel")
            .Bind(new(KeyboardKey.Backslash, Ctrl | Shift), "jumpToBracket")
            .Bind(new(KeyboardKey.F, Ctrl), "find")
            .Bind(new(KeyboardKey.H, Ctrl), "replace")
            .Bind(new(KeyboardKey.F3), "findNext")
            .Bind(new(KeyboardKey.F3, Shift), "findPrevious")
            .Bind(new(KeyboardKey.Space, Ctrl), "triggerSuggest")
            .Bind(new(KeyboardKey.LeftBracket, Ctrl | Shift), "fold")
            .Bind(new(KeyboardKey.RightBracket, Ctrl | Shift), "unfold");
    }

    /// <summary>Binds a movement, its selecting variant with Shift, and their Control variants.</summary>
    static void BindMovement(EditorKeymap map, KeyboardKey key, string command, string? controlCommand)
    {
        map.Bind(new(key), command).Bind(new(key, Shift), command + "Select");
        if (controlCommand is not null)
        {
            map.Bind(new(key, Ctrl), controlCommand).Bind(new(key, Ctrl | Shift), controlCommand + "Select");
        }
    }
}
