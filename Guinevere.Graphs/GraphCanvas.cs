using System.Runtime.CompilerServices;

namespace Guinevere.Graphs;

/// <summary>
/// A PSS-styled infinite canvas that builds controls solely for visible view descriptors.
/// Keep one instance per canvas and call Draw from the same site in both Guinevere passes.
/// </summary>
public sealed partial class GraphCanvas
{
    readonly List<GraphElementView> _visible = [];
    readonly HashSet<string> _visibleKeys = new(StringComparer.Ordinal);
    readonly List<GraphSemanticEvent> _announcements = [];
    GraphElementView[] _items = [];
    GraphCamera _frameCamera = new();
    Vector2 _viewportSize;
    Vector2 _frameSize;
    GraphBounds? _contentBounds;
    long _revealedRevision;
    Matrix3x2 _viewToScreen = Matrix3x2.Identity;
    Matrix3x2 _screenToView = Matrix3x2.Identity;

    /// <summary>Creates a canvas with an optional configured camera and its own interaction state.</summary>
    public GraphCanvas(GraphCamera? camera = null)
    {
        Camera = camera ?? new GraphCamera();
        Navigation.Changed += change => _announcements.Add(change);
    }

    /// <summary>The application's camera command surface.</summary>
    public GraphCamera Camera { get; }
    /// <summary>The logical keyboard and assistive-technology command surface.</summary>
    public GraphNavigation Navigation { get; } = new();
    /// <summary>Draws an adaptive minor and major grid.</summary>
    public bool ShowGrid { get; set; } = true;
    /// <summary>Distinguishes graph origin axes from other grid lines.</summary>
    public bool ShowAxes { get; set; } = true;
    /// <summary>Shows an interactive overview with a camera viewport indicator.</summary>
    public bool ShowMinimap { get; set; }
    /// <summary>The accessible name of the graph surface.</summary>
    public string Name { get; set; } = "Graph";
    /// <summary>The last laid-out viewport's visible world bounds.</summary>
    public GraphBounds VisibleBounds => Camera.VisibleBounds(_viewportSize);
    /// <summary>The number of descriptors whose controls are built in the current frame.</summary>
    public int VisibleCount => _visible.Count;
    /// <summary>Descriptors whose controls are built in the current frame, for visible-only hit testing.</summary>
    public IReadOnlyList<GraphElementView> VisibleElements => _visible;
    /// <summary>Visual layers drawn above elements in the render pass.</summary>
    public IList<IGraphOverlay> Overlays { get; } = new List<IGraphOverlay>();
    /// <summary>Below this zoom, elements render with <see cref="GraphLevelOfDetail.Compact"/> detail.</summary>
    public double CompactZoom { get; set; } = 0.6;
    /// <summary>Below this zoom, elements render with <see cref="GraphLevelOfDetail.Outline"/> detail.</summary>
    public double OutlineZoom { get; set; } = 0.3;
    /// <summary>The zoom of the current frame's camera snapshot.</summary>
    public double FrameZoom => _frameCamera.Zoom;
    /// <summary>The level of detail for the current frame's camera snapshot.</summary>
    public GraphLevelOfDetail Detail => FrameZoom < OutlineZoom ? GraphLevelOfDetail.Outline
        : FrameZoom < CompactZoom ? GraphLevelOfDetail.Compact : GraphLevelOfDetail.Full;

    /// <summary>Maps the current camera's world point through the last laid-out host transform.</summary>
    public Vector2 WorldToScreen(GraphPoint point) => Vector2.Transform(Camera.WorldToView(point), _viewToScreen);

    /// <summary>Maps a screen pointer through the last laid-out host transform into camera world coordinates.</summary>
    public GraphPoint ScreenToWorld(Vector2 point) =>
        Camera.ViewToWorld(Vector2.Transform(point, _screenToView));

    /// <summary>
    /// Builds visible elements under camera-transformed, stable-keyed scopes and publishes logical semantics.
    /// The optional builder draws one element in its unscaled layout rectangle; descriptors are frozen per frame.
    /// </summary>
    public LayoutNode Draw(Gui gui, IEnumerable<GraphElementView> items,
        Action<Gui, GraphElementView>? buildElement = null, float width = 0, float height = 0, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        GraphStyles.Ensure(gui);
        var canvas = gui.StyledNode("graph-canvas", id: id, filePath: filePath, lineNumber: lineNumber)
            .Expand().BlockInput();
        if (width > 0)
        {
            canvas.Width(width);
        }
        if (height > 0)
        {
            canvas.Height(height);
        }
        using var scope = canvas.Enter();
        gui.SetClipped(true);
        AttachInput(gui, canvas);
        if (gui.Pass == Pass.Pass1Build)
        {
            PrepareFrame(items);
        }
        else
        {
            gui.RegisterFocusable(claimsArrowKeys: true);
            UpdateScreenTransform(canvas);
            if (_viewportSize != canvas.Rect.Size)
            {
                _viewportSize = canvas.Rect.Size;
                gui.RequestFrame();
            }
            DrawGrid(gui, canvas);
            ReadSemanticInput(gui);
        }

        foreach (var item in _visible)
        {
            BuildVisibleElement(gui, canvas, item, buildElement);
        }
        DrawOverlays(gui);
        DrawMinimap(gui);
        if (gui.Pass == Pass.Pass2Render)
        {
            PublishSemantics(gui, canvas);
        }
        return canvas;
    }

