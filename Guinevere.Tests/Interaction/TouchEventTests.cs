using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

public class TouchEventTests
{
    [Fact]
    public void ContactsCaptureIndependentlyAndReleaseOnEndOrSourceRemoval()
    {
        using var harness = new FrameHarness();
        harness.Gui.Platform.Register<ITouchCapability>(harness.Input);
        harness.Gui.GestureRecognizer = new TouchGestureRecognizer();
        var events = new List<(long Id, string Target, TouchPhase Phase)>();
        var phases = new List<EventPhase>();
        void Draw(Gui gui)
        {
            gui.On<TouchEvent>(e => phases.Add(e.Phase), capture: true);
            foreach (var id in new[] { "top", "bottom" })
            {
                using (gui.Node(100, 50, id).Enter())
                {
                    gui.On<TouchEvent>(e =>
                    {
                        events.Add((e.Contact.Id, e.TargetId, e.Contact.Phase));
                        e.PreventDefault();
                        e.CapturePointer();
                    });
                }
            }
        }
        harness.Frame(Draw);
        harness.Input.Touch(new TouchContact(1, new Vector2(5, 5), 0.3f, TouchPhase.Began));
        harness.Input.Touch(new TouchContact(2, new Vector2(5, 55), null, TouchPhase.Began));
        harness.Frame(Draw);
        Assert.Equal("top", harness.Gui.TouchCapture(1));
        Assert.Equal("bottom", harness.Gui.TouchCapture(2));
        Assert.All(phases, phase => Assert.Equal(EventPhase.Capture, phase));
        harness.Input.Touch(new TouchContact(1, new Vector2(200, 200), 0.6f, TouchPhase.Moved));
        harness.Frame(Draw);
        Assert.Equal((1L, "top", TouchPhase.Moved), events[^1]);
        harness.Input.Touch(new TouchContact(1, new Vector2(200, 200), null, TouchPhase.Ended));
        harness.Frame(Draw);
        Assert.Null(harness.Gui.TouchCapture(1));
        harness.Gui.Platform.Remove<ITouchCapability>();
        harness.Frame(Draw);
        Assert.Null(harness.Gui.TouchCapture(2));
        Assert.Equal((2L, "bottom", TouchPhase.Canceled), events[^1]);
    }

    [Fact]
    public void HostAndRecognizedGesturesRouteToCaptureOwnerAndRemovedNodesReleaseCapture()
    {
        using var harness = new FrameHarness();
        harness.Gui.Platform.Register<ITouchCapability>(harness.Input);
        harness.Gui.GestureRecognizer = new TouchGestureRecognizer();
        var gestures = new List<TouchGesture>();
        void Draw(Gui gui)
        {
            using (gui.Node(100, 100, "surface").Enter())
            {
                gui.On<TouchEvent>(e => e.CapturePointer());
                gui.On<TouchGestureEvent>(e =>
                {
                    gestures.Add(e.Gesture);
                    e.PreventDefault();
                });
            }
        }
        harness.Frame(Draw);
        harness.Input.Touch(new TouchContact(1, new Vector2(5, 5), null, TouchPhase.Began));
        harness.Frame(Draw);
        harness.Input.Gesture(new TouchGesture(TouchGestureKind.Rotate, 1, new Vector2(300, 300), Rotation: 1));
        harness.Input.Touch(new TouchContact(1, new Vector2(6, 5), null, TouchPhase.Ended));
        harness.Frame(Draw);
        Assert.Equal([TouchGestureKind.Rotate, TouchGestureKind.Tap], gestures.Select(gesture => gesture.Kind));
        harness.Input.Touch(new TouchContact(2, new Vector2(5, 5), null, TouchPhase.Began));
        harness.Frame(Draw);
        harness.Frame(_ => { });
        Assert.Null(harness.Gui.TouchCapture(2));
    }
}
