namespace Example02;

/// <summary>
/// A domain editor with live previews: each document change recompiles with the reference interpreter on the UI
/// thread, then node previews and the declared output render off the UI thread and are drawn from published snapshots.
/// </summary>
public sealed class PreviewScene
{
    readonly IPreviewDomain _domain;
    readonly GraphInterpreter _interpreter;
    readonly string _outputTitle;
    GraphDocument? _document;
    bool _dirty = true;
    bool _render;
    float _lastElapsed = float.NaN;

    /// <summary>Creates the domain's editable document, renderer and output panel.</summary>
    public PreviewScene(IPreviewDomain domain, string name, string canvasId, string outputTitle)
    {
        _domain = domain;
        _outputTitle = outputTitle;
        _interpreter = new GraphInterpreter(domain.Semantics, null, null);
        Renderer = new PreviewRenderer(domain);
        Sandbox = new SandboxScene(name, canvasId, domain)
        {
            Previews = id => Frame.Previews.GetValueOrDefault(id)
        };
        Playing = domain.Animated;
        AttachDocument();
        Sandbox.DocumentLoaded += AttachDocument;
    }

    /// <summary>The shared editor hosting this domain.</summary>
    public SandboxScene Sandbox { get; }
    /// <summary>The non-overlapping background renderer.</summary>
    public PreviewRenderer Renderer { get; }
    /// <summary>The snapshot frozen for the current frame.</summary>
    public PreviewSnapshot Frame { get; private set; } = PreviewSnapshot.Empty;
    /// <summary>The domain's generated artifact for the last compiled document.</summary>
    public string Generated { get; private set; } = "";
    /// <summary>The declared outputs of the last successful compilation.</summary>
    public IReadOnlyList<ElementId> Outputs { get; private set; } = [];
    /// <summary>Whether an animated domain's timeline advances.</summary>
    public bool Playing { get; set; }
    /// <summary>The timeline position rendered next.</summary>
    public double Seconds { get; private set; }

    /// <summary>Moves the timeline to a position and renders it.</summary>
    public void Seek(double seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        Seconds = seconds;
        _render = true;
    }

    /// <summary>Compiles after document changes, schedules background renders and draws the editor and output.</summary>
    public void Draw(Gui gui, Action<string, string, Action> button)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            Pump(gui);
        }
        using var row = gui.Node().Expand().Direction(Axis.Horizontal).Gap(8).Enter();
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(6).Enter())
        {
            Sandbox.Draw(gui, button);
        }
        DrawOutput(gui, button);
    }

    /// <summary>Recompiles when needed and starts a render; called once per build pass.</summary>
    public void Pump(Gui gui)
    {
        if (_dirty)
        {
            _dirty = false;
            Compile();
        }
        var elapsed = gui.Time.Elapsed;
        if (_domain.Animated && Playing)
        {
            Seconds += float.IsNaN(_lastElapsed) ? 0 : Math.Max(0, elapsed - _lastElapsed);
            _render = true;
            gui.RequestFrame();
        }
        _lastElapsed = elapsed;
        if (_render && Renderer.TryRender(Seconds, gui.RequestFrame))
        {
            _render = false;
        }
        Frame = Renderer.Snapshot;
    }

    void DrawOutput(Gui gui, Action<string, string, Action> button)
    {
        using var panel = gui.StyledNode("graph-output-panel", id: "graph-output-panel").Width(300).ExpandHeight()
            .Direction(Axis.Vertical).Gap(6).Enter();
        gui.DrawText(_outputTitle);
        using (gui.Node().ExpandWidth().Height(200).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && Outputs.Count > 0
                && Frame.Previews.GetValueOrDefault(Outputs[0]) is { } output)
            {
                output.Draw(gui, gui.CurrentNode.Rect);
            }
        }
        if (_domain.Animated)
        {
            using (gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Horizontal).Gap(8).Enter())
            {
                button(Playing ? "Pause" : "Run", "preview-play", () => Playing = !Playing);
                button("Restart", "preview-restart", () => Seek(0));
            }
            gui.DrawText($"t = {Frame.Seconds.ToString("0.00", CultureInfo.InvariantCulture)} s");
        }
        if (Renderer.Error is { } error)
        {
            using var message = gui.StyledNode("graph-message", modifiers: ["error"]).Enter();
            gui.DrawText(error.Message);
        }
        using var code = gui.StyledNode("graph-generated").ExpandWidth().Direction(Axis.Vertical).Enter();
        foreach (var line in Generated.Split('\n'))
        {
            gui.DrawText(line.Length == 0 ? " " : line);
        }
    }

    void DocumentChanged(GraphDocument document, IReadOnlyList<GraphChange> changes) =>
        _dirty |= changes.Any(static change => change.Kind is not (GraphChangeKind.LayoutChanged
            or GraphChangeKind.GroupAdded or GraphChangeKind.GroupRemoved or GraphChangeKind.GroupChanged));

    void AttachDocument()
    {
        if (_document is not null)
        {
            _document.Changed -= DocumentChanged;
        }
        _document = Sandbox.Graph.Editor.Document;
        _document.Changed += DocumentChanged;
        _dirty = true;
    }

    // Compiling reads the live document, so it stays on the UI thread; the artifact is independent of later edits.
    void Compile()
    {
        var document = Sandbox.Graph.Editor.Document;
        var result = _interpreter.Compile(document);
        Sandbox.View.Validation = result.Diagnostics;
        Generated = _domain.Generate(document);
        if (result.Graph is { } graph)
        {
            Outputs = graph.Outputs;
            Renderer.Start(graph, PreviewPorts(document));
            _render = true;
        }
        else
        {
            Outputs = [];
            Renderer.Clear();
        }
    }

    Dictionary<ElementId, ElementId> PreviewPorts(GraphDocument document)
    {
        var ports = new Dictionary<ElementId, ElementId>();
        foreach (var node in document.Nodes.Where(_domain.HasPreview))
        {
            if (node.Ports.FirstOrDefault(static port => port.Direction == PortDirection.Output) is { } output)
            {
                ports[output.Id] = node.Id;
            }
        }
        return ports;
    }
}
