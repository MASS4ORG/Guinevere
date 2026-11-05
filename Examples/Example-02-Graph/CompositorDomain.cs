using System.Text.Json;
using MASS4.Graph.Dataflow;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

/// <summary>A greyscale video frame; nodes reuse their frames between evaluations.</summary>
public sealed class VideoFrame(int width, int height)
{
    /// <summary>Pixels per row.</summary>
    public int Width { get; } = width;
    /// <summary>Rows.</summary>
    public int Height { get; } = height;
    /// <summary>Row-major luminance in [0, 1].</summary>
    public float[] Luma { get; } = new float[width * height];

    /// <summary>Returns <paramref name="existing"/> when it has the requested size, otherwise a new frame.</summary>
    public static VideoFrame Reuse(VideoFrame? existing, int width, int height) =>
        existing is not null && existing.Width == width && existing.Height == height ? existing : new(width, height);
}

/// <summary>Evaluation-context feature: the playhead position and the size of rendered frames.</summary>
public sealed class Playhead
{
    /// <summary>Seconds from the start of the composition.</summary>
    public double Seconds { get; set; }
    /// <summary>Pixels per row.</summary>
    public int Width { get; init; } = 24;
    /// <summary>Rows.</summary>
    public int Height { get; init; } = 12;
}

/// <summary>Payload of a media node holding one number: ramp speed, pulse frequency or envelope duration.</summary>
/// <param name="Value">The parameter.</param>
public sealed record MediaPayload(double Value);

/// <summary>
/// A video compositor domain registered through the public Graph API: time-driven media sources, an envelope, a
/// blend and a declared frame output. Nothing here references an engine or a media library.
/// </summary>
public sealed partial class CompositorDomain : IPreviewDomain
{
    /// <summary>Numbers such as times and mixes.</summary>
    public static PortType Number { get; } = new(Id(0x001), "number");
    /// <summary>Frames, carried as object references.</summary>
    public static PortType FrameType { get; } = new(Id(0x002), "frame");
    /// <summary>Output of every single-output node.</summary>
    public static StableId OutKey { get; } = Id(0x100);
    /// <summary>The only or first input.</summary>
    public static StableId AKey { get; } = Id(0x101);
    /// <summary>The second input.</summary>
    public static StableId BKey { get; } = Id(0x102);
    /// <summary>The mix input of the blend.</summary>
    public static StableId MixKey { get; } = Id(0x103);
    /// <summary>The playhead time.</summary>
    public static NodeType Time { get; } = new(Id(0x201), "Time", [Out(Number)]);
    /// <summary>A horizontally scrolling ramp; its payload is the speed in frame widths per second.</summary>
    public static NodeType Ramp { get; } = Media(0x202, "Ramp", [Out(FrameType)], 0.25);
    /// <summary>A uniform sine pulse; its payload is the frequency in hertz.</summary>
    public static NodeType Pulse { get; } = Media(0x203, "Pulse", [Out(FrameType)], 0.5);
    /// <summary>A rise from 0 to 1; its payload is the duration in seconds.</summary>
    public static NodeType Envelope { get; } = Media(0x204, "Envelope",
        [PortDeclaration.Input(AKey, "time", Number.Id), Out(Number)], 2);
    /// <summary>Blends the top frame over the bottom frame.</summary>
    public static NodeType Over { get; } = new(Id(0x205), "Over",
    [
        PortDeclaration.Input(AKey, "top", FrameType.Id), PortDeclaration.Input(BKey, "bottom", FrameType.Id),
        PortDeclaration.Input(MixKey, "mix", Number.Id), Out(FrameType)
    ]);

    /// <summary>Creates a registry, semantics and reversible editor for this domain.</summary>
    public CompositorDomain()
    {
        var registry = new GraphTypeRegistry();
        registry.PortTypes.Register(Number);
        registry.PortTypes.Register(FrameType);
        foreach (var node in new[] { Time, Ramp, Pulse, Envelope, Over })
        {
            registry.NodeTypes.Register(node);
        }
        DataflowNodes.Register(registry);
        Semantics = new DataflowSemantics()
            .AddValueType<double>(Number.Id)
            .AddReferenceType<VideoFrame>(FrameType.Id)
            .AddNode(Time.Id, new DataflowNode(static c => c.Write(0, c.Evaluation.Get<Playhead>().Seconds),
                isVolatile: true, typeof(Playhead)))
            .AddNode(Ramp.Id, new DataflowNode(RenderRamp, isVolatile: true, typeof(Playhead)))
            .AddNode(Pulse.Id, new DataflowNode(RenderPulse, isVolatile: true, typeof(Playhead)))
            .AddNode(Envelope.Id, new DataflowNode(static c =>
                c.Write(1, EnvelopeAt(c.Read<double>(0), ((MediaPayload)c.Payload!).Value))))
            .AddNode(Over.Id, new DataflowNode(Blend, isVolatile: false, typeof(Playhead)));
        Editor = new GraphEditor(new GraphDocument(registry), Semantics.Conversions, Semantics.Rules);
    }

    /// <summary>The composition's actual dataflow behaviors and validation rules.</summary>
    public DataflowSemantics Semantics { get; }
    /// <summary>The sole mutation entry point for the composition.</summary>
    public GraphEditor Editor { get; private set; }
    /// <summary>Thumbnails follow the running playhead.</summary>
    public bool Animated => true;

    /// <summary>Luminance of the scrolling ramp at a column and time.</summary>
    public static float RampAt(int x, int width, double speed, double seconds)
    {
        var position = (double)x / width + speed * seconds;
        return (float)(position - Math.Floor(position));
    }

