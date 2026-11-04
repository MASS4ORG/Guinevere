using MASS4.Graph.Dataflow;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

/// <summary>
/// A material graph domain registered through the public Graph API: typed scalar, color and texture-coordinate
/// ports, a procedural checker, color math, an editor-inserted scalar-to-color conversion, an exposed tint parameter
/// and a declared albedo output.
/// </summary>
public sealed partial class ShaderDomain : IPreviewDomain
{
    /// <summary>Scalar values.</summary>
    public static PortType Scalar { get; } = new(Id(0x001), "scalar") { ValueSerializer = ShaderJson.Scalar };
    /// <summary>Color values.</summary>
    public static PortType ColorType { get; } = new(Id(0x002), "color") { ValueSerializer = ShaderJson.Color };
    /// <summary>Texture coordinates.</summary>
    public static PortType UvType { get; } = new(Id(0x003), "uv");
    /// <summary>Output of every single-output node.</summary>
    public static StableId OutKey { get; } = Id(0x100);
    /// <summary>The first or only input.</summary>
    public static StableId AKey { get; } = Id(0x101);
    /// <summary>The second input.</summary>
    public static StableId BKey { get; } = Id(0x102);
    /// <summary>The interpolation factor of Lerp.</summary>
    public static StableId TKey { get; } = Id(0x103);
    /// <summary>The coordinates input of Checker.</summary>
    public static StableId UvKey { get; } = Id(0x104);
    /// <summary>A scalar constant.</summary>
    public static NodeType ScalarConstant { get; } = new(Id(0x201), "Scalar", [Out(Scalar)])
    {
        PayloadSerializer = ShaderJson.ScalarPayload,
        DefaultPayload = static () => new ScalarPayload(0.5f)
    };
    /// <summary>A color constant.</summary>
    public static NodeType ColorConstant { get; } = new(Id(0x202), "Color", [Out(ColorType)])
    {
        PayloadSerializer = ShaderJson.ColorPayload,
        DefaultPayload = static () => new ColorPayload(new(1, 0.4f, 0.2f, 1))
    };
    /// <summary>The texture coordinates of the fragment being shaded.</summary>
    public static NodeType TexCoord { get; } = new(Id(0x203), "Texture coordinates", [Out(UvType)]);
    /// <summary>A procedural checkerboard; its payload is the number of tiles per side.</summary>
    public static NodeType Checker { get; } = new(Id(0x204), "Checker",
        [In(UvKey, "uv", UvType), In(AKey, "a", ColorType), In(BKey, "b", ColorType), Out(ColorType)])
    {
        PayloadSerializer = ShaderJson.ScalarPayload,
        DefaultPayload = static () => new ScalarPayload(4)
    };
    /// <summary>Multiplies two colors.</summary>
    public static NodeType Multiply { get; } = new(Id(0x205), "Multiply",
        [In(AKey, "a", ColorType), In(BKey, "b", ColorType), Out(ColorType)]);
    /// <summary>Scales a color by a scalar.</summary>
    public static NodeType ScaleColor { get; } = new(Id(0x206), "Scale",
        [In(AKey, "color", ColorType), In(BKey, "factor", Scalar), Out(ColorType)]);
    /// <summary>Interpolates between two colors.</summary>
    public static NodeType Lerp { get; } = new(Id(0x207), "Lerp",
        [In(AKey, "a", ColorType), In(BKey, "b", ColorType), In(TKey, "t", Scalar), Out(ColorType)]);
    /// <summary>The conversion the editor inserts between scalar outputs and color inputs.</summary>
    public static NodeType ScalarToColor { get; } = new(Id(0x208), "Scalar to color",
        [In(AKey, "scalar", Scalar), Out(ColorType)]);

