using System.Text.Json;

namespace Guinevere;

/// <summary>
/// Reads and writes <see cref="DockLayout"/> as JSON. The tree is polymorphic, so each node carries
/// a <c>kind</c> discriminator; the whole document carries a <c>version</c> and a reader that does
/// not recognise it declines rather than guessing.
/// </summary>
static class DockLayoutSerializer
{
    public static string Serialize(DockLayout layout)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", layout.Version);

            writer.WritePropertyName("root");
            WriteNode(writer, layout.Root);

            writer.WritePropertyName("floating");
            writer.WriteStartArray();
            foreach (var window in layout.Floating)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("bounds");
                writer.WriteStartObject();
                writer.WriteNumber("x", window.Bounds.X);
                writer.WriteNumber("y", window.Bounds.Y);
                writer.WriteNumber("w", window.Bounds.W);
                writer.WriteNumber("h", window.Bounds.H);
                writer.WriteEndObject();
                writer.WritePropertyName("root");
                WriteNode(writer, window.Root);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static DockLayout? Deserialize(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("version", out var version) || version.GetInt32() != DockLayout.CurrentVersion)
                return null;

            var layout = new DockLayout
            {
                Root = root.TryGetProperty("root", out var rootNode) ? ReadNode(rootNode) : null
            };

            if (root.TryGetProperty("floating", out var floating) && floating.ValueKind == JsonValueKind.Array)
                foreach (var window in floating.EnumerateArray())
                {
                    var node = window.TryGetProperty("root", out var windowRoot) ? ReadNode(windowRoot) : null;
                    if (node is null) continue;

                    layout.Floating.Add(new DockFloat(node, ReadRect(window)));
                }

            return layout;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static Rect ReadRect(JsonElement window)
    {
        if (!window.TryGetProperty("bounds", out var bounds)) return new Rect(0, 0, 320, 240);

        return new Rect(
            bounds.GetProperty("x").GetSingle(),
            bounds.GetProperty("y").GetSingle(),
            bounds.GetProperty("w").GetSingle(),
            bounds.GetProperty("h").GetSingle());
    }

    static void WriteNode(Utf8JsonWriter writer, DockNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;

            case DockLeaf leaf:
                writer.WriteStartObject();
                writer.WriteString("kind", "leaf");
                writer.WriteNumber("active", leaf.ActiveIndex);
                writer.WriteString("zone", leaf.Zone.ToString());
                writer.WritePropertyName("panels");
                writer.WriteStartArray();
                foreach (var panelId in leaf.PanelIds) writer.WriteStringValue(panelId);
                writer.WriteEndArray();
                writer.WriteEndObject();
                return;

            case DockSplit split:
                writer.WriteStartObject();
                writer.WriteString("kind", "split");
                writer.WriteString("axis", split.Axis == Axis.Horizontal ? "horizontal" : "vertical");
                writer.WriteNumber("fraction", split.Fraction);
                writer.WritePropertyName("first");
                WriteNode(writer, split.First);
                writer.WritePropertyName("second");
                WriteNode(writer, split.Second);
                writer.WriteEndObject();
                return;

            default:
                writer.WriteNullValue();
                return;
        }
    }

    static DockNode? ReadNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("kind", out var kind)) return null;

        switch (kind.GetString())
        {
            case "leaf":
                {
                    var leaf = new DockLeaf();
                    if (element.TryGetProperty("panels", out var panels) && panels.ValueKind == JsonValueKind.Array)
                        foreach (var panel in panels.EnumerateArray())
                            if (panel.GetString() is { } panelId)
                                leaf.PanelIds.Add(panelId);

                    if (element.TryGetProperty("active", out var active)) leaf.ActiveIndex = active.GetInt32();
                    if (element.TryGetProperty("zone", out var zone) && Enum.TryParse<DockZone>(zone.GetString(), out var parsed))
                        leaf.Zone = parsed;

                    return leaf.PanelIds.Count == 0 ? null : leaf;
                }

            case "split":
                {
                    var first = element.TryGetProperty("first", out var f) ? ReadNode(f) : null;
                    var second = element.TryGetProperty("second", out var s) ? ReadNode(s) : null;

                    // A split that lost a side on the way in collapses, the same way removal collapses it.
                    if (first is null) return second;
                    if (second is null) return first;

                    var axis = element.TryGetProperty("axis", out var a) && a.GetString() == "horizontal"
                        ? Axis.Horizontal
                        : Axis.Vertical;
                    var fraction = element.TryGetProperty("fraction", out var fr) ? fr.GetSingle() : 0.5f;

                    return new DockSplit(axis, first, second, fraction);
                }

            default:
                return null;
        }
    }
}
