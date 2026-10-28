using Guinevere;
using Guinevere.Graphs;
using MASS4.Graph;
using MASS4.Graph.Interpreter;

namespace Example02;

/// <summary>
/// The arithmetic sandbox with live flow: each document change recompiles with the reference interpreter, and the
/// compiled artifact is evaluated and observed off the UI thread before the flow overlay draws the results.
/// </summary>
public sealed class FlowScene
{
    readonly GraphInterpreter _interpreter;
    EvaluationContext _context = new();
    ICompiledGraph? _graph;
    InstanceBuffer? _instances;
    InstanceHandle _instance;
    bool _dirty = true;
    bool _observe;

    /// <summary>Creates a separate document with the same arithmetic domain and a flow overlay.</summary>
    public FlowScene()
    {
        _interpreter = new GraphInterpreter(Sandbox.Graph.Semantics, null, null);
        Flow = new GraphFlowOverlay(Sandbox.View);
        Sandbox.Canvas.Overlays.Add(Flow);
        Sandbox.Graph.Editor.Document.Changed += (_, _) => _dirty = true;
    }

    /// <summary>The editable document whose evaluation is visualized.</summary>
    public SandboxScene Sandbox { get; } = new("Running flow", "flow-canvas");
    /// <summary>The overlay drawing observed values and moving pulses.</summary>
    public GraphFlowOverlay Flow { get; }

    /// <summary>Compiles after document changes and starts background observation, then draws the sandbox.</summary>
    public void Draw(Gui gui, Action<string, string, Action> button)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            Pump(gui);
        }
        Sandbox.Draw(gui, button);
        if (Flow.Error is { } error)
        {
            gui.DrawText(error.Message, 13, Sandbox.View.Theme.Error);
        }
    }

    void Pump(Gui gui)
    {
        if (_dirty)
        {
            _dirty = false;
            Compile();
        }
        if (_observe && _graph is not null && Flow.TryObserve(_graph, _context, _instances!, _instance,
                gui.RequestFrame))
        {
            _observe = false;
        }
    }

    // Compiling reads the live document, so it stays on the UI thread; the artifact is independent of later edits.
    void Compile()
    {
        var result = _interpreter.Compile(Sandbox.Graph.Editor.Document);
        Sandbox.View.Validation = result.Diagnostics;
        if (result.Graph is not { } graph)
        {
            _graph = null;
            Flow.Clear();
            return;
        }
        // A previous background evaluation may still hold the old context and instances.
        _graph = graph;
        _context = new EvaluationContext();
        _instances = new InstanceBuffer(graph.InstanceLayout);
        _instance = _instances.Allocate();
        graph.Initialize(_context, _instances, _instance);
        _observe = true;
    }
}