    /// <summary>Creates a registry, semantics and reversible editor for this domain.</summary>
    public ShaderDomain()
    {
        var registry = new GraphTypeRegistry();
        foreach (var type in new[] { Scalar, ColorType, UvType })
        {
            registry.PortTypes.Register(type);
        }
        foreach (var node in new[] { ScalarConstant, ColorConstant, TexCoord, Checker, Multiply, ScaleColor, Lerp,
                     ScalarToColor })
        {
            registry.NodeTypes.Register(node);
        }
        DataflowNodes.Register(registry);
        Semantics = new DataflowSemantics()
            .AddValueType<float>(Scalar.Id)
            .AddValueType<Rgba>(ColorType.Id)
            .AddValueType<Uv>(UvType.Id)
            .AddNode(ScalarConstant.Id, new DataflowNode(static c => c.Write(0, ((ScalarPayload)c.Payload!).Value)))
            .AddNode(ColorConstant.Id, new DataflowNode(static c => c.Write(0, ((ColorPayload)c.Payload!).Value)))
            .AddNode(TexCoord.Id, new DataflowNode(static c => c.Write(0, c.Evaluation.Get<Fragment>().Uv),
                isVolatile: true, typeof(Fragment)))
            .AddNode(Checker.Id, new DataflowNode(static c => c.Write(3,
                CheckerColor(c.Read<Uv>(0), c.Read<Rgba>(1), c.Read<Rgba>(2), ((ScalarPayload)c.Payload!).Value))))
            .AddNode(Multiply.Id, new DataflowNode(static c => c.Write(2, c.Read<Rgba>(0) * c.Read<Rgba>(1))))
            .AddNode(ScaleColor.Id, new DataflowNode(static c => c.Write(2, c.Read<Rgba>(0) * c.Read<float>(1))))
            .AddNode(Lerp.Id, new DataflowNode(static c =>
                c.Write(3, Rgba.Lerp(c.Read<Rgba>(0), c.Read<Rgba>(1), c.Read<float>(2)))))
            .AddConversionNode(ScalarToColor, new DataflowNode(static c => c.Write(1, Rgba.Splat(c.Read<float>(0)))));
        Editor = new GraphEditor(new GraphDocument(registry), Semantics.Conversions, Semantics.Rules);
    }

    /// <summary>The material's actual dataflow behaviors, conversion and validation rules.</summary>
    public DataflowSemantics Semantics { get; }
    /// <summary>The sole mutation entry point for the material.</summary>
    public GraphEditor Editor { get; private set; }
    /// <summary>Material previews are static.</summary>
    public bool Animated => false;

    /// <summary>The checker's color at a coordinate.</summary>
    public static Rgba CheckerColor(Uv uv, Rgba a, Rgba b, float tiles)
    {
        var cell = (int)MathF.Floor(uv.U * tiles) + (int)MathF.Floor(uv.V * tiles);
        return (cell & 1) == 0 ? a : b;
    }

    /// <inheritdoc />
    public void UseEditor(GraphEditor editor) => Editor = editor;

    /// <inheritdoc />
    public string Describe(GraphNode node)
    {
        if (node.Payload is VariableReference reference
            && Editor.Document.TryGetVariable(reference.Variable, out var variable))
        {
            return (node.TypeId == DataflowNodes.Output.Id ? "Output · " : "Parameter · ") + variable.Name;
        }
        return Editor.Document.Registry.NodeTypes.TryResolve(node.TypeId, out var type) ? type.DisplayName : "Unknown";
    }

    /// <inheritdoc />
    public object? Stepped(GraphNode node, float delta) => node.Payload switch
    {
        ScalarPayload scalar when node.TypeId == Checker.Id => new ScalarPayload(Math.Max(1, scalar.Value + delta)),
        ScalarPayload scalar => new ScalarPayload(MathF.Round(scalar.Value + delta * 0.05f, 2)),
        ColorPayload color => new ColorPayload(color.Value.RotateHue(delta)),
        _ => null
    };

