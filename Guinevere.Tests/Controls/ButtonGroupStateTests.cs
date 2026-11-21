namespace Guinevere.Tests.Controls;

/// <summary>The roving tab stop of a button group: enabled-item search, tab-stop settling and the key listener.</summary>
public sealed class ButtonGroupStateTests
{
    static readonly ButtonGroupItem[] Items = ["A", "B", "C", "D"];

    [Theory]
    [InlineData("TTT", 0, 1, 1)]
    [InlineData("TTT", 2, 1, 0)]
    [InlineData("TTT", 0, -1, 2)]
    [InlineData("TFT", 0, 1, 2)]
    [InlineData("TFT", 2, -1, 0)]
    [InlineData("FTF", 1, 1, 1)]
    [InlineData("FFF", 0, 1, -1)]
    [InlineData("", 0, 1, -1)]
    public void NextEnabledWrapsAndSkipsDisabledItems(string flags, int from, int step, int expected)
    {
        var enabled = flags.Select(static c => c == 'T').ToArray();

        Assert.Equal(expected, ButtonGroupState.NextEnabled(enabled, from, step));
    }

    [Theory]
    [InlineData("FTTF", false, 1)]
    [InlineData("FTTF", true, 2)]
    [InlineData("TFFT", false, 0)]
    [InlineData("TFFT", true, 3)]
    [InlineData("FF", false, -1)]
    public void EdgeEnabledFindsTheEnabledEnds(string flags, bool last, int expected)
    {
        var enabled = flags.Select(static c => c == 'T').ToArray();

        Assert.Equal(expected, ButtonGroupState.EdgeEnabled(enabled, last));
    }

    /// <summary>The tab stop starts on the first enabled item, and moves off an item that becomes disabled.</summary>
    [Fact]
    public void BeginSettlesTheTabStopOnAnEnabledItem()
    {
        var state = new ButtonGroupState();
        ButtonGroupItem[] firstDisabled = [new("A", Enabled: false), "B", "C"];

        state.Begin(firstDisabled, true, false, -1, false);
        Assert.Equal(1, state.Active);

        state.Begin(Items, true, false, -1, true);
        Assert.Equal(1, state.Active);

        state.Begin(Items, false, true, -1, true);
        Assert.Equal((-1, true), (state.Active, state.Vertical));
        Assert.All(state.Enabled, Assert.False);
    }

    /// <summary>Outside the group the stop rests on the selected item; inside, it stays where it was.</summary>
    [Fact]
    public void BeginFollowsTheSelectionOnlyWhileFocusIsOutside()
    {
        var state = new ButtonGroupState();
        state.Begin(Items, true, false, 2, false);
        Assert.Equal(2, state.Active);

        state.Begin(Items, true, false, 3, true);
        Assert.Equal(2, state.Active);

        state.Begin(Items, true, false, 3, false);
        Assert.Equal(3, state.Active);

        state.Begin(Items, true, false, 9, false);
        Assert.Equal(3, state.Active);
    }

    /// <summary>The group's own arrows, Home and End move the stop and are consumed; other keys pass through.</summary>
    [Theory]
    [InlineData(false, KeyboardKey.Right, 1, true)]
    [InlineData(false, KeyboardKey.Left, 3, true)]
    [InlineData(false, KeyboardKey.End, 3, true)]
    [InlineData(false, KeyboardKey.Down, 0, false)]
    [InlineData(true, KeyboardKey.Down, 1, true)]
    [InlineData(true, KeyboardKey.Up, 3, true)]
    [InlineData(true, KeyboardKey.Right, 0, false)]
    [InlineData(false, KeyboardKey.A, 0, false)]
    public void KeyHandlerMovesTheStopAlongTheAxis(bool vertical, KeyboardKey key, int expected, bool consumed)
    {
        var state = new ButtonGroupState();
        state.Begin(Items, true, vertical, -1, false);
        var e = new KeyDownEvent { Key = key };

        state.KeyHandler(e);

        Assert.Equal((expected, consumed, consumed), (state.Active, e.DefaultPrevented, state.FocusPending));
        Assert.Same(state.KeyHandler, state.KeyHandler);
    }

    /// <summary>Home returns to the first enabled item; with nothing enabled no key does anything.</summary>
    [Fact]
    public void KeyHandlerHomeAndAnEmptyGroup()
    {
        var state = new ButtonGroupState();
        state.Begin(Items, true, false, 3, false);
        state.KeyHandler(new KeyDownEvent { Key = KeyboardKey.Home });
        Assert.Equal(0, state.Active);

        state.Begin(Items, false, false, -1, false);
        var e = new KeyDownEvent { Key = KeyboardKey.Right };
        state.KeyHandler(e);
        Assert.False(e.DefaultPrevented);
    }
}
