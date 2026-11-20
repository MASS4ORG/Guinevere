using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the append-only streaming source: rows appear only when a frame publishes them, never move, keep a
/// stable identity, tolerate producers on other threads, and ask for one frame per burst rather than one per row.
/// </summary>
public class StreamingTableSourceTests
{
    [Fact]
    public void AppendedRowsAreInvisibleUntilAFramePublishesThem()
    {
        var source = new StreamingTableSource<int>();

        source.Append(1);
        source.Append(2);

        Assert.Equal(0, source.RowCount);
        Assert.Equal(2, source.AppendedCount);

        source.BeginFrame(1);

        Assert.Equal(2, source.RowCount);
        Assert.Equal(1, source[0]);
        Assert.Equal(2, source[1]);
    }

    [Fact]
    public void AFrameKeepsItsCountWhateverIsAppendedMeanwhile()
    {
        var source = new StreamingTableSource<int>();
        source.AppendRange([1, 2, 3]);
        source.BeginFrame(1);

        source.AppendRange(Enumerable.Range(0, 1000));
        source.BeginFrame(1);

        Assert.Equal(3, source.RowCount);

        source.BeginFrame(2);

        Assert.Equal(1003, source.RowCount);
    }

    [Fact]
    public void RowsKeepTheirOrderAndPlaceAcrossChunksAndDirectoryGrowth()
    {
        var source = new StreamingTableSource<int>();
        const int total = 70_000;

        for (var i = 0; i < total; i++)
        {
            source.Append(i * 3);
        }

        source.BeginFrame(1);

        Assert.Equal(total, source.RowCount);
        Assert.All(new[] { 0, 1, 4095, 4096, 4097, 8191, 8192, 65_535, 65_536, 69_999 },
            index => Assert.Equal(index * 3, source[index]));
    }

    [Fact]
    public void ARowNeverMovesWhileMoreRowsArrive()
    {
        var source = new StreamingTableSource<object>();
        var first = new object();
        source.Append(first);
        source.BeginFrame(1);
        Assert.Same(first, source[0]);

        for (var i = 0; i < 20_000; i++)
        {
            source.Append(new object());
        }

        source.BeginFrame(2);

        Assert.Same(first, source[0]);
    }

    [Fact]
    public void ARangeIsAddedTogetherSoAFrameSeesAllOfItOrNone()
    {
        var source = new StreamingTableSource<int>();
        source.BeginFrame(1);

        source.AppendRange([10, 11, 12]);
        Assert.Equal(0, source.RowCount);

        source.BeginFrame(2);
        Assert.Equal([10, 11, 12], Enumerable.Range(0, source.RowCount).Select(i => source[i]));
    }

    [Fact]
    public void IdentityIsTheSequenceNumberAndDoesNotChangeAsRowsArrive()
    {
        var source = new StreamingTableSource<string>();
        source.AppendRange(["a", "b", "c"]);
        source.BeginFrame(1);
        var before = source.GetRowIdentity(1);

        source.AppendRange(Enumerable.Range(0, 5000).Select(static i => "x" + i));
        source.BeginFrame(2);

        Assert.Equal(before, source.GetRowIdentity(1));
        Assert.Equal("b", source[1]);
        Assert.Equal("2", source.GetRowIdentity(2).Key);
        Assert.Equal("5002", source.GetRowIdentity(5002).Key);
    }

