using System.Text;
using MASS4.Graph.Dataflow;

namespace Example02;

public sealed partial class ShaderDomain
{
    /// <summary>Fragments per preview side.</summary>
    public const int PreviewSize = 12;

    /// <inheritdoc />
    public EvaluationContext CreateContext() => new EvaluationContext().Set(new Fragment());

    /// <summary>Shades every preview fragment and samples each previewed port and declared output.</summary>
    public async Task<IReadOnlyDictionary<ElementId, NodePreview>> RenderAsync(PreviewRun run,
        IReadOnlyDictionary<ElementId, ElementId> ports, double seconds)
    {
        var fragment = run.Context.Get<Fragment>();
        var outputs = run.Graph.Outputs.ToHashSet();
        var pixels = new Dictionary<ElementId, uint[]>();
        var observations = new List<GraphObservation>();
        for (var y = 0; y < PreviewSize; y++)
        {
            for (var x = 0; x < PreviewSize; x++)
            {
                fragment.Uv = new((x + 0.5f) / PreviewSize, (y + 0.5f) / PreviewSize);
                await run.Graph.EvaluateAsync(run.Context, run.Instances, run.Instance);
                observations.Clear();
                run.Graph.Observe(run.Context, run.Instances, run.Instance, observations);
                foreach (var observation in observations)
                {
                    var key = ports.TryGetValue(observation.Element, out var node) ? node
                        : outputs.Contains(observation.Element) ? observation.Element : (ElementId?)null;
                    if (key is { } target && Pixel(observation.Value) is { } pixel)
                    {
                        if (!pixels.TryGetValue(target, out var image))
                        {
                            pixels[target] = image = new uint[PreviewSize * PreviewSize];
                        }
                        image[y * PreviewSize + x] = pixel;
                    }
                }
            }
        }
        return pixels.ToDictionary(static pair => pair.Key,
            static pair => new NodePreview(PreviewSize, PreviewSize, pair.Value));
    }

    static uint? Pixel(object? value) => value switch
    {
        Rgba color => NodePreview.Pack(color.R, color.G, color.B),
        float scalar => NodePreview.Pack(scalar, scalar, scalar),
        Uv uv => NodePreview.Pack(uv.U, uv.V, 0),
        _ => null
    };

    /// <summary>Generates a GLSL fragment shader equivalent to the material's declared outputs.</summary>
    public string Generate(GraphDocument document) => new GlslWriter(document).Write();

    sealed class GlslWriter(GraphDocument document)
    {
        readonly StringBuilder _body = new();
        readonly Dictionary<ElementId, string> _names = [];
        readonly HashSet<ElementId> _visiting = [];

        public string Write()
        {
            var text = new StringBuilder("#version 330 core\nin vec2 uv;\n");
            foreach (var variable in document.Variables.OrderBy(static v => v.Name, StringComparer.Ordinal))
            {
                var qualifier = variable.Scope == VariableScope.Output ? "out" : "uniform";
                text.Append(CultureInfo.InvariantCulture, $"{qualifier} {Type(variable.TypeId)} {Name(variable.Name)};\n");
            }
            text.Append("\nvec4 checker(vec2 p, vec4 a, vec4 b, float n) {\n")
                .Append("    vec2 c = floor(p * n);\n    return mod(c.x + c.y, 2.0) < 1.0 ? a : b;\n}\n\nvoid main() {\n");
            foreach (var output in document.Nodes.Where(static n => n.TypeId == DataflowNodes.Output.Id))
            {
                if (output.Payload is VariableReference reference
                    && document.TryGetVariable(reference.Variable, out var variable))
                {
                    var value = Input(output, DataflowNodes.ResultKey);
                    _body.Append(CultureInfo.InvariantCulture, $"    {Name(variable.Name)} = {value};\n");
                }
            }
            return text.Append(_body).Append('}').ToString();
        }

        string Emit(GraphNode node)
        {
            if (_names.TryGetValue(node.Id, out var existing))
            {
                return existing;
            }
            if (node.TypeId == DataflowNodes.GetVariable.Id)
            {
                return node.Payload is VariableReference reference
                    && document.TryGetVariable(reference.Variable, out var variable) ? Name(variable.Name) : "vec4(0.0)";
            }
            if (!_visiting.Add(node.Id))
            {
                return "vec4(0.0)";
            }
            var expression = Expression(node);
            _visiting.Remove(node.Id);
            var name = "n" + _names.Count.ToString(CultureInfo.InvariantCulture);
            _names[node.Id] = name;
            var output = node.Ports.FirstOrDefault(static p => p.Direction == PortDirection.Output);
            _body.Append(CultureInfo.InvariantCulture,
                $"    {(output is null ? "vec4" : Type(output.TypeId))} {name} = {expression};\n");
            return name;
        }

        string Expression(GraphNode node) => node.Payload switch
        {
            ScalarPayload scalar when node.TypeId == ScalarConstant.Id => Literal(scalar.Value),
            ColorPayload color => $"vec4({Literal(color.Value.R)}, {Literal(color.Value.G)}, "
                + $"{Literal(color.Value.B)}, {Literal(color.Value.A)})",
            ScalarPayload tiles when node.TypeId == Checker.Id =>
                $"checker({Input(node, UvKey)}, {Input(node, AKey)}, {Input(node, BKey)}, {Literal(tiles.Value)})",
            _ when node.TypeId == TexCoord.Id => "uv",
            _ when node.TypeId == Multiply.Id || node.TypeId == ScaleColor.Id =>
                $"{Input(node, AKey)} * {Input(node, BKey)}",
            _ when node.TypeId == Lerp.Id => $"mix({Input(node, AKey)}, {Input(node, BKey)}, {Input(node, TKey)})",
            _ when node.TypeId == ScalarToColor.Id => $"vec4({Input(node, AKey)})",
            _ => "vec4(0.0)"
        };

        string Input(GraphNode node, StableId key)
        {
            var port = node.GetPort(key);
            var edge = document.GetEdges(port.Id).FirstOrDefault();
            return edge is null ? Default(port.TypeId) : Emit(document.GetNode(document.GetPort(edge.Source).NodeId));
        }

        static string Default(StableId type) =>
            type == Scalar.Id ? "0.0" : type == UvType.Id ? "vec2(0.0)" : "vec4(0.0)";

        static string Type(StableId type) => type == Scalar.Id ? "float" : type == UvType.Id ? "vec2" : "vec4";

        static string Literal(float value) => value.ToString("0.0##", CultureInfo.InvariantCulture);

        static string Name(string name) =>
            new([.. name.Select(static c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);
    }
}
