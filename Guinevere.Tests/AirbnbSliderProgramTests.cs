using Example72PanGuiAirbnbSlider;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks continuous circular dragging and release settling in the Airbnb example.</summary>
public class AirbnbSliderProgramTests
{
    /// <summary>Dragging keeps a fractional angle and pointer ownership after the pointer leaves the handle.</summary>
    [Fact]
    public void DragTracksFractionalAngleOutsideHandleAndReleasesCapture()
    {
        using var harness = new FrameHarness(500, 500);
        var month = 1;
        void Draw(Gui gui) => Program.DrawAirBnBMonthSlider(gui, ref month);
        harness.Frame(Draw);
        var center = new Vector2(250, 250);
        harness.Input.MoveTo(center + Angle.Turns(1 / 12f - 0.25f).GetDirectionVector() * 120);
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        var capture = harness.Gui.PointerCapture;
        Assert.NotNull(capture);

        foreach (var turn in new[] { 0.27f, 0.29f, 0.71f })
        {
            harness.Input.MoveTo(center + Angle.Turns(turn - 0.25f).GetDirectionVector() * 220);
            harness.Frame(Draw);
            Assert.Equal(capture, harness.Gui.PointerCapture);
            Assert.Equal(turn, harness.Gui.GetFloat(0f, "month / 12.0f"), 4);
            Assert.Equal((int)Math.Round(turn * 12), month);
        }

        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(Draw);
        Assert.Null(harness.Gui.PointerCapture);
        var released = harness.Gui.GetFloat(0f, "month / 12.0f");
        Assert.InRange(released, 0.71f, month / 12f);
        Assert.NotEqual(month / 12f, released);
        harness.Frame(Draw, 0.1);
        Assert.Equal(month / 12f, harness.Gui.GetFloat(0f, "month / 12.0f"));
    }

    /// <summary>A pointer press outside the handle cannot start a drag.</summary>
    [Fact]
    public void PressAwayFromHandleDoesNotChangeMonthOrCapturePointer()
    {
        using var harness = new FrameHarness(500, 500);
        var month = 1;
        void Draw(Gui gui) => Program.DrawAirBnBMonthSlider(gui, ref month);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(250, 250));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(450, 250));
        harness.Frame(Draw);
        Assert.Equal(1, month);
        Assert.Null(harness.Gui.PointerCapture);
    }

    /// <summary>Reduced motion settles directly on an externally selected month.</summary>
    [Fact]
    public void ReducedMotionSettlesWithoutInterpolation()
    {
        using var harness = new FrameHarness(500, 500);
        harness.Gui.ReduceMotion = true;
        var month = 1;
        void Draw(Gui gui) => Program.DrawAirBnBMonthSlider(gui, ref month);
        harness.Frame(Draw);
        month = 9;
        harness.Frame(Draw);
        Assert.Equal(0.75f, harness.Gui.GetFloat(0f, "month / 12.0f"));
    }
}
