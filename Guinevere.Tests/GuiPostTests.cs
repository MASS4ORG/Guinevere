using System.Diagnostics;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks posted work: ordering, cross-thread wake-ups, drain timing, failures and allocations.</summary>
public class GuiPostTests
{
    /// <summary>Posted actions run in posting order at the next frame, never inline, even on the UI thread.</summary>
    [Fact]
    public void PostedActionsRunInOrderAtTheNextFrame()
    {
        using var harness = new FrameHarness();
        var order = new List<int>();
        for (var i = 1; i <= 3; i++)
        {
            var n = i;
            harness.Gui.Post(() => order.Add(n));
        }

        Assert.Empty(order);
        harness.Frame(static _ => { });
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    /// <summary>A post from another thread wakes the host and makes the pending frame due immediately.</summary>
    [Fact]
    public void PostFromAnotherThreadWakesTheHost()
    {
        using var harness = new FrameHarness();
        var wakes = 0;
        harness.Gui.WakeHost = () => Interlocked.Increment(ref wakes);
        harness.Frame(static _ => { });
        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);

        var worker = new Thread(() => harness.Gui.Post(static () => { }));
        worker.Start();
        worker.Join();
        Assert.Equal(1, wakes);
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);
    }

    /// <summary>Posts from the frame thread request a frame without waking the host.</summary>
    [Fact]
    public void PostFromTheFrameThreadDoesNotWakeTheHost()
    {
        using var harness = new FrameHarness();
        var wakes = 0;
        harness.Gui.WakeHost = () => Interlocked.Increment(ref wakes);
        harness.Frame(gui => gui.Post(static () => { }));
        Assert.Equal(0, wakes);
    }

    /// <summary>Posted actions run before the frame builds, so the same frame draws their changes.</summary>
    [Fact]
    public void PostedActionsRunBeforeTheFrameBuilds()
    {
        using var harness = new FrameHarness();
        var value = 0;
        var seen = -1;
        harness.Gui.Post(() => value = 5);
        harness.Frame(_ => seen = value);
        Assert.Equal(5, seen);
    }

    /// <summary>An exception does not stop later actions, and the same instance is rethrown after the drain.</summary>
    [Fact]
    public void ThrowingActionDoesNotStopTheOthers()
    {
        using var harness = new FrameHarness();
        var ran = new List<int>();
        var error = new InvalidOperationException("first");
        harness.Gui.Post(() => ran.Add(1));
        harness.Gui.Post(() => throw error);
        harness.Gui.Post(() => ran.Add(3));

        var thrown = Assert.Throws<InvalidOperationException>(() => harness.Frame(static _ => { }));
        Assert.Same(error, thrown);
        Assert.Equal(new[] { 1, 3 }, ran);
    }

    /// <summary>Two failing actions are rethrown together as an aggregate in posting order.</summary>
    [Fact]
    public void TwoThrowingActionsAreRethrownAsAggregate()
    {
        using var harness = new FrameHarness();
        var ran = new List<int>();
        var first = new InvalidOperationException("a");
        var second = new ArgumentException("b");
        harness.Gui.Post(() => throw first);
        harness.Gui.Post(() => ran.Add(2));
        harness.Gui.Post(() => throw second);

        var aggregate = Assert.Throws<AggregateException>(() => harness.Frame(static _ => { }));
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Same(first, aggregate.InnerExceptions[0]);
        Assert.Same(second, aggregate.InnerExceptions[1]);
        Assert.Equal(new[] { 2 }, ran);
    }

    /// <summary>Actions posted from a posted action run on the following frame, and that frame is requested.</summary>
    [Fact]
    public void ActionsPostedWhileDrainingRunOnTheNextFrame()
    {
        using var harness = new FrameHarness();
        var gui = harness.Gui;
        var ranLater = false;
        var waitDuringFrame = -1.0;
        gui.Post(() => gui.Post(() => ranLater = true));

        harness.Frame(g => waitDuringFrame = g.FrameWaitSeconds);
        Assert.False(ranLater);
        Assert.Equal(0, waitDuringFrame);
        Assert.Equal(0, gui.FrameWaitSeconds);

        harness.Frame(static _ => { });
        Assert.True(ranLater);
    }

    /// <summary>After a failed drain the next frame is normal and settles to idle, with no stale request.</summary>
    [Fact]
    public void TheFrameAfterAFailedDrainIsNormalAndSettlesToIdle()
    {
        using var harness = new FrameHarness();
        var gui = harness.Gui;
        var ran = 0;
        gui.Post(static () => throw new InvalidOperationException("failed"));
        Assert.Throws<InvalidOperationException>(() => harness.Frame(static _ => { }));
        Assert.Equal(0, gui.FrameWaitSeconds);

        gui.Post(() => ran++);
        var drawn = 0;
        harness.Frame(_ => drawn++);

        Assert.Equal(1, ran);
        Assert.Equal(2, drawn);
        Assert.Equal(double.PositiveInfinity, gui.FrameWaitSeconds);
        harness.Frame(static _ => { });
        Assert.Equal(1, ran);
        Assert.Equal(double.PositiveInfinity, gui.FrameWaitSeconds);
    }

    /// <summary>
    /// A host that sleeps while no frame is due is woken for every post from concurrent threads, and each thread's
    /// actions run in order.
    /// </summary>
    [Fact]
    public void ConcurrentPostsRunInPerThreadOrderAndNeverLeaveTheHostAsleep()
    {
        // Guards the wider gaps: a request lost after the re-check, ordering and counts across threads, and a realistic
        // host wait. The clear/check race itself is covered by ABeginFrameRequestsRacingAPostNeverLosesTheFrameRequest.
        const int Rounds = 300;
        using var harness = new FrameHarness(64, 64);
        // Like a window host's wait, the signal does not accumulate counts.
        using var wake = new AutoResetEvent(false);
        harness.Gui.WakeHost = () => wake.Set();
        for (var round = 0; round < Rounds; round++)
        {
            RunPostRound(harness, wake);
        }
    }

    static void RunPostRound(FrameHarness harness, AutoResetEvent wake)
    {
        const int Threads = 4;
        const int PerThread = 50;
        var gui = harness.Gui;
        var seen = Enumerable.Range(0, Threads).Select(static _ => new List<int>()).ToArray();
        var total = 0;
        var posted = 0;
        var running = Threads;
        using var go = new ManualResetEventSlim();
        var posters = Enumerable.Range(0, Threads).Select(thread => new Thread(() =>
        {
            go.Wait();
            for (var i = 0; i < PerThread; i++)
            {
                var n = i;
                gui.Post(() =>
                {
                    seen[thread].Add(n);
                    total++;
                });
                Interlocked.Increment(ref posted);
                if ((i + thread) % 7 == 0)
                {
                    Thread.Yield();
                }
                else if ((i + thread) % 4 == 0)
                {
                    Thread.SpinWait(20);
                }
            }

            Interlocked.Decrement(ref running);
            wake.Set();
        })
        { IsBackground = true }).ToArray();

        // A host asks again after every wake-up and sleeps only while no frame is due, so a lost request stalls it.
        void SleepUntilDue(bool untilPostersDone)
        {
            while (gui.FrameWaitSeconds > 0 && !(untilPostersDone && Volatile.Read(ref running) == 0))
            {
                Assert.True(wake.WaitOne(TimeSpan.FromSeconds(10)), $"wake-up lost with {total} actions run");
            }
        }

        try
        {
            foreach (var poster in posters)
            {
                poster.Start();
            }

            // A finished post that has not run yet is queued, so a frame must be due.
            void AssertFrameDueWhilePending()
            {
                var finished = Volatile.Read(ref posted);
                Assert.True(total >= finished || !(gui.FrameWaitSeconds > 0), "actions pending but no frame is due");
            }

            go.Set();
            while (Volatile.Read(ref running) > 0)
            {
                SleepUntilDue(true);
                harness.Frame(static _ => { });
                AssertFrameDueWhilePending();
            }

            foreach (var poster in posters)
            {
                poster.Join();
            }

            AssertFrameDueWhilePending();
            while (total < Threads * PerThread)
            {
                SleepUntilDue(false);
                harness.Frame(static _ => { });
                AssertFrameDueWhilePending();
            }

            Assert.Equal(Threads * PerThread, total);
            Assert.All(seen, list => Assert.Equal(Enumerable.Range(0, PerThread), list));
        }
        finally
        {
            // A failed assertion must not dispose the wake event or the harness under running posters.
            go.Set();
            foreach (var poster in posters)
            {
                if (poster.IsAlive)
                {
                    poster.Join();
                }
            }
        }
    }

    /// <summary>Whether two threads can really run in parallel, which the clear/check litmus test needs.</summary>
    public static bool HasSeveralProcessors => Environment.ProcessorCount >= 2;

    /// <summary>
    /// A post that lands between the frame start's clear of the request flag and its check of the queue must leave a
    /// frame due; each iteration sweeps the poster's offset against the frame start and checks after both finished.
    /// </summary>
    [Fact(Skip = "Needs at least two processors to race the threads.", SkipUnless = nameof(HasSeveralProcessors))]
    public void ABeginFrameRequestsRacingAPostNeverLosesTheFrameRequest()
    {
        const int MaxIterations = 1_000_000;
        const int Stop = -2;
        var budget = TimeSpan.FromSeconds(2);
        using var harness = new FrameHarness(16, 16);
        var gui = harness.Gui;
        var uiGo = -1;
        var posterGo = -1;
        var posterDone = -1;
        var poster = new Thread(() =>
        {
            var rng = new Random(12345);
            for (var i = 0; ; i++)
            {
                int seen;
                while ((seen = Volatile.Read(ref uiGo)) != i)
                {
                    if (seen == Stop)
                    {
                        return;
                    }
                }

                Volatile.Write(ref posterGo, i);
                Thread.SpinWait(rng.Next(0, 8));
                gui.Post(static () => { });
                Volatile.Write(ref posterDone, i);
            }
        })
        { IsBackground = true };

        var iterations = 0;
        var losses = 0;
        poster.Start();
        try
        {
            var clock = Stopwatch.StartNew();
            while (iterations < MaxIterations && clock.Elapsed < budget)
            {
                Volatile.Write(ref uiGo, iterations);
                while (Volatile.Read(ref posterGo) != iterations)
                {
                }

                gui.BeginFrameRequests();
                while (Volatile.Read(ref posterDone) != iterations)
                {
                }

                // The post is queued, so a frame must be due; a positive wait means the request was lost.
                if (gui.FrameWaitSeconds > 0)
                {
                    losses++;
                }

                gui.DrainPosted();
                iterations++;
            }
        }
        finally
        {
            Volatile.Write(ref uiGo, Stop);
            poster.Join();
        }

        Assert.True(losses == 0, $"{losses} lost frame requests in {iterations} iterations");
    }

    /// <summary>Draining an empty queue allocates nothing, so idle frames pay no garbage.</summary>
    [Fact]
    public void DrainingAnEmptyQueueAllocatesNothing()
    {
        using var harness = new FrameHarness();
        var gui = harness.Gui;
        for (var i = 0; i < 100; i++)
        {
            gui.DrainPosted();
        }

        var allocated = AllocationProbe.Steady(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                gui.DrainPosted();
            }
        });

        Assert.Equal(0L, allocated);
    }

    /// <summary>A null action is rejected at the call rather than at the frame.</summary>
    [Fact]
    public void NullActionThrows()
    {
        using var harness = new FrameHarness();
        Assert.Throws<ArgumentNullException>(() => harness.Gui.Post(null!));
    }
}