    void UpdateScreenTransform(LayoutNode canvas)
    {
        var origin = canvas.LocalToScreen(canvas.Rect.Position);
        var x = canvas.LocalToScreen(canvas.Rect.Position + Vector2.UnitX) - origin;
        var y = canvas.LocalToScreen(canvas.Rect.Position + Vector2.UnitY) - origin;
        _viewToScreen = new Matrix3x2(x.X, x.Y, y.X, y.Y, origin.X, origin.Y);
        if (!Matrix3x2.Invert(_viewToScreen, out _screenToView))
        {
            _screenToView = Matrix3x2.Identity;
        }
    }

    /// <summary>Frames all current view content; an empty graph resets the camera.</summary>
    public void FitAll()
    {
        if (_contentBounds is { } bounds)
        {
            Camera.Fit(bounds, _viewportSize);
        }
        else
        {
            Camera.Reset();
        }
    }

    /// <summary>Frames the current view selection without changing model state.</summary>
    public void FitSelection()
    {
        var bounds = BoundsOf(_items.Where(item => Navigation.SelectedKeys.Contains(item.Key)));
        if (bounds is { } selection)
        {
            Camera.Fit(selection, _viewportSize);
        }
    }

    void PrepareFrame(IEnumerable<GraphElementView> items)
    {
        _items = [.. items];
        Navigation.Update(_items);
        _contentBounds = BoundsOf(_items);
        RevealFocus();
        _frameCamera = Camera.Snapshot();
        _frameSize = _viewportSize;
        _visible.Clear();
        _visibleKeys.Clear();
        if (_frameSize.X <= 0 || _frameSize.Y <= 0)
        {
            return;
        }
        var visible = _frameCamera.VisibleBounds(_frameSize);
        foreach (var item in _items)
        {
            if (item.Bounds.Intersects(visible))
            {
                _visible.Add(item);
                _visibleKeys.Add(item.Key);
            }
        }
    }

    void RevealFocus()
    {
        if (!CanRevealFocus())
        {
            return;
        }
        _revealedRevision = Navigation.FocusRevision;
        var focused = _items.FirstOrDefault(item => item.Key == Navigation.FocusedKey);
        if (focused is null)
        {
            return;
        }
        var visible = VisibleBounds;
        var bounds = focused.Bounds;
        if (!visible.Contains(bounds))
        {
            Camera.CenterOn(bounds.Center, _viewportSize);
        }
    }

    bool CanRevealFocus() => _revealedRevision != Navigation.FocusRevision
        && _viewportSize.X > 0 && _viewportSize.Y > 0;

    void BuildVisibleElement(Gui gui, LayoutNode canvas, GraphElementView item,
        Action<Gui, GraphElementView>? buildElement)
    {
        using var key = gui.ItemKey(item.Key);
        var position = _frameCamera.WorldToView(new GraphPoint(item.Bounds.X, item.Bounds.Y));
        var node = gui.Node((float)Math.Max(1, item.Bounds.Width), (float)Math.Max(1, item.Bounds.Height))
            .Absolute(position.X, position.Y);
        node.On<PointerDownEvent>(e =>
        {
            if (e.Button == MouseButton.Left)
            {
                Navigation.Focus(item.Key);
                if (e.TargetId == node.Id)
                {
                    gui.Focus.RequestFocus(canvas.Id, FocusReason.Mouse);
                }
            }
        });
        using var scope = node.Enter();
        gui.SetTransform(Matrix3x2.CreateScale((float)_frameCamera.Zoom, node.Rect.Position));
        buildElement?.Invoke(gui, item);
        if (gui.Pass == Pass.Pass2Render && Navigation.FocusedKey == item.Key && gui.HasFocus(canvas.Id)
            && item.Role != GraphElementRole.Edge)
        {
            var style = gui.ResolvePart("graph-focus");
            gui.DrawRect(node.Rect).Stroke(GraphStyles.Color(style, "border-color"),
                (float)(GraphStyles.Length(style, "border-width") / _frameCamera.Zoom));
        }
    }

    void DrawGrid(Gui gui, LayoutNode canvas)
    {
        if (!ShowGrid)
        {
            return;
        }
        var grid = gui.ResolvePart("graph-grid");
        var spacing = GraphStyles.Length(grid, "gap");
        var major = (int)GraphStyles.Length(grid, "graph-major-every");
        if (spacing <= 0 || major <= 0)
        {
            return;
        }
        foreach (var line in GraphGrid.Lines(_frameCamera, canvas.Rect.Size, ShowAxes, spacing, major))
        {
            var style = gui.ResolvePart("graph-grid", modifiers: line.Axis ? ["axis"] : line.Major ? ["major"] : []);
            gui.DrawLine(canvas.Rect.Position + line.Start, canvas.Rect.Position + line.End,
                GraphStyles.Color(style, "stroke"), GraphStyles.Length(style, "stroke-width"));
        }
    }

    static GraphBounds? BoundsOf(IEnumerable<GraphElementView> items)
    {
        GraphBounds? bounds = null;
        foreach (var item in items)
        {
            bounds = bounds?.Union(item.Bounds) ?? item.Bounds;
        }
        return bounds;
    }
}
