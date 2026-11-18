namespace Guinevere.Tests.Platform;

/// <summary>
/// Covers the shutdown contract every integration window follows: a draw that throws surfaces as that exception,
/// not as the failure of resetting a native window still inside its render loop, and disposal never throws.
/// </summary>
public class WindowLifecycleTests
{
    [Fact]
    public void AThrowingDrawSurfacesAsItselfNotAsTheResetFailureOfTheDisposal()
    {
        var thrown = new DrawFailure("the draw callback failed");
        var caught = Record.Exception(() =>
        {
            using var window = new FakeWindow(draw: () => throw thrown);
            window.RunGui();
        });

        Assert.Same(thrown, caught);
    }

    [Fact]
    public void TheOriginalExceptionKeepsItsTypeMessageAndStack()
    {
        var caught = Assert.Throws<DrawFailure>(static () =>
        {
            using var window = new FakeWindow(draw: static () => throw new DrawFailure("boom"));
            window.RunGui();
        });

        Assert.Equal("boom", caught.Message);
        Assert.Contains(nameof(TheOriginalExceptionKeepsItsTypeMessageAndStack), caught.StackTrace);
    }

    [Fact]
    public void ADisposalAfterAFaultedLoopIsCleanAndReportsWhatItSuppressed()
    {
        var window = new FakeWindow(draw: static () => throw new DrawFailure("boom"));
        Assert.Throws<DrawFailure>(window.RunGui);

        window.Dispose();

        Assert.True(window.Lifecycle.Faulted);
        Assert.True(window.ResetWasAttempted);
        var report = Assert.Single(window.Reports);
        Assert.Contains("window", report.Message);
        Assert.Contains("after the run loop faulted", report.Message);
        Assert.IsType<InvalidOperationException>(report.Exception);
    }

    [Fact]
    public void ADisposalIsIdempotentAndReleasesEachResourceOnce()
    {
        var window = new FakeWindow(draw: static () => { });
        window.RunGui();

        window.Dispose();
        window.Dispose();

        Assert.Equal(1, window.ReleaseCount);
        Assert.False(window.Lifecycle.Faulted);
        Assert.Empty(window.Reports);
    }

    [Fact]
    public void OneFailingStepDoesNotStopTheStepsAfterIt()
    {
        var lifecycle = new WindowLifecycle();
        var released = new List<string>();

        lifecycle.Teardown("first", () => throw new InvalidOperationException("no"), (_, _) => released.Add("reported"));
        lifecycle.Teardown("second", () => released.Add("second"));

        Assert.Equal(["reported", "second"], released);
    }

    [Fact]
    public void ADisposalWithoutALoadedWindowDoesNotThrow()
    {
        var window = new FakeWindow(draw: static () => { }, neverLoaded: true);

        var caught = Record.Exception(window.Dispose);

        Assert.Null(caught);
        Assert.Single(window.Reports);
    }

    [Fact]
    public void ALoopThatEndsNormallyIsNotMarkedFaulted()
    {
        var lifecycle = new WindowLifecycle();

        lifecycle.Run(static () => { });

        Assert.False(lifecycle.Faulted);
    }

    [Fact]
    public void ALoopNeedsABody()
    {
        Assert.Throws<ArgumentNullException>(static () => new WindowLifecycle().Run(null!));
        Assert.Throws<ArgumentNullException>(static () => new WindowLifecycle().Teardown("x", null!));
    }

    sealed class DrawFailure(string message) : Exception(message);

    /// <summary>
    /// A window shaped like the Silk one: running sets "in the render loop" and only a normal exit clears it, and
    /// resetting while inside the loop throws.
    /// </summary>
    sealed class FakeWindow(Action draw, bool neverLoaded = false) : IDisposable
    {
        bool _insideLoop;
        bool _loaded;

        public WindowLifecycle Lifecycle { get; } = new();

        public List<(string Message, Exception Exception)> Reports { get; } = [];

        public bool ResetWasAttempted { get; private set; }

        public int ReleaseCount { get; private set; }

        public void RunGui() => Lifecycle.Run(() =>
        {
            _loaded = !neverLoaded;
            _insideLoop = true;
            draw();
            _insideLoop = false;
        });

        public void Dispose()
        {
            if (!Lifecycle.BeginDispose())
            {
                return;
            }

            Lifecycle.Teardown("input", () =>
            {
                if (!_loaded)
                {
                    throw new NullReferenceException("input was never created");
                }
            }, Report);
            Lifecycle.Teardown("window", () =>
            {
                ResetWasAttempted = true;
                if (_insideLoop)
                {
                    throw new InvalidOperationException("You cannot call Reset inside of the render loop");
                }

                ReleaseCount++;
            }, Report);
        }

        void Report(string message, Exception exception) => Reports.Add((message, exception));
    }
}