    /// <summary>Luminance of the uniform pulse at a time.</summary>
    public static float PulseAt(double frequency, double seconds) =>
        (float)(0.5 + 0.5 * Math.Sin(2 * Math.PI * frequency * seconds));

    /// <summary>A linear rise from 0 to 1 over <paramref name="duration"/> seconds.</summary>
    public static double EnvelopeAt(double seconds, double duration) =>
        duration <= 0 ? 1 : Math.Clamp(seconds / duration, 0, 1);

    /// <inheritdoc />
    public void UseEditor(GraphEditor editor) => Editor = editor;

    /// <inheritdoc />
    public string Describe(GraphNode node) =>
        node.Payload is VariableReference reference && Editor.Document.TryGetVariable(reference.Variable, out var v)
            ? "Output · " + v.Name
            : Editor.Document.Registry.NodeTypes.TryResolve(node.TypeId, out var type) ? type.DisplayName : "Unknown";

    /// <inheritdoc />
    public object? Stepped(GraphNode node, float delta) => node.Payload is MediaPayload media
        ? new MediaPayload(Math.Round(Math.Max(Minimum(node), media.Value + delta * Increment(node)), 2))
        : null;

    /// <inheritdoc />
    public string FormatPayload(GraphNode node)
    {
        var value = (node.Payload as MediaPayload)?.Value ?? 0;
        var text = value.ToString("0.00", CultureInfo.InvariantCulture);
        return node.TypeId == Ramp.Id ? text + " widths/s" : node.TypeId == Pulse.Id ? text + " Hz" : text + " s";
    }

    /// <inheritdoc />
    public bool HasPreview(GraphNode node) =>
        node.TypeId == Ramp.Id || node.TypeId == Pulse.Id || node.TypeId == Over.Id;

    /// <summary>Builds frame = over(pulse, ramp, envelope(time)): a pulse fading in over a scrolling ramp.</summary>
    public void Populate(GraphDocumentView view)
    {
        var frame = new GraphVariable(ElementId.New(), "frame", FrameType.Id, VariableScope.Output);
        Require(view.Execute(new SetVariableCommand(frame)));
        var pulse = view.AddNode(Pulse, new GraphPoint(40, 40));
        var ramp = view.AddNode(Ramp, new GraphPoint(40, 300));
        var time = view.AddNode(Time, new GraphPoint(40, 560));
        var envelope = view.AddNode(Envelope, new GraphPoint(340, 520));
        var blend = view.AddNode(Over, new GraphPoint(640, 180));
        var output = view.AddNode(DataflowNodes.Output, new GraphPoint(940, 240), new VariableReference(frame.Id));
        Connect(view, time, OutKey, envelope, AKey);
        Connect(view, pulse, OutKey, blend, AKey);
        Connect(view, ramp, OutKey, blend, BKey);
        Connect(view, envelope, OutKey, blend, MixKey);
        Connect(view, blend, OutKey, output, DataflowNodes.ResultKey);
    }

    static double Increment(GraphNode node) => node.TypeId == Envelope.Id ? 0.5 : node.TypeId == Pulse.Id ? 0.25 : 0.05;

    static double Minimum(GraphNode node) => node.TypeId == Envelope.Id ? 0.5 : node.TypeId == Pulse.Id ? 0 : -2;

    static void RenderRamp(DataflowContext context)
    {
        var frame = Target(context, 0);
        var speed = ((MediaPayload)context.Payload!).Value;
        var seconds = context.Evaluation.Get<Playhead>().Seconds;
        for (var i = 0; i < frame.Luma.Length; i++)
        {
            frame.Luma[i] = RampAt(i % frame.Width, frame.Width, speed, seconds);
        }
        context.WriteReference(0, frame);
    }

    static void RenderPulse(DataflowContext context)
    {
        var frame = Target(context, 0);
        var seconds = context.Evaluation.Get<Playhead>().Seconds;
        frame.Luma.AsSpan().Fill(PulseAt(((MediaPayload)context.Payload!).Value, seconds));
        context.WriteReference(0, frame);
    }

    static void Blend(DataflowContext context)
    {
        var frame = Target(context, 3);
        var top = context.ReadReference<VideoFrame>(0);
        var bottom = context.ReadReference<VideoFrame>(1);
        var mix = context.Read<double>(2);
        for (var i = 0; i < frame.Luma.Length; i++)
        {
            frame.Luma[i] = (float)(Pixel(top, i) * mix + Pixel(bottom, i) * (1 - mix));
        }
        context.WriteReference(3, frame);
    }

    static VideoFrame Target(DataflowContext context, int output)
    {
        var playhead = context.Evaluation.Get<Playhead>();
        return VideoFrame.Reuse(context.ReadReference<VideoFrame>(output), playhead.Width, playhead.Height);
    }

    static float Pixel(VideoFrame? frame, int index) =>
        frame is not null && index < frame.Luma.Length ? frame.Luma[index] : 0;

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

    static NodeType Media(int id, string name, PortDeclaration[] ports, double initial) => new(Id(id), name, ports)
    {
        PayloadSerializer = MediaSerializer.Instance,
        DefaultPayload = () => new MediaPayload(initial)
    };

    static PortDeclaration Out(PortType type) => PortDeclaration.Output(OutKey, "out", type.Id);

    static StableId Id(int value) => new(new Guid($"c0b0f0e1-0000-4000-8000-{value:x12}"));

    sealed class MediaSerializer : IValueSerializer
    {
        public static MediaSerializer Instance { get; } = new();

        public void Write(Utf8JsonWriter writer, object? value) => writer.WriteNumberValue(((MediaPayload)value!).Value);

        public object? Read(JsonElement element) => new MediaPayload(element.GetDouble());
    }
}
