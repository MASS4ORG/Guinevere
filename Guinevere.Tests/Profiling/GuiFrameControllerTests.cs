using Guinevere.Profiling;

namespace Guinevere.Tests.Profiling;

public sealed class GuiFrameControllerTests
{
    [Fact]
    public void PauseFreezesCallbacksClockAndPixelsAndStepExecutesOneCompleteFrame()
    {
        using var fixture = new ProfilerFixture();
        using var session = new ProfileSession();
        using var profiler = new GuiProfiler(fixture.Gui, session, "Target");
        using var controller = new GuiFrameController(fixture.Gui);
        var callbacks = 0;
        var customDraws = 0;
        var custom = Substitute.For<IDrawListEntry>();
        custom.When(entry => entry.Execute(Arg.Any<Gui>(), Arg.Any<LayoutNode>(), Arg.Any<SKCanvas>()))
            .Do(_ => customDraws++);
        void Draw()
        {
            callbacks++;
            fixture.Gui.DrawRect(new Rect(0, 0, 20, 20), Color.Blue);
            if (fixture.Gui.Pass == Pass.Pass2Render) { fixture.Gui.RootNode!.DrawList.Add(custom); }
        }
        fixture.Frame(Draw, .02);
        var elapsed = fixture.Gui.Time.Elapsed;
        var generation = profiler.Latest!.Generation;
        controller.Paused = true;
        fixture.Frame(Draw, 10);
        fixture.Frame(Draw, 10);
        Assert.Equal(2, callbacks);
        Assert.Equal(1, customDraws);
        Assert.Equal(elapsed, fixture.Gui.Time.Elapsed);
        Assert.Equal(generation, profiler.Latest.Generation);
        using (var pixels = SKBitmap.FromImage(fixture.Surface.Snapshot()))
        {
            Assert.Equal(SKColors.Blue, pixels.GetPixel(4, 4));
        }
        controller.Step(.125);
        fixture.Frame(Draw, 50);
        fixture.Frame(Draw, 50);
        Assert.Equal(4, callbacks);
        Assert.Equal(2, customDraws);
        Assert.Equal(elapsed + .125f, fixture.Gui.Time.Elapsed);
        Assert.True(controller.Paused);
    }

    [Fact]
    public void QueuedInputKeepsBoundariesSimultaneousKeysAndNativeOrder()
    {
        using var fixture = new ProfilerFixture();
        using var controller = new GuiFrameController(fixture.Gui) { Recording = true, Paused = true };
        fixture.Input.PressKey(KeyboardKey.LeftShift);
        fixture.Input.PressKey(KeyboardKey.A);
        fixture.Input.TypeText("A");
        controller.Input(new GuiInputEvent("key-down", Code: (int)KeyboardKey.LeftShift));
        controller.Input(new GuiInputEvent("key-down", Code: (int)KeyboardKey.A));
        controller.Input(new GuiInputEvent("text", Text: "A"));
        fixture.Frame(static () => { });
        fixture.Input.ReleaseKey(KeyboardKey.A);
        fixture.Input.ReleaseKey(KeyboardKey.LeftShift);
        fixture.Frame(static () => { });
        var delivered = new List<(bool Shift, bool A, string Text)>();
        void Draw()
        {
            if (fixture.Gui.Pass == Pass.Pass1Build)
            {
                delivered.Add((fixture.Gui.Input.IsKeyDown(KeyboardKey.LeftShift),
                    fixture.Gui.Input.IsKeyPressed(KeyboardKey.A), fixture.Gui.Input.GetTypedCharacters()));
                fixture.Gui.Input.SetClipboardText("host clipboard");
            }
        }
        controller.Step();
        fixture.Frame(Draw);
        controller.Step();
        fixture.Frame(Draw);
        Assert.Equal((true, true, "A"), delivered[0]);
        Assert.Equal((false, false, ""), delivered[1]);
        var frame = controller.GetRecording().Frames[0];
        Assert.Equal(new[] { "key-down", "key-down", "text" }, frame.Events.Select(input => input.Kind));
        Assert.Equal("host clipboard", fixture.Input.GetClipboardText());
        Assert.Same(fixture.Input, fixture.Gui.PlatformInput);
    }

    [Fact]
    public void BoundedQueuesReportDropsAndPlaybackRequiresExplicitLoopReset()
    {
        using var fixture = new ProfilerFixture();
        using var controller = new GuiFrameController(fixture.Gui) { MaximumFrames = 1, Paused = true, Recording = true };
        fixture.Frame(static () => { });
        fixture.Frame(static () => { });
        Assert.Equal(1, controller.DroppedInputFrames);
        controller.Step();
        fixture.Frame(static () => { });
        var recording = controller.GetRecording();
        Assert.Single(recording.Frames);
        controller.Loop = true;
        Assert.Throws<InvalidOperationException>(() => controller.Play(recording));
        var resets = 0;
        controller.ResetTarget = () => resets++;
        controller.Play(recording);
        controller.Paused = false;
        controller.TimeScale = .5;
        fixture.Frame(static () => { });
        fixture.Frame(static () => { });
        Assert.True(resets >= 2);
        controller.Stop();
        Assert.False(controller.Playing);
        controller.ClearRecording();
        Assert.Empty(controller.GetRecording().Frames);
        controller.Dispose();
        Assert.Null(fixture.Gui.FrameController);
    }

