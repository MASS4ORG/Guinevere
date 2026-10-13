using Example_74_PanGui_Heart;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks independent stages, contour transitions and the heart's animated deformation.</summary>
public class HeartProgramTests
{
    /// <summary>Every operation can be evaluated directly, without playing the earlier stages first.</summary>
    [Fact]
    public void Stages_AreIndependentAndKeepSourceGeometry()
    {
        var heart = Program.CreateStage(13, 0).Path.Bounds;
        for (var stage = 0; stage < 16; stage++)
            Assert.False(Program.CreateStage(stage, 1).Path.IsEmpty);

        Program.CreateStage(15, 2);
        Assert.Equal(heart, Program.CreateStage(13, 0).Path.Bounds);
        Assert.Equal(Program.CreateStage(2, 0).Path.Bounds, Program.CreateStage(2, 2).Path.Bounds);
    }

    /// <summary>A stage change starts at the previous contour and passes through intermediate geometry.</summary>
    [Fact]
    public void StageTransition_MorphsAndReducedMotionSkipsIt()
    {
        var previous = Program.CreateStage(0, 0).Path.Bounds;
        var target = Program.CreateStage(1, 0).Path.Bounds;
        var start = Program.CreateFrame(3.5f).Shape.Path.Bounds;
        var middle = Program.CreateFrame(3.825f).Shape.Path.Bounds;
        var finish = Program.CreateFrame(4.2f).Shape.Path.Bounds;

        Assert.Equal(previous, start);
        Assert.InRange(middle.MidX, target.MidX + 1, previous.MidX - 1);
        Assert.Equal(target, finish);
        Assert.Equal(target, Program.CreateFrame(3.825f, reduceMotion: true).Shape.Path.Bounds);
    }

    /// <summary>Rotating the small mixed-in circle locally changes the heart's outline over time.</summary>
    [Fact]
    public void FinalStage_DeformsOverTimeWithoutAccumulatingRotation()
    {
        var first = Program.CreateStage(15, 0).Path.Bounds;
        var turned = Program.CreateStage(15, MathF.PI / 8).Path.Bounds;
        Assert.NotEqual(first, turned);
        Assert.Equal(first, Program.CreateStage(15, 0).Path.Bounds);
    }

    /// <summary>The caption remains visible outside the shape rather than being clipped to the heart.</summary>
    [Fact]
    public void Draw_KeepsCaptionAndRequestsAnimationFrames()
    {
        using var harness = new FrameHarness(800, 700);
        harness.Frame(Program.Draw, 46);
        Assert.NotNull(harness.Gui.RootNode);
        Assert.Contains(harness.Gui.RootNode.ChildNodes.SelectMany(node => node.ChildNodes),
            node => node.Rect.Y >= 500);
        Assert.NotNull(harness.Gui.GetData<(Shape Shape, string Text)>((null!, ""), "heart-frame").Shape);
    }

    /// <summary>A shape and its shared main-layer path receive a scale exactly once.</summary>
    [Fact]
    public void Scale_TransformsMainPathOnce()
    {
        var original = Shape.Circle(100);
        var scaled = original.Scale(1.2f);
        Assert.Equal(240, scaled.Path.Bounds.Width, 3);
        Assert.Equal(200, original.Path.Bounds.Width, 3);
    }
}
