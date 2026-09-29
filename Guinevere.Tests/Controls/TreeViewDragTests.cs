using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// A row is both a click and a drag source, and the two share one press. What the gesture is has to be
/// settled by the input script, not assumed from where the pointer ended up.
/// </summary>
public class TreeViewDragTests : IDisposable
{
    const int Width = 400;
    const int Height = 200;

    static readonly TreeViewTheme Theme = new() { RowHeight = 20f };

    static readonly IReadOnlyList<TreeItem> Items = [new("a", "Row A", 0)];

    readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Width, Height));
    readonly ScriptedInputHandler _input = new();
    readonly TestableGui _gui;
    readonly TreeViewState _state = new();

    object? _dropped;
    DropTargetState _typed = DropTargetState.None;
    List<TreeViewEvent> _clicks = [];

    public TreeViewDragTests()
    {
        _gui = new TestableGui { Input = _input };
        _gui.SetScreenRect(Width, Height);
    }

    public void Dispose() => _surface.Dispose();

    void Frame()
    {
        void Draw()
        {
            _gui.TreeView(_state, Items, Theme, e => _clicks.Add(e), dragPayload: _ => new RowPayload("Row A"));
            using (_gui.Node(100, 60, "inspector").AbsoluteScreen(280, 20).Enter())
                _typed = _gui.DropTarget<RowPayload>("inspector", onDrop: payload => _dropped = payload).State;
        }

        _gui.Time.Update(0.016);
        _gui.SetStage(Pass.Pass1Build);
        _gui.BeginFrame(_surface.Canvas);
        Draw();
        _gui.CalculateLayout();
        _gui.SetStage(Pass.Pass2Render);
        Draw();
        _gui.EndFrame();
        _input.NewFrame();
    }

    [Fact]
    public void ATypedDropTargetReceivesARow()
    {
        // A row registered as `DragSource<object>` carries the payload as `object`, and nothing is
        // assignable from `object` but `object` — so every typed target rejected it forever and an
        // inspector could only be fed by a tree whose payload type it happened to guess.
        _input.MoveTo(50, 10);
        Frame();

        _input.PressButton();
        Frame();
        _input.MoveTo(150, 30);
        Frame();
        Assert.True(_gui.IsDragging, "the row is a drag source, so holding it and moving starts a drag");

        _input.MoveTo(320, 40);
        Frame();
        Assert.Equal(DropTargetState.HoverAccepted, _typed);

        _input.ReleaseButton();
        Frame();
        Assert.Equal(new RowPayload("Row A"), _dropped);
    }

    /// <summary>
    /// The click lands when the button comes back up, and only if the pointer stayed put. A press that
    /// becomes a drag is not a click, so the row is not selected and nothing the selection points at is
    /// rebuilt while the user is on their way somewhere else with the payload.
    /// </summary>
    [Fact]
    public void OnlyAPressThatStaysPutIsAClick()
    {
        _input.MoveTo(50, 10);
        Frame();

        _input.PressButton();
        Frame();
        Assert.Empty(_clicks);
        Assert.Null(_state.SelectedId);
        Assert.False(_gui.IsDragging, "a press alone is not a drag either");

        Frame();
        Assert.Empty(_clicks);
        Assert.False(_gui.IsDragging, "holding still is still not a drag");

        _input.ReleaseButton();
        Frame();
        Assert.Single(_clicks);
        Assert.Equal("a", _state.SelectedId);
    }

    /// <summary>
    /// The editor case: a press that travels is the drag, and the row it started on is left alone.
    /// </summary>
    [Fact]
    public void APressThatBecomesADragNeverSelectsTheRow()
    {
        _input.MoveTo(50, 10);
        Frame();

        _input.PressButton();
        Frame();
        _input.MoveTo(150, 30);
        Frame();
        Assert.True(_gui.IsDragging);

        _input.MoveTo(320, 40);
        Frame();
        _input.ReleaseButton();
        Frame();

        Assert.Empty(_clicks);
        Assert.Null(_state.SelectedId);
        Assert.Equal(new RowPayload("Row A"), _dropped);
    }

    /// <summary>Jitter under the threshold is still a click, so a hand that shakes does not lose it.</summary>
    [Fact]
    public void AJitteryClickStillCounts()
    {
        _input.MoveTo(50, 10);
        Frame();

        _input.PressButton();
        Frame();
        _input.MoveTo(52, 11);
        Frame();
        _input.MoveTo(50, 10);
        Frame();
        _input.ReleaseButton();
        Frame();

        Assert.Single(_clicks);
        Assert.False(_gui.IsDragging);
    }

    readonly record struct RowPayload(string Label);
}