    [Fact]
    public void TargetExceptionsLeaveTheDebuggerLiveAndKeepTheLastImage()
    {
        using var fixture = new ProfilerFixture();
        using var controller = new GuiFrameController(fixture.Gui);
        fixture.Frame(() => fixture.Gui.DrawRect(new Rect(0, 0, 20, 20), Color.Blue));
        fixture.Frame(static () => throw new InvalidOperationException("broken target"));
        Assert.True(controller.Paused);
        Assert.Contains("broken target", controller.LastError!, StringComparison.Ordinal);
        using var pixels = SKBitmap.FromImage(fixture.Surface.Snapshot());
        Assert.Equal(SKColors.Blue, pixels.GetPixel(4, 4));
        controller.Step();
        fixture.Frame(static () => { });
        Assert.Null(controller.LastError);
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.Step(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.UpdateTime(-1));
    }

    [Fact]
    public void InvalidSettingsAndOversizedInputAreReportedWithoutBreakingHostRouting()
    {
        using var fixture = new ProfilerFixture();
        using var controller = new GuiFrameController(fixture.Gui) { MaximumBytes = 1024, MaximumSurfacePixels = 1 };
        controller.Input(new GuiInputEvent("text", Text: new string('x', 20000)));
        Assert.Equal(1, controller.DroppedInputEvents);
        fixture.Gui.Platform.Register<IGameInputCapability>(fixture.Input);
        fixture.Gui.Platform.Register<ITouchCapability>(fixture.Input);
        fixture.Frame(static () => { });
        Assert.Contains("pixel budget", controller.LastError);
        Assert.True(fixture.Gui.Platform.TryGet<IGameInputCapability>(out var restored));
        Assert.Same(fixture.Input, restored);
        controller.TimeScale = 0;
        Assert.Throws<InvalidOperationException>(() => fixture.Frame(static () => { }));
    }

    [Fact]
    public void RemovingLoopResetDuringPlaybackPausesAtTheBoundaryWithAnError()
    {
        using var fixture = new ProfilerFixture();
        using var controller = new GuiFrameController(fixture.Gui) { Loop = true, ResetTarget = static () => { } };
        controller.Play(new InputRecording([InputFrameRecorder.Capture(fixture.Input, .016, 480, 320)]));
        fixture.Frame(static () => { });
        controller.ResetTarget = null;
        fixture.Frame(static () => { });
        Assert.True(controller.Paused);
        Assert.Contains("reset", controller.LastError);
    }

    [Fact]
    public void LegacyScriptsKeepFrameExpansionDevicesChecksAndDumps()
    {
        using var fixture = new ProfilerFixture();
        using var controller = new GuiFrameController(fixture.Gui);
        var directory = Path.Combine(Path.GetTempPath(), "profiler-script-" + Guid.NewGuid());
        controller.ScriptOutputDirectory = directory;
        try
        {
            var script = new InputScript
            {
                Width = 480,
                Height = 320,
                Steps = [
                    new() { Action = InputAction.Move, X = 40, Y = 40 },
                    new() { Action = InputAction.Press }, new() { Action = InputAction.Release },
                    new() { Action = InputAction.Click }, new() { Action = InputAction.Scroll, Delta = 2 },
                    new() { Action = InputAction.Hold, Key = KeyboardKey.LeftShift },
                    new() { Action = InputAction.Key, Key = KeyboardKey.A },
                    new() { Action = InputAction.Unhold, Key = KeyboardKey.LeftShift },
                    new() { Action = InputAction.Type, Text = "text" },
                    new() { Action = InputAction.ControllerConnect },
                    new() { Action = InputAction.ControllerButton, GameButton = GameButton.South, Down = true },
                    new() { Action = InputAction.ControllerAxis, Axis = 0, AxisValue = .5f },
                    new() { Action = InputAction.ControllerDisconnect },
                    new() { Action = InputAction.Touch, ContactId = 7, Phase = TouchPhase.Began },
                    new() { Action = InputAction.Gesture, ContactId = 7, GestureKind = TouchGestureKind.Pinch },
                    new() { Action = InputAction.Reset }, new() { Action = InputAction.Frames, Count = 2 },
                    new() { Action = InputAction.ExpectNode, Value = "application-node" },
                    new() { Action = InputAction.ExpectNoNode, Value = "unavailable" },
                    new() { Action = InputAction.ExpectCapture, Value = "none" },
                    new() { Action = InputAction.Tree }, new() { Action = InputAction.Dump, Path = "frame.png" }
                ]
            };
            var compiled = InputScriptRecording.Compile(script);
            Assert.Equal(script.Steps.Count + 3, compiled.Recording.Frames.Count);
            controller.Play(script);
            for (var frame = 0; frame < compiled.Recording.Frames.Count; frame++)
            {
                fixture.Frame(() => fixture.Gui.Node(20, 20, "application-node"));
            }
            Assert.False(controller.Playing);
            Assert.Equal(0, controller.ScriptFailures);
            Assert.Null(compiled.Check(-1, fixture.Gui, fixture.Surface));
            Assert.True(File.Exists(Path.Combine(directory, "frame.png")));
            controller.Play(new InputScript { Steps = [new() { Action = InputAction.ExpectNode, Value = "missing" }] });
            fixture.Frame(() => fixture.Gui.Node());
            Assert.Equal(1, controller.ScriptFailures);
            Assert.Contains("missing", controller.PlaybackMessage!, StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => InputScriptRecording.Compile(script, maximumFrames: 1));
            Assert.Throws<InvalidDataException>(() => InputScriptRecording.Compile(
                new InputScript { Steps = [new() { Action = (InputAction)999 }] }));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }
}
