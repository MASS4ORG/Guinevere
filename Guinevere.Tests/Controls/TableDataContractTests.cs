namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the data contract between a table and its source: the table asks for a publish once per frame, builds
/// both passes from one snapshot, follows a replaced source, and asks the host for a frame when the source
/// changes.
/// </summary>
public class TableDataContractTests : TableTestBase
{
    [Fact]
    public void ASourceWhoseCountChangesBetweenThePassesStillDrawsTheCountOfTheFirstPass()
    {
        var source = new PublishingSource { Pending = 10 };

        // The source breaks the contract and shrinks after the first pass; the second pass still builds 10 rows.
        var gui = RunFrames(source, Columns(2), betweenPasses: () => source.ChangeNowBehindTheTablesBack(4));

        Assert.Equal(Enumerable.Range(0, 10), RowIndices(gui));
        Assert.Equal(4, source.RowCount);
    }

    [Fact]
    public void AChangeIsDrawnFromTheFrameAfterItWasPublishedNotInTheMiddleOfOne()
    {
        var source = new PublishingSource { Pending = 5 };
        var state = new TableState();
        var columns = Columns(2);
        var gui = RunFrames(source, columns, state, frames: 2);
        Assert.Equal(5, RowIndices(gui).Count);

        // Published at the start of this frame, so 8 rows; the 2 set between the passes waits for the next one.
        source.Pending = 8;
        gui = RunFrames(source, columns, state, reuse: gui, betweenPasses: () => source.Pending = 2);
        Assert.Equal(8, RowIndices(gui).Count);

        gui = RunFrames(source, columns, state, reuse: gui);
        Assert.Equal(2, RowIndices(gui).Count);
    }

    [Fact]
    public void ASourceReplacedBetweenFramesDrawsTheNewSourceAndLetsTheOldOneGo()
    {
        var first = new PublishingSource { Pending = 6 };
        var second = new PublishingSource { Pending = 3 };
        var state = new TableState();
        var columns = Columns(2);

        var gui = RunFrames(first, columns, state, frames: 2);
        Assert.Equal(6, RowIndices(gui).Count);
        Assert.Equal(1, first.Subscribers);

        gui = RunFrames(second, columns, state, reuse: gui, frames: 2);

        Assert.Equal(3, RowIndices(gui).Count);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
    }

    [Fact]
    public void TheTableAsksTheSourceToPublishOncePerFrameNotOncePerPass()
    {
        var source = new PublishingSource { Pending = 4 };

        RunFrames(source, Columns(2), frames: 3);

        Assert.Equal(3, source.BeginFrameCalls);
    }

    [Fact]
    public void ASourceSharedByTwoTablesPublishesOncePerFrame()
    {
        var source = new PublishingSource { Pending = 4 };
        var gui = RunFrames(new PublishingSource(), Columns(2));
        gui.SetStage(Pass.Pass1Build);

        new TableState().BeginFrame(gui, source);
        new TableState().BeginFrame(gui, source);

        Assert.Equal(2, source.BeginFrameCalls);
        Assert.Equal(1, source.Publications);
        Assert.Equal(4, source.RowCount);
    }

    [Fact]
    public void ASourceThatSaysItChangedAsksTheHostForAFrame()
    {
        var source = new PublishingSource { Pending = 4 };
        var gui = RunFrames(source, Columns(2), frames: 2);
        Assert.True(gui.FrameWaitSeconds > 0);

        source.RaiseInvalidated();

        Assert.Equal(0, gui.FrameWaitSeconds);
    }

    [Fact]
    public void AnInvalidationFromAnotherThreadAsksForAFrameToo()
    {
        var source = new PublishingSource { Pending = 4 };
        var gui = RunFrames(source, Columns(2), frames: 2);

        var worker = new Thread(source.RaiseInvalidated);
        worker.Start();
        worker.Join();

        Assert.Equal(0, gui.FrameWaitSeconds);
    }
}
