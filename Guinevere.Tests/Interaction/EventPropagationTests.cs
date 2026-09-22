using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

/// <summary>
/// DOM-style dispatch: events built from a frame's input run at the start of that frame against the tree the
/// previous frame laid out, travelling capture → target → bubble along the target's ancestors.
/// </summary>
public class EventPropagationTests
{
    const int Size = 200;

    sealed class Harness
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Size, Size));

        public Harness()
        {
            Gui = new TestableGui { Input = Input };
            Gui.SetScreenRect(Size, Size);
        }

        public ScriptedInputHandler Input { get; } = new();
        public TestableGui Gui { get; }

        public void Frame(Action<Gui> draw)
        {
            Gui.SetStage(Pass.Pass1Build);
            Gui.BeginFrame(_surface.Canvas);
            draw(Gui);
            Gui.CalculateLayout();
            Gui.SetStage(Pass.Pass2Render);
            draw(Gui);
            Gui.EndFrame();
            Input.NewFrame();
        }

        /// <summary>Builds a frame, then presses and releases at <paramref name="at"/> over two more.</summary>
        public void Click(Action<Gui> draw, Vector2 at)
        {
            Frame(draw);
            Input.MoveTo(at);
            Input.PressButton();
            Frame(draw);
            Input.ReleaseButton();
            Frame(draw);
        }
    }

    static readonly Vector2 OnButton = new(50, 50);

    [Fact]
    public void ClickTravelsCaptureThenTargetThenBubble()
    {
        var log = new List<string>();
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(Size, Size, "panel").Enter())
            {
                gui.On<ClickEvent>(e => log.Add($"panel-capture:{e.Phase}"), capture: true);
                gui.On<ClickEvent>(e => log.Add($"panel:{e.Phase}"));
                using (gui.Node(100, 100, "button").Enter())
                {
                    gui.On<ClickEvent>(e => log.Add($"button:{e.Phase}"));
                    gui.On<ClickEvent>(e => log.Add($"button-capture:{e.Phase}"), capture: true);
                }
            }
        }, OnButton);

        Assert.Equal(["panel-capture:Capture", "button-capture:Target", "button:Target", "panel:Bubble"], log);
    }

    [Fact]
    public void CompoundControlHandlesItsChildsClick()
    {
        ClickEvent? seen = null;
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(Size, Size, "list").Enter())
            {
                gui.On<ClickEvent>(e => seen = e);
                using (gui.Node(100, 100, "row").Enter()) { }
            }
        }, OnButton);

        Assert.NotNull(seen);
        Assert.Equal("row", seen.TargetId);
        Assert.Equal("list", seen.CurrentTargetId);
        Assert.Equal(EventPhase.Bubble, seen.Phase);
    }

    [Fact]
    public void StopPropagationFinishesTheNodeButSparesTheAncestors()
    {
        var log = new List<string>();
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(Size, Size, "panel").Enter())
            {
                gui.On<ClickEvent>(_ => log.Add("panel"));
                using (gui.Node(100, 100, "button").Enter())
                {
                    gui.On<ClickEvent>(e =>
                    {
                        log.Add("first");
                        e.StopPropagation();
                    });
                    gui.On<ClickEvent>(_ => log.Add("second"));
                }
            }
        }, OnButton);

        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public void StopImmediatePropagationSkipsTheRemainingListeners()
    {
        var log = new List<string>();
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(100, 100, "button").Enter())
            {
                gui.On<ClickEvent>(e =>
                {
                    log.Add("first");
                    e.StopImmediatePropagation();
                });
                gui.On<ClickEvent>(_ => log.Add("second"));
            }
        }, OnButton);

        Assert.Equal(["first"], log);
    }

    [Fact]
    public void BaseTypeListenersReceiveEverySubtype()
    {
        var types = new List<string>();
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(100, 100, "button").Enter())
                gui.On<PointerEvent>(e => types.Add(e.GetType().Name));
        }, OnButton);

        Assert.Equal(["PointerMoveEvent", "PointerDownEvent", "PointerUpEvent", "ClickEvent"], types);
    }

    [Fact]
    public void LaterSiblingWinsAtEqualZ_AndHigherZWinsOverall()
    {
        string? target = null;
        var raiseFirst = false;
        var harness = new Harness();

        void Draw(Gui gui)
        {
            using (gui.Node(Size, Size, "stage").Enter())
            {
                gui.On<ClickEvent>(e => target = e.TargetId);
                using (gui.Node(100, 100, "first").Absolute(0, 0).Enter())
                    if (raiseFirst) gui.SetZIndex(5);
                using (gui.Node(100, 100, "second").Absolute(0, 0).Enter()) { }
            }
        }

        harness.Click(Draw, OnButton);
        Assert.Equal("second", target);

        raiseFirst = true;
        harness.Click(Draw, OnButton);
        Assert.Equal("first", target);
    }

    [Fact]
    public void HitTestInvisibleNodesLetThePointerThrough()
    {
        string? target = null;
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(Size, Size, "stage").Enter())
            {
                gui.On<ClickEvent>(e => target = e.TargetId);
                using (gui.Node(100, 100, "button").Absolute(0, 0).Enter()) { }
                using (gui.Node(Size, Size, "decoration").Absolute(0, 0).HitTestVisible(false).Enter()) { }
            }
        }, OnButton);

        Assert.Equal("button", target);
    }

    [Fact]
    public void BlockingOverlayKeepsEventsInsideItsSubtree()
    {
        string? target = null;
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(Size, Size, "stage").Enter())
            {
                gui.On<ClickEvent>(e => target = e.TargetId);
                using (gui.Node(100, 100, "under").Absolute(0, 0).Enter())
                    gui.SetZIndex(10);
                using (gui.Node(150, 150, "overlay").Absolute(0, 0).BlockInput().Enter())
                using (gui.Node(20, 20, "overlay-button").Absolute(100, 100).Enter()) { }
            }
        }, OnButton);

        Assert.Equal("overlay", target);
    }

    [Fact]
    public void PressAndReleaseOnSiblingsClickTheirParent()
    {
        string? target = null;
        var harness = new Harness();

        void Draw(Gui gui)
        {
            using (gui.Node(Size, 100, "row").Direction(Axis.Horizontal).Enter())
            {
                gui.On<ClickEvent>(e => target = e.TargetId);
                using (gui.Node(100, 100, "a").Enter()) { }
                using (gui.Node(100, 100, "b").Enter()) { }
            }
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(50, 50);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(52, 50);
        harness.Frame(Draw);
        harness.Input.MoveTo(150, 50);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);

        // The pointer moved past the drag threshold, but nobody listens for drags; the click still lands.
        Assert.Equal("row", target);
    }

    [Fact]
    public void SecondClickCountsAsDouble()
    {
        var counts = new List<int>();
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(100, 100, "button").Enter())
                gui.On<ClickEvent>(e => counts.Add(e.ClickCount));
        }

        harness.Click(Draw, OnButton);
        harness.Click(Draw, OnButton);

        Assert.Equal([1, 2], counts);
    }

    [Fact]
    public void PreventedPressIsHiddenFromPollingControls()
    {
        var polledClicks = 0;
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(100, 100, "button").Enter())
            {
                gui.On<PointerDownEvent>(e => e.PreventDefault());
                if (gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick()) polledClicks++;
            }
        }, OnButton);

        Assert.Equal(0, polledClicks);
    }

    [Fact]
    public void UnhandledPressStillReachesPollingControls()
    {
        var polledClicks = 0;
        var harness = new Harness();

        harness.Click(gui =>
        {
            using (gui.Node(100, 100, "button").Enter())
            {
                gui.On<PointerDownEvent>(_ => { });
                if (gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick()) polledClicks++;
            }
        }, OnButton);

        Assert.Equal(1, polledClicks);
    }

    [Fact]
    public void PreventedScrollLeavesTheWheelAtZero()
    {
        var wheelSeen = float.NaN;
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(100, 100, "list").Enter())
            {
                gui.On<ScrollEvent>(e => e.PreventDefault());
                if (gui.Pass == Pass.Pass2Render) wheelSeen = gui.Input.MouseWheelDelta;
            }
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(OnButton);
        harness.Input.Scroll(3f);
        harness.Frame(Draw);

        Assert.Equal(0f, wheelSeen);
    }

    [Fact]
    public void DragGoesToThePressedNodeWhereverThePointerTravels()
    {
        var log = new List<string>();
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(Size, Size, "stage").Enter())
            {
                gui.On<ClickEvent>(_ => log.Add("click"));
                using (gui.Node(50, 50, "handle").Enter())
                {
                    gui.On<DragStartEvent>(e => log.Add($"start:{e.TotalDelta.X}"));
                    gui.On<DragMoveEvent>(e => log.Add($"move:{e.TotalDelta.X}"));
                    gui.On<DragEndEvent>(e => log.Add($"end:{e.TotalDelta.X}"));
                }
            }
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(10, 10);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(20, 10);
        harness.Frame(Draw);
        Assert.Equal("handle", harness.Gui.PointerCapture);

        harness.Input.MoveTo(150, 10);
        harness.Frame(Draw);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);

        Assert.Equal(["start:10", "move:140", "end:140"], log);
        Assert.Null(harness.Gui.PointerCapture);
    }

    [Fact]
    public void DragDoesNotStealThePointerFromAPollingOwner()
    {
        var started = false;
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(Size, 100, "row").Direction(Axis.Horizontal).Enter())
            {
                gui.On<DragStartEvent>(_ => started = true);
                using (gui.Node(100, 100, "slider").Enter())
                    if (gui.Pass == Pass.Pass2Render)
                        gui.GetInteractable().OnDrag(out _);
            }
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(50, 50);
        harness.Input.PressButton();
        harness.Frame(Draw);
        Assert.Equal("slider", harness.Gui.PointerCapture);

        // The row's listener would start a drag on the slider it targets; the slider already owns the gesture
        // under its own id, so both agree on one owner.
        harness.Input.MoveTo(80, 50);
        harness.Frame(Draw);
        Assert.Equal("slider", harness.Gui.PointerCapture);
        Assert.True(started);
    }

    [Fact]
    public void KeysAndTextGoToTheFocusedNodeAndBubble()
    {
        var log = new List<string>();
        var typed = "unset";
        var keyPolled = true;
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(Size, Size, "form").Enter())
            {
                gui.On<KeyDownEvent>(e =>
                {
                    log.Add($"{e.TargetId}:{e.Key}");
                    e.PreventDefault();
                });
                gui.On<TextInputEvent>(e => log.Add($"{e.TargetId}:{e.Text}"));
                using (gui.Node(100, 30, "field").Enter())
                {
                    gui.RegisterFocusable();
                    gui.On<TextInputEvent>(e => e.PreventDefault());
                    if (gui.Pass == Pass.Pass2Render)
                    {
                        typed = gui.Input.GetTypedCharacters();
                        keyPolled = gui.Input.IsKeyPressed(KeyboardKey.A);
                    }
                }
            }
        }

        harness.Frame(Draw);
        harness.Gui.RequestFocus("field");
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.A);
        harness.Input.TypeText("a");
        harness.Frame(Draw);

        Assert.Equal(["field:A", "field:a"], log);
        Assert.Equal(string.Empty, typed);
        Assert.False(keyPolled);
    }

    [Fact]
    public void UnfocusedKeysReachTheRoot()
    {
        var log = new List<string>();
        var harness = new Harness();
        void Draw(Gui gui)
        {
            // Nothing has focus, so key events target the root: app-wide shortcuts listen there.
            gui.On<KeyDownEvent>(e => log.Add($"down:{e.Key}"));
            gui.On<KeyUpEvent>(e => log.Add($"up:{e.Key}"));
            using (gui.Node(Size, Size, "app").Enter()) { }
        }

        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.Escape);
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Input.ReleaseKey(KeyboardKey.Escape);
        harness.Frame(Draw);

        Assert.Equal(["down:Escape", "up:Escape"], log);
    }

    [Fact]
    public void FocusMovesBubbleToTheirAncestors()
    {
        var log = new List<string>();
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(Size, Size, "form").Enter())
            {
                gui.On<FocusInEvent>(e => log.Add($"in:{e.TargetId}<-{e.RelatedTargetId ?? "none"}"));
                gui.On<FocusOutEvent>(e => log.Add($"out:{e.TargetId}->{e.RelatedTargetId ?? "none"}"));
                using (gui.Node(100, 30, "name").Enter()) gui.RegisterFocusable();
                using (gui.Node(100, 30, "email").Enter()) gui.RegisterFocusable();
            }
        }

        harness.Frame(Draw);
        harness.Gui.RequestFocus("name");
        harness.Frame(Draw);
        harness.Gui.RequestFocus("email");
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.Equal(["in:name<-none", "out:name->email", "in:email<-name"], log);
    }

    [Fact]
    public void NoListenersMeansNoFiltering()
    {
        var harness = new Harness();
        harness.Frame(_ => { });
        harness.Input.Scroll(2f);
        harness.Input.TypeText("x");
        var wheel = 0f;
        var text = string.Empty;

        harness.Frame(gui =>
        {
            if (gui.Pass != Pass.Pass2Render) return;
            wheel = gui.Input.MouseWheelDelta;
            text = gui.Input.GetTypedCharacters();
        });

        Assert.Equal(2f, wheel);
        Assert.Equal("x", text);
        Assert.Same(harness.Input, harness.Gui.PlatformInput);
    }
}
