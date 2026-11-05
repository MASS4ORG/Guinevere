using MASS4.Graph.Dataflow;

namespace Example02;

/// <summary>A sample dataflow domain hosted by the shared sandbox editor; the canvas never reads its payloads.</summary>
public interface IDemoDomain
{
    /// <summary>The domain's dataflow behaviors, conversions and validation rules.</summary>
    DataflowSemantics Semantics { get; }
    /// <summary>The sole mutation entry point for the document.</summary>
    GraphEditor Editor { get; }
    /// <summary>Adopts a loaded editor that uses this domain's registry and rules.</summary>
    void UseEditor(GraphEditor editor);
    /// <summary>The domain-owned node title.</summary>
    string Describe(GraphNode node);
    /// <summary>The payload one stepper increment produces, or null for nodes without a stepper.</summary>
    object? Stepped(GraphNode node, float delta);
    /// <summary>The value shown between a node's stepper buttons.</summary>
    string FormatPayload(GraphNode node);
    /// <summary>Whether the node reserves a slot for a background-rendered preview.</summary>
    bool HasPreview(GraphNode node);
    /// <summary>Builds the initial document through the view's real editor commands.</summary>
    void Populate(GraphDocumentView view);
}

/// <summary>A domain whose nodes are previewed by evaluating the compiled graph off the UI thread.</summary>
public interface IPreviewDomain : IDemoDomain
{
    /// <summary>Whether previews depend on a running timeline and are re-rendered while playing.</summary>
    bool Animated { get; }
    /// <summary>Creates an evaluation context carrying every feature the domain's nodes require.</summary>
    EvaluationContext CreateContext();
    /// <summary>
    /// Evaluates one instance on a background thread and returns previews keyed by node identity and by declared
    /// output variable identity.
    /// </summary>
    /// <param name="run">The compiled artifact and its private instance state.</param>
    /// <param name="ports">Previewed output ports mapped to their nodes, captured on the document-owning thread.</param>
    /// <param name="seconds">The timeline position.</param>
    Task<IReadOnlyDictionary<ElementId, NodePreview>> RenderAsync(PreviewRun run,
        IReadOnlyDictionary<ElementId, ElementId> ports, double seconds);
    /// <summary>The domain's generated artifact for the current document, such as shader source.</summary>
    string Generate(GraphDocument document);
}

/// <summary>A small immutable RGBA image drawn as cells inside a node's content slot.</summary>
/// <param name="Width">Cells per row.</param>
/// <param name="Height">Rows.</param>
/// <param name="Pixels">Row-major packed <c>0xRRGGBBAA</c> colors.</param>
public sealed record NodePreview(int Width, int Height, uint[] Pixels)
{
    /// <summary>Packs unit-range channels, clamping out-of-range values.</summary>
    public static uint Pack(float r, float g, float b, float a = 1) =>
        (Byte(r) << 24) | (Byte(g) << 16) | (Byte(b) << 8) | Byte(a);

    /// <summary>Draws the preview's cells centered in a rectangle, preserving their aspect ratio.</summary>
    public void Draw(Gui gui, Rect rect)
    {
        var cell = MathF.Min(rect.W / Width, rect.H / Height);
        var originX = rect.X + (rect.W - cell * Width) / 2;
        var originY = rect.Y + (rect.H - cell * Height) / 2;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var left = originX + x * cell;
                var top = originY + y * cell;
                // Overlapping by half a unit hides seams between anti-aliased cells.
                gui.DrawRectFilled(left, top, left + cell + 0.5f, top + cell + 0.5f,
                    Color.FromRgba(Pixels[y * Width + x]));
            }
        }
    }

    static uint Byte(float value) => (uint)MathF.Round(Math.Clamp(float.IsNaN(value) ? 0 : value, 0, 1) * 255);
}

/// <summary>One compiled artifact with private instance state, used only by the background renderer.</summary>
/// <param name="Graph">The compiled artifact, independent of later document edits.</param>
/// <param name="Context">The domain's evaluation context.</param>
/// <param name="Instances">The private instance buffer.</param>
/// <param name="Instance">The previewed instance.</param>
public sealed record PreviewRun(ICompiledGraph Graph, EvaluationContext Context, InstanceBuffer Instances,
    InstanceHandle Instance);
