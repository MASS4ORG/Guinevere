using System.Text.Json;
using System.Text.Json.Nodes;
using MASS4.Graph;

namespace Guinevere.Graphs;

/// <summary>A loaded editor and diagnostics; malformed input never produces a partial view.</summary>
/// <param name="View">The loaded view, or null if errors prevent a lossless load.</param>
/// <param name="Diagnostics">Core migration, unknown-content and view-layout findings.</param>
public sealed record GraphViewLoadResult(GraphDocumentView? View, IReadOnlyList<Diagnostic> Diagnostics);

public sealed partial class GraphDocumentView
{
    JsonObject _layoutExtensions = [];
    JsonObject _viewExtensions = [];

    /// <summary>Serializes the entire real document, optionally embedding authored layout without transient UI state.</summary>
    public string Serialize(bool includeLayout = true)
    {
        var root = JsonNode.Parse(GraphJson.Serialize(Editor.Document, new GraphWriteOptions { IncludeLayout = false }))!
            .AsObject();
        if (includeLayout)
        {
            root["layout"] = LayoutObject();
        }
        return root.ToJsonString(JsonOptions);
    }

    /// <summary>Serializes authored positions separately, suitable for an optional source-control sidecar.</summary>
    public string SerializeLayout() => LayoutObject().ToJsonString(JsonOptions);

    /// <summary>Serializes camera state separately; selection, focus, hover and unfinished connections are omitted.</summary>
    public string SerializeViewState()
    {
        var root = _viewExtensions.DeepClone().AsObject();
        root["format"] = "mass4.guinevere.graph-view";
        root["version"] = 1;
        root["assetId"] = Editor.Document.AssetId.ToString();
        root["camera"] = new JsonObject
        {
            ["x"] = Canvas.Camera.Origin.X,
            ["y"] = Canvas.Camera.Origin.Y,
            ["zoom"] = Canvas.Camera.Zoom
        };
        return root.ToJsonString(JsonOptions);
    }

    /// <summary>Loads with the core's registered serializers, renames and migrations, retaining unavailable nodes.</summary>
    public static GraphViewLoadResult Load(string json, GraphTypeRegistry registry, string? layout = null,
        string? viewState = null, IPortCompatibility? compatibility = null, IEnumerable<IGraphRule>? rules = null)
    {
        var loaded = ReadDocument(json, registry);
        if (loaded.HasErrors || loaded.Document is not { } document)
        {
            return new(null, loaded.Diagnostics);
        }
        var view = new GraphDocumentView(new GraphEditor(document, compatibility, rules));
        try
        {
            var root = JsonNode.Parse(json)!.AsObject();
            if (layout is not null || root["layout"] is JsonObject)
            {
                view.ReadLayout(layout is null ? root["layout"]!.AsObject() : ParseObject(layout));
            }
            if (viewState is not null)
            {
                view.ReadViewState(ParseObject(viewState));
            }
            view.Refresh();
            view.Diagnostics = loaded.Diagnostics;
            return new(view, loaded.Diagnostics);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or FormatException or ArgumentException or OverflowException)
        {
            return new(null, [.. loaded.Diagnostics,
                new Diagnostic("GVG2001", DiagnosticSeverity.Error, null, $"Invalid graph view data: {exception.Message}")]);
        }
    }

    static GraphLoadResult ReadDocument(string json, GraphTypeRegistry registry)
    {
        try
        {
            return GraphJson.Deserialize(json, registry);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or FormatException or ArgumentException or OverflowException)
        {
            return new(null, [new Diagnostic("GVG2001", DiagnosticSeverity.Error, null,
                $"Invalid graph document: {exception.Message}")]);
        }
    }

    JsonObject LayoutObject()
    {
        var root = _layoutExtensions.DeepClone().AsObject();
        root["format"] = "mass4.guinevere.graph-layout";
        root["version"] = 1;
        root["assetId"] = Editor.Document.AssetId.ToString();
        var previous = (_layoutExtensions["positions"]?.AsArray() ?? [])
            .ToDictionary(static item => item!["node"]!.GetValue<string>(), StringComparer.Ordinal);
        root["positions"] = new JsonArray(Editor.Document.Nodes.OrderBy(static node => node.Id).Select(node =>
        {
            var position = EnsurePosition(node.Id);
            var saved = previous.GetValueOrDefault(node.Id.ToString())?.DeepClone().AsObject() ?? [];
            saved["node"] = node.Id.ToString();
            saved["x"] = position.X;
            saved["y"] = position.Y;
            return (JsonNode)saved;
        }).ToArray());
        return root;
    }

    void ReadLayout(JsonObject root)
    {
        // Core layout without a GUI header remains a valid input.
        if (root.ContainsKey("format"))
        {
            CheckHeader(root, "mass4.guinevere.graph-layout");
        }
        var positions = root["positions"]?.AsArray() ?? [];
        var seen = new HashSet<ElementId>();
        foreach (var item in positions)
        {
            ReadPosition(item, seen);
        }
        _layoutExtensions = root.DeepClone().AsObject();
    }

    void ReadPosition(JsonNode? item, HashSet<ElementId> seen)
    {
        var position = item?.AsObject() ?? throw new FormatException("A layout position must be an object.");
        var id = ElementId.Parse(position["node"]?.GetValue<string>()
            ?? throw new FormatException("A layout position requires a node identity."));
        if (!seen.Add(id) || !Editor.Document.TryGetNode(id, out _))
        {
            throw new FormatException("Layout positions must name distinct existing nodes.");
        }
        SetPosition(id, new GraphPoint(Number(position, "x"), Number(position, "y")));
        _initialPositions[id] = _positions[id];
    }

    void ReadViewState(JsonObject root)
    {
        CheckHeader(root, "mass4.guinevere.graph-view");
        var camera = root["camera"] ?? throw new FormatException("Camera state is missing.");
        Canvas.Camera.SetView(new GraphPoint(Number(camera, "x"), Number(camera, "y")), Number(camera, "zoom"));
        _viewExtensions = root.DeepClone().AsObject();
    }

    void CheckHeader(JsonObject root, string format)
    {
        if (root["format"]?.GetValue<string>() != format || root["version"]?.GetValue<int>() != 1
            || root["assetId"]?.GetValue<string>() != Editor.Document.AssetId.ToString())
        {
            throw new FormatException("Unsupported view version, format or asset identity.");
        }
    }

    static double Number(JsonNode node, string property)
    {
        var number = node[property]?.GetValue<double>() ?? throw new FormatException($"Missing '{property}'.");
        GraphBounds.RequireFinite(number);
        return number;
    }

    static JsonObject ParseObject(string json) => JsonNode.Parse(json)?.AsObject()
        ?? throw new FormatException("A JSON object is required.");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
