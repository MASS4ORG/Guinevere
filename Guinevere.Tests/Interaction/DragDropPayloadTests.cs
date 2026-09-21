using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

public class DragDropPayloadTests : IDisposable
{
    readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(200, 100));
    readonly ScriptedInputHandler _input = new();
    readonly TestableGui _gui;

    public DragDropPayloadTests()
    {
        _gui = new TestableGui { Input = _input };
        _gui.SetScreenRect(200, 100);
    }

    public void Dispose() => _surface.Dispose();

    void Frame(Action draw)
    {
        _gui.SetStage(Pass.Pass1Build);
        _gui.BeginFrame(_surface.Canvas);
        draw();
        _gui.CalculateLayout();
        _gui.SetStage(Pass.Pass2Render);
        draw();
        _gui.EndFrame();
        _input.NewFrame();
    }

    [Fact]
    public void PayloadMustMatchBothTypeAndTag()
    {
        var files = new DragDropTag("files");
        var tabs = new DragDropTag("tabs");
        _gui.BeginDrag("source", "readme.md", files);
        Frame(() => { });

        Assert.True(_gui.TryPeekPayload<string>(out var payload, files));
        Assert.Equal("readme.md", payload);
        Assert.True(_gui.CanAccept<string>(files, value => value.EndsWith(".md")));
        Assert.False(_gui.CanAccept<string>(tabs));
        Assert.False(_gui.CanAccept<int>(files));
    }

    [Fact]
    public void TargetPreviewsWithoutConsumingAndReportsTheSettledDrop()
    {
        var tag = new DragDropTag("asset");
        DropTargetResult<string> result = default;
        string? received = null;
        _input.MoveTo(25, 25);
        _input.PressButton();
        _gui.BeginDrag("source", "texture.png", tag);
        Frame(() => { });

        void Draw()
        {
            using (_gui.Node(50, 50, "target").Enter())
                result = _gui.DropTarget<string>("target", tag, onDrop: value => received = value);
        }

        Frame(Draw);
        Assert.Equal(DropTargetState.HoverAccepted, result.State);
        Assert.Equal("texture.png", result.Payload);
        Assert.True(_gui.TryPeekPayload<string>(out _, tag));

        _input.ReleaseButton();
        Frame(Draw);
        Assert.Equal("texture.png", received);

        Frame(Draw);
        Assert.Equal(DropTargetState.Dropped, result.State);
        Assert.Equal("texture.png", result.Payload);
    }

    [Fact]
    public void RejectedTargetReturnsAnExplicitPreviewState()
    {
        var result = default(DropTargetResult<int>);
        _input.MoveTo(25, 25);
        _input.PressButton();
        _gui.BeginDrag("source", 3);
        Frame(() => { });

        Frame(() =>
        {
            using (_gui.Node(50, 50, "target").Enter())
                result = _gui.DropTarget<int>("target", canAccept: value => value > 10);
        });

        Assert.Equal(DropTargetState.HoverRejected, result.State);
        Assert.Equal(3, result.Payload);
    }

    [Fact]
    public void OnlyTheLastEligibleOverlappingTargetReceivesThePayload()
    {
        var drops = new List<string>();
        _input.MoveTo(25, 25);
        _input.PressButton();
        _gui.BeginDrag("source", 7);
        Frame(() => { });
        _input.ReleaseButton();

        Frame(() =>
        {
            _gui.DropTarget<int>(new Rect(0, 0, 50, 50), "back", onDrop: _ => drops.Add("back"));
            _gui.DropTarget<int>(new Rect(0, 0, 50, 50), "front", onDrop: _ => drops.Add("front"));
        });

        Assert.Equal(["front"], drops);
    }

    [Fact]
    public void KeyboardDragDropsOnTheFocusedTarget()
    {
        string? received = null;
        _gui.BeginKeyboardDrag("source", "payload");
        Frame(() => { });
        Frame(() =>
        {
            using (_gui.Node(50, 50, "target").Enter())
            {
                _gui.DropTarget<string>("target", onDrop: value => received = value);
                if (_gui.Pass == Pass.Pass2Render) _gui.RequestFocus();
            }
        });
        Frame(() =>
        {
            using (_gui.Node(50, 50, "target").Enter())
                _gui.DropTarget<string>("target", onDrop: value => received = value);
        });
        Assert.True(_gui.HasFocus("target"));

        _input.PressKey(KeyboardKey.Enter);
        Frame(() =>
        {
            using (_gui.Node(50, 50, "target").Enter())
                _gui.DropTarget<string>("target", onDrop: value => received = value);
        });

        Assert.Equal("payload", received);
        Assert.False(_gui.IsDragging);
    }
}
