using System.Globalization;
using MASS4.Graph;
using MASS4.Graph.Dataflow;

namespace Example02;

/// <summary>A real arithmetic dataflow domain registered through the public Graph API.</summary>
public sealed class ArithmeticGraph
{
    /// <summary>The numeric port type.</summary>
    public static PortType NumberType { get; } = new(Id(1), "number");
    /// <summary>The output port declaration key.</summary>
    public static StableId ValueKey { get; } = Id(2);
    /// <summary>The first input declaration key.</summary>
    public static StableId AKey { get; } = Id(3);
    /// <summary>The second input declaration key.</summary>
    public static StableId BKey { get; } = Id(4);
    /// <summary>A constant numeric value.</summary>
    public static NodeType Number { get; } = new(Id(5), "Number",
        [PortDeclaration.Output(ValueKey, "Value", NumberType.Id)]) { DefaultPayload = static () => 1f };
    /// <summary>Adds two numeric inputs.</summary>
    public static NodeType Add { get; } = Binary(6, "Add");
    /// <summary>Multiplies two numeric inputs.</summary>
    public static NodeType Multiply { get; } = Binary(7, "Multiply");
    /// <summary>Exposes a numeric result as an inspectable output port.</summary>
    public static NodeType Result { get; } = new(Id(8), "Result",
        [PortDeclaration.Input(AKey, "Input", NumberType.Id),
            PortDeclaration.Output(ValueKey, "Value", NumberType.Id)]);

    /// <summary>Creates a registry, semantics and reversible editor for this domain.</summary>
    public ArithmeticGraph()
    {
        var registry = new GraphTypeRegistry();
        registry.PortTypes.Register(NumberType);
        foreach (var type in new[] { Number, Add, Multiply, Result })
        {
            registry.NodeTypes.Register(type);
        }
        Semantics = new DataflowSemantics().AddValueType<float>(NumberType.Id)
            .AddNode(Number.Id, new DataflowNode(static c => c.Write(0, (float)c.Payload!)))
            .AddNode(Add.Id, new DataflowNode(static c => c.Write(2, c.Read<float>(0) + c.Read<float>(1))))
            .AddNode(Multiply.Id, new DataflowNode(static c => c.Write(2, c.Read<float>(0) * c.Read<float>(1))))
            .AddNode(Result.Id, new DataflowNode(static c => c.Write(1, c.Read<float>(0))));
        Editor = new GraphEditor(new GraphDocument(registry), Semantics.Conversions, Semantics.Rules);
    }

    /// <summary>The actual dataflow behaviors and validation rules.</summary>
    public DataflowSemantics Semantics { get; }
    /// <summary>The sole mutation entry point for the document.</summary>
    public GraphEditor Editor { get; }

    /// <summary>Domain-owned presentation of numeric payloads.</summary>
    public string Describe(GraphNode node) => node.TypeId == Number.Id && node.Payload is float value
        ? "Number · " + value.ToString(CultureInfo.InvariantCulture)
        : Editor.Document.Registry.NodeTypes.Resolve(node.TypeId).DisplayName;

    static NodeType Binary(int id, string name) => new(Id(id), name,
        [PortDeclaration.Input(AKey, "A", NumberType.Id), PortDeclaration.Input(BKey, "B", NumberType.Id),
            PortDeclaration.Output(ValueKey, "Value", NumberType.Id)]);

    static StableId Id(int number) => StableId.Parse($"5193d7a0-8a7d-4f7e-9238-{number:000000000000}");
}
