using System.Globalization;
using MASS4.Graph;

namespace Guinevere.Graphs;

/// <summary>
/// Visualizes running dataflow from <see cref="ICompiledGraph.Observe"/> snapshots: links whose source output has an
/// observed value carry moving pulses, and observed outputs show their values. Evaluation and observation run off the
/// UI thread; drawing only reads the latest published snapshot, so the UI never waits for a graph.
/// </summary>
public sealed class GraphFlowOverlay(GraphDocumentView view) : IGraphOverlay
{
    IReadOnlyDictionary<ElementId, object> _values = new Dictionary<ElementId, object>();
    IReadOnlyDictionary<ElementId, object> _frame = new Dictionary<ElementId, object>();
    long _revision;
    int _running;

    /// <summary>The latest published observations keyed by observed element.</summary>
    public IReadOnlyDictionary<ElementId, object> Values => Volatile.Read(ref _values);
    /// <summary>The number of snapshots published so far.</summary>
    public long Revision => Interlocked.Read(ref _revision);
    /// <summary>Whether an evaluation started by <see cref="TryObserve"/> has not yet published.</summary>
    public bool Running => Volatile.Read(ref _running) != 0;
    /// <summary>The failure of the most recent evaluation, or null after a successful one.</summary>
    public Exception? Error { get; private set; }
    /// <summary>Animates pulses along active links; static markers are drawn when disabled.</summary>
    public bool Animate { get; set; } = true;
    /// <summary>Pulse speed in world units per second.</summary>
    public double Speed { get; set; } = 90;
    /// <summary>Distance between pulses in world units.</summary>
    public double Spacing { get; set; } = 48;
    /// <summary>Formats an observed value for its label; formattable values use the invariant culture by default.</summary>
    public Func<object, string> Format { get; set; } = static value =>
        value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? "";

    /// <summary>Replaces the snapshot; safe to call from any thread.</summary>
    public void Publish(IEnumerable<GraphObservation> observations)
    {
        var values = new Dictionary<ElementId, object>();
        foreach (var observation in observations)
        {
            if (observation.Value is { } value)
            {
                values[observation.Element] = value;
            }
        }
        Volatile.Write(ref _values, values);
        Interlocked.Increment(ref _revision);
    }

    /// <summary>Clears the snapshot so no flow is drawn.</summary>
    public void Clear() => Publish([]);

    /// <summary>
    /// Evaluates and observes one instance on the thread pool, then publishes the observations. Returns false without
    /// starting when a previous evaluation is still running; the caller retries after <paramref name="published"/>.
    /// </summary>
    /// <param name="graph">A compiled artifact, independent of later document edits.</param>
    /// <param name="context">The evaluation context, used only by the background evaluation.</param>
    /// <param name="instances">Instance state, used only by the background evaluation.</param>
    /// <param name="instance">The observed instance.</param>
    /// <param name="published">Called from the background thread after publishing, such as Gui.RequestFrame.</param>
    public bool TryObserve(ICompiledGraph graph, EvaluationContext context, InstanceBuffer instances,
        InstanceHandle instance, Action? published = null)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }
        Completion = Task.Run(async () =>
        {
            try
            {
                await graph.EvaluateAsync(context, instances, instance);
                var observations = new List<GraphObservation>();
                graph.Observe(context, instances, instance, observations);
                Error = null;
                Publish(observations);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Error = exception;
                Publish([]);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
                published?.Invoke();
            }
        });
        return true;
    }

    /// <summary>The most recently started evaluation, for hosts that shut down or test deterministically.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public void Draw(Gui gui, GraphOverlayContext context)
    {
        if (context.Pass == Pass.Pass1Build)
        {
            _frame = Values;
        }
        if (_frame.Count == 0)
        {
            return;
        }
        if (context.Pass == Pass.Pass2Render)
        {
            DrawActiveLinks(gui, context);
        }
        if (context.Detail != GraphLevelOfDetail.Outline)
        {
            DrawLabels(gui, context);
        }
    }

    void DrawActiveLinks(Gui gui, GraphOverlayContext context)
    {
        var time = Animate ? gui.Time.Elapsed : 0;
        foreach (var edge in view.Editor.Document.Edges)
        {
            if (_frame.ContainsKey(edge.Source) && view.TryGetConnector(edge.Id, out var connector)
                && connector.Bounds.Intersects(context.VisibleBounds))
            {
                DrawPulses(gui, context, connector, time, view.Theme.Flow);
            }
        }
        if (Animate)
        {
            gui.RequestFrame();
        }
    }

    void DrawLabels(Gui gui, GraphOverlayContext context)
    {
        foreach (var (element, value) in _frame)
        {
            if (view.TryGetPortPosition(element, out var point)
                && context.VisibleBounds.Intersects(new GraphBounds(point.X, point.Y, 0, 0)))
            {
                DrawLabel(gui, element, context.Offset(point), Format(value), view.Theme);
            }
        }
    }

    void DrawPulses(Gui gui, GraphOverlayContext context, GraphConnector connector, double time, Color color)
    {
        var count = Math.Max(1, (int)Math.Round(connector.Length / Spacing));
        var phase = time * Speed / Spacing % 1;
        var radius = (float)Math.Clamp(3.5 * context.Zoom, 1.5, 6);
        for (var i = 0; i < count; i++)
        {
            gui.DrawCircleFilled(context.ToLocal(connector.Point((i + phase) / count)), radius, color);
        }
    }

    static void DrawLabel(Gui gui, ElementId element, Vector2 anchor, string text, GraphTheme theme)
    {
        using var key = gui.ItemKey(element.ToString());
        var width = gui.MeasureTextWidth(text, 12) + 12;
        var label = gui.Node(width, 18).Absolute(anchor.X + 10, anchor.Y - 26).AlignContent(0.5f, 0.5f)
            .HitTestVisible(false);
        using var scope = label.Enter();
        if (gui.Pass == Pass.Pass2Render)
        {
            gui.DrawRectFilled(label.Rect, theme.Badge, 9);
            gui.DrawRect(label.Rect, 9).Stroke(theme.Flow, 1);
        }
        gui.DrawText(text, 12, theme.BadgeText).HitTestVisible(false);
    }
}