    [Fact]
    public void AnyNumberOfAppendsBetweenFramesRaisesInvalidatedOnce()
    {
        var source = new StreamingTableSource<int>();
        var raised = 0;
        source.Invalidated += (_, _) => raised++;

        for (var i = 0; i < 1000; i++)
        {
            source.Append(i);
        }

        source.AppendRange([1, 2, 3]);
        Assert.Equal(1, raised);

        source.BeginFrame(1);
        source.Append(1);
        source.Append(2);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void PublishingTwiceForTheSameFrameDoesNotRearmTheNotification()
    {
        var source = new StreamingTableSource<int>();
        var raised = 0;
        source.Invalidated += (_, _) => raised++;
        source.Append(1);
        source.BeginFrame(1);
        source.Append(2);
        Assert.Equal(2, raised);

        source.BeginFrame(1);
        source.Append(3);

        Assert.Equal(2, raised);
        Assert.Equal(1, source.RowCount);
    }

    [Fact]
    public void ReadingOutsideThePublishedSnapshotThrows()
    {
        var source = new StreamingTableSource<int>();
        source.Append(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => source[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetRowIdentity(0));

        source.BeginFrame(1);

        Assert.Equal(1, source[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => source[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => source[-1]);
    }

    [Fact]
    public void ProducersOnOtherThreadsLoseNoRowAndReadersSeeOnlyCompleteRows()
    {
        const int producers = 4;
        const int perProducer = 25_000;
        var source = new StreamingTableSource<Tagged>();
        var done = 0;
        var failures = new List<string>();
        var threads = Enumerable.Range(0, producers).Select(p => new Thread(() =>
        {
            for (var i = 0; i < perProducer; i++)
            {
                source.Append(new Tagged(p, i));
            }

            Interlocked.Increment(ref done);
        })).ToArray();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        var frame = 0L;
        var lastCount = 0;
        while (Volatile.Read(ref done) < producers || lastCount < producers * perProducer)
        {
            source.BeginFrame(++frame);
            var count = source.RowCount;
            if (count < lastCount)
            {
                failures.Add($"count went back from {lastCount} to {count}");
            }

            lastCount = count;
            for (var i = 0; i < count; i += 97)
            {
                if (source[i] is null)
                {
                    failures.Add($"row {i} of {count} was not there");
                    break;
                }
            }
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        Assert.Empty(failures);
        Assert.Equal(producers * perProducer, source.RowCount);
        var seen = Enumerable.Range(0, source.RowCount).Select(i => source[i]).ToHashSet();
        Assert.Equal(producers * perProducer, seen.Count);
        for (var p = 0; p < producers; p++)
        {
            var sequence = Enumerable.Range(0, source.RowCount).Select(i => source[i]).Where(row => row.Producer == p)
                .Select(static row => row.Sequence).ToArray();
            Assert.Equal(Enumerable.Range(0, perProducer), sequence);
        }
    }

    [Fact]
    public void ReadingAPublishedRowAllocatesNothing()
    {
        var source = new StreamingTableSource<int>();
        source.AppendRange(Enumerable.Range(0, 10_000));
        source.BeginFrame(1);

        var allocated = AllocationProbe.Steady(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                _ = source[i];
            }
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ItIsNotSortableAndLoadingIsAFlag()
    {
        var source = new StreamingTableSource<int>();

        Assert.False(typeof(ISortableTableViewSource<int>).IsAssignableFrom(typeof(StreamingTableSource<int>)));
        Assert.False(source.IsLoading);
        source.IsLoading = true;
        Assert.True(source.IsLoading);
    }

    [Fact]
    public void ARangeNeedsRows()
    {
        Assert.Throws<ArgumentNullException>(static () => new StreamingTableSource<int>().AppendRange(null!));
    }

    [Fact]
    public void ATableOverAStreamAsksForAFrameWhenRowsArriveAndShowsThemInTheNextOne()
    {
        var source = new StreamingTableSource<int>();
        source.AppendRange(Enumerable.Range(0, 10));
        var columns = new TableColumns<int>([TableColumn<int>.Of("Number", static row => row)]);
        var state = new TableState();
        using var harness = new FrameHarness(400, 300);

        harness.Frame(gui => gui.Table(state, source, columns));
        harness.Frame(gui => gui.Table(state, source, columns));
        Assert.Equal(10, source.RowCount);
        Assert.True(harness.Gui.FrameWaitSeconds > 0);

        source.Append(10);
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);
        Assert.Equal(10, source.RowCount);

        harness.Frame(gui => gui.Table(state, source, columns));
        Assert.Equal(11, source.RowCount);
    }

    sealed record Tagged(int Producer, int Sequence);
}