    /// <inheritdoc />
    public string FormatPayload(GraphNode node) => node.Payload switch
    {
        ScalarPayload scalar when node.TypeId == Checker.Id =>
            string.Create(CultureInfo.InvariantCulture, $"{scalar.Value:0} × {scalar.Value:0} tiles"),
        ScalarPayload scalar => scalar.Value.ToString("0.00", CultureInfo.InvariantCulture),
        ColorPayload color => "#" + (color.Value.Pack() >> 8).ToString("X6", CultureInfo.InvariantCulture),
        _ => ""
    };

    /// <inheritdoc />
    public bool HasPreview(GraphNode node) => node.TypeId == TexCoord.Id || node.TypeId == Checker.Id
        || node.TypeId == Multiply.Id || node.TypeId == ScaleColor.Id || node.TypeId == Lerp.Id
        || node.TypeId == ScalarToColor.Id;

    /// <summary>Builds albedo = lerp(checker × tint, 0.5 grey, 0.25), inserting the grey conversion by connection.</summary>
    public void Populate(GraphDocumentView view)
    {
        var tint = new GraphVariable(ElementId.New(), "tint", ColorType.Id, VariableScope.Input)
        { DefaultValue = new Rgba(1, 0.65f, 0.35f, 1) };
        var albedo = new GraphVariable(ElementId.New(), "albedo", ColorType.Id, VariableScope.Output)
        { DefaultValue = new Rgba(0, 0, 0, 1) };
        Require(view.Execute(new SetVariableCommand(tint)));
        Require(view.Execute(new SetVariableCommand(albedo)));
        var uv = view.AddNode(TexCoord, new GraphPoint(40, 40));
        var light = view.AddNode(ColorConstant, new GraphPoint(40, 250), new ColorPayload(new(0.95f, 0.92f, 0.85f, 1)));
        var dark = view.AddNode(ColorConstant, new GraphPoint(40, 380), new ColorPayload(new(0.18f, 0.24f, 0.36f, 1)));
        var checker = view.AddNode(Checker, new GraphPoint(290, 40), new ScalarPayload(4));
        var tintRead = view.AddNode(DataflowNodes.GetVariable, new GraphPoint(290, 300), new VariableReference(tint.Id));
        var grey = view.AddNode(ScalarConstant, new GraphPoint(290, 400), new ScalarPayload(0.5f));
        var amount = view.AddNode(ScalarConstant, new GraphPoint(290, 530), new ScalarPayload(0.25f));
        var tinted = view.AddNode(Multiply, new GraphPoint(540, 40));
        var mix = view.AddNode(Lerp, new GraphPoint(790, 140));
        var output = view.AddNode(DataflowNodes.Output, new GraphPoint(1040, 190), new VariableReference(albedo.Id));
        Connect(view, uv, OutKey, checker, UvKey);
        Connect(view, light, OutKey, checker, AKey);
        Connect(view, dark, OutKey, checker, BKey);
        Connect(view, checker, OutKey, tinted, AKey);
        Connect(view, tintRead, DataflowNodes.ValueKey, tinted, BKey);
        Connect(view, tinted, OutKey, mix, AKey);
        Connect(view, grey, OutKey, mix, BKey);
        Connect(view, amount, OutKey, mix, TKey);
        Connect(view, mix, OutKey, output, DataflowNodes.ResultKey);
        var conversion = view.Editor.Document.Nodes.Single(static node => node.TypeId == ScalarToColor.Id);
        view.SetPosition(conversion.Id, new GraphPoint(540, 330));
    }

    static void Connect(GraphDocumentView view, ElementId from, StableId output, ElementId to, StableId input)
    {
        var document = view.Editor.Document;
        Require(view.Connect(document.GetNode(from).GetPort(output).Id, document.GetNode(to).GetPort(input).Id));
    }

    static void Require(CommandResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(static d => d.Message)));
        }
    }

    static PortDeclaration In(StableId key, string name, PortType type) => PortDeclaration.Input(key, name, type.Id);

    static PortDeclaration Out(PortType type) => PortDeclaration.Output(OutKey, "out", type.Id);

    static StableId Id(int value) => new(new Guid($"5ade6e1e-0000-4000-8000-{value:x12}"));
}
