using System.Text;
using MASS4.Graph.Dataflow;

namespace Example02;

public sealed partial class CompositorDomain
{
    /// <inheritdoc />
    public EvaluationContext CreateContext() => new EvaluationContext().Set(new Playhead());

    /// <summary>Renders one frame at the playhead and copies every previewed frame and declared output.</summary>
    public async Task<IReadOnlyDictionary<ElementId, NodePreview>> RenderAsync(PreviewRun run,
        IReadOnlyDictionary<ElementId, ElementId> ports, double seconds)
    {
        run.Context.Get<Playhead>().Seconds = seconds;
        await run.Graph.EvaluateAsync(run.Context, run.Instances, run.Instance);
        var observations = new List<GraphObservation>();
        run.Graph.Observe(run.Context, run.Instances, run.Instance, observations);
        var outputs = run.Graph.Outputs.ToHashSet();
        var previews = new Dictionary<ElementId, NodePreview>();
        foreach (var observation in observations)
        {
            var key = ports.TryGetValue(observation.Element, out var node) ? node
                : outputs.Contains(observation.Element) ? observation.Element : (ElementId?)null;
            // Nodes reuse their frames, so the published preview copies the pixels.
            if (key is { } target && observation.Value is VideoFrame frame)
            {
                previews[target] = new NodePreview(frame.Width, frame.Height,
                    [.. frame.Luma.Select(static luma => NodePreview.Pack(luma, luma, luma))]);
            }
        }
        return previews;
    }

    /// <summary>Generates a readable render expression for each declared output.</summary>
    public string Generate(GraphDocument document)
    {
        var playhead = new Playhead();
        var text = new StringBuilder().Append(CultureInfo.InvariantCulture,
            $"# composition · {playhead.Width} × {playhead.Height} luma\n");
        foreach (var output in document.Nodes.Where(static n => n.TypeId == DataflowNodes.Output.Id))
        {
            if (output.Payload is VariableReference reference
                && document.TryGetVariable(reference.Variable, out var variable))
            {
                text.Append(CultureInfo.InvariantCulture,
                    $"{variable.Name} = {Expression(document, output, DataflowNodes.ResultKey, [], "")}\n");
            }
        }
        return text.ToString().TrimEnd();
    }

    // Blends break their operands onto indented lines so the expression fits a narrow panel.
    string Expression(GraphDocument document, GraphNode node, StableId key, HashSet<ElementId> visiting,
        string indent)
    {
        var edge = document.GetEdges(node.GetPort(key).Id).FirstOrDefault();
        if (edge is null)
        {
            return "none";
        }
        var source = document.GetNode(document.GetPort(edge.Source).NodeId);
        if (!visiting.Add(source.Id))
        {
            return "cycle";
        }
        var inner = indent + "    ";
        var text = source.TypeId == Over.Id
            ? $"over(\n{inner}top: {Expression(document, source, AKey, visiting, inner)},\n"
                + $"{inner}bottom: {Expression(document, source, BKey, visiting, inner)},\n"
                + $"{inner}mix: {Expression(document, source, MixKey, visiting, inner)})"
            : source.TypeId == Envelope.Id
                ? $"envelope({Expression(document, source, AKey, visiting, inner)}, {FormatPayload(source)})"
                : source.TypeId == Time.Id ? "time"
                : source.TypeId == Ramp.Id || source.TypeId == Pulse.Id
                    ? $"{Describe(source).ToLowerInvariant()}({FormatPayload(source)})" : Describe(source);
        visiting.Remove(source.Id);
        return text;
    }
}
