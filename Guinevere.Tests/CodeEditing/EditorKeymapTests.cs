namespace Guinevere.Tests.CodeEditing;

/// <summary>Key binding customization and platform-aware modifier detection.</summary>
public class EditorKeymapTests
{
    /// <summary>Default maps can be customized independently and bindings can be removed.</summary>
    [Fact]
    public void BindingsCanBeReplacedAndRemovedWithoutChangingOtherMaps()
    {
        var first = EditorKeymap.CreateDefault();
        var second = EditorKeymap.CreateDefault();
        var chord = new KeyChord(KeyboardKey.F, KeyModifiers.Control);
        Assert.Equal("find", first.CommandFor(chord));
        Assert.Equal("cursorWordLeftSelect", first.CommandFor(new(KeyboardKey.Left,
            KeyModifiers.Control | KeyModifiers.Shift)));
        Assert.Same(first, first.Bind(chord, "custom"));
        Assert.Equal("custom", first.Bindings[chord]);
        Assert.Equal("find", second.CommandFor(chord));
        Assert.True(first.Unbind(chord));
        Assert.False(first.Unbind(chord));
        Assert.Null(first.CommandFor(chord));
        Assert.Throws<ArgumentException>(() => first.Bind(chord, " "));
    }

    /// <summary>Either side of each modifier is recognized; Super maps to Control on macOS.</summary>
    [Theory]
    [InlineData(KeyboardKey.LeftControl, KeyModifiers.Control)]
    [InlineData(KeyboardKey.RightControl, KeyModifiers.Control)]
    [InlineData(KeyboardKey.LeftShift, KeyModifiers.Shift)]
    [InlineData(KeyboardKey.RightShift, KeyModifiers.Shift)]
    [InlineData(KeyboardKey.LeftAlt, KeyModifiers.Alt)]
    [InlineData(KeyboardKey.RightAlt, KeyModifiers.Alt)]
    [InlineData(KeyboardKey.LeftSuper, KeyModifiers.None)]
    [InlineData(KeyboardKey.RightSuper, KeyModifiers.None)]
    public void ModifiersUseTheHeldKeys(KeyboardKey key, KeyModifiers expected)
    {
        var input = Substitute.For<IInputHandler>();
        Assert.Equal(KeyModifiers.None, KeyChord.Held(input));
        input.IsKeyDown(key).Returns(true);
        if (OperatingSystem.IsMacOS() && key is KeyboardKey.LeftSuper or KeyboardKey.RightSuper)
        {
            expected = KeyModifiers.Control;
        }

        Assert.Equal(expected, KeyChord.Held(input));
        input.IsKeyDown(KeyboardKey.LeftControl).Returns(true);
        input.IsKeyDown(KeyboardKey.LeftShift).Returns(true);
        input.IsKeyDown(KeyboardKey.LeftAlt).Returns(true);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt, KeyChord.Held(input));
    }
}
