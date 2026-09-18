using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the radio button and radio group: click-to-select on a shared index, the read-only
/// overload, and that disabled radios stay inert. Selection lives on the caller, so each frame is
/// a fresh gui and only the index crosses frames.
/// </summary>
public class RadioButtonTests
{
    const int Width = 400;
    const int Height = 200;

    static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    static void Frame(Gui gui, Action<Gui> draw, IInputHandler? input = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        gui.Input = input ?? NoInput();
        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
    }

    static IInputHandler NoInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        input.MouseDelta.Returns(Vector2.Zero);
        input.MouseWheelDelta.Returns(0f);
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
        input.IsAnyKeyDown.Returns(false);
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        return input;
    }

    static IInputHandler ClickAt(float x, float y)
    {
        var input = NoInput();
        input.MousePosition.Returns(new Vector2(x, y));
        input.PrevMousePosition.Returns(new Vector2(x, y));
        input.IsMouseButtonPressed(MouseButton.Left).Returns(true);
        return input;
    }

    /// <summary>
    /// Two radios stacked in a 8px-gap group: row 0 spans y [0,24] and - with a 20px radio and a
    /// 14px label, each row is 24px tall - row 1 spans y [32,56].
    /// </summary>
    static readonly (int Value, string Label)[] TwoOptions = [(0, "Light"), (1, "Dark")];

    [Fact]
    public void ClickingARadioSelectsItsValue()
    {
        var gui = CreateGui();
        var selected = 0;

        Frame(gui, g => g.RadioGroup(ref selected, TwoOptions));
        Frame(gui, g => g.RadioGroup(ref selected, TwoOptions), ClickAt(10, 40));

        Assert.Equal(1, selected);
    }

    [Fact]
    public void ClickingAFurtherRadioMovesTheSelection()
    {
        var gui = CreateGui();
        var selected = 1;

        Frame(gui, g => g.RadioGroup(ref selected, TwoOptions));
        Frame(gui, g => g.RadioGroup(ref selected, TwoOptions), ClickAt(10, 10));

        Assert.Equal(0, selected);
    }

    [Fact]
    public void TheReadOnlyOverloadReportsWhetherItsValueIsSelected()
    {
        var gui = CreateGui();
        var selected = 1;
        var firstSelected = false;
        var secondSelected = false;

        Frame(gui, g =>
        {
            firstSelected = g.RadioButton(selected, 1, "Dark");
            secondSelected = g.RadioButton(selected, 0, "Light");
        });

        Assert.True(firstSelected);
        Assert.False(secondSelected);
    }

    [Fact]
    public void DisabledRadioDoesNotMoveTheSelection()
    {
        var gui = CreateGui();
        var selected = 0;

        Frame(gui, g =>
        {
            g.RadioButton(ref selected, 0, "Light");
            g.RadioButton(ref selected, 1, "Dark", enabled: false);
        });
        Frame(gui, g =>
        {
            g.RadioButton(ref selected, 0, "Light");
            g.RadioButton(ref selected, 1, "Dark", enabled: false);
        }, ClickAt(10, 40));

        Assert.Equal(0, selected);
    }

    [Fact]
    public void RadioGroupLaysOutEachOptionAsItsOwnRow()
    {
        var gui = CreateGui();
        var selected = 0;

        Frame(gui, g => g.RadioGroup(ref selected, [(0, "A"), (1, "B"), (2, "C")]));

        var group = Assert.Single(gui.RootNode!.Children);
        Assert.Equal(3, group.Children.Count);
    }
}
