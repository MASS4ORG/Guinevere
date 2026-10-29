using System.Text.Json.Nodes;
using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    void FilterClipboardGroups(JsonObject root, HashSet<ElementId> nodes)
    {
        var groups = ClipboardGroupIds();
        FilterGroupEntries(root, groups);
        FilterGroupLayout(root, nodes, groups);
    }

    HashSet<ElementId> ClipboardGroupIds()
    {
        var selected = SelectionOrFocus().ToHashSet(StringComparer.Ordinal);
        return [.. Editor.Document.Groups.Where(group => selected.Contains(group.Id.ToString())
            || Editor.Document.Groups.Any(parent => selected.Contains(parent.Id.ToString())
                && Editor.Document.IsWithin(group.Id, parent.Id))).Select(static group => group.Id)];
    }

    static void FilterGroupEntries(JsonObject root, HashSet<ElementId> groups)
    {
        var entries = root["groups"]?.AsArray() ?? [];
        foreach (var entry in entries.ToArray())
        {
            var id = ElementId.Parse(entry!["id"]!.GetValue<string>());
            if (!groups.Contains(id))
            {
                entries.Remove(entry);
            }
            else if (entry["parent"] is { } parent && !groups.Contains(ElementId.Parse(parent.GetValue<string>())))
            {
                entry.AsObject().Remove("parent");
            }
        }
    }

    void FilterGroupLayout(JsonObject root, HashSet<ElementId> nodes, HashSet<ElementId> groups)
    {
        var layout = root["layout"]!.AsObject();
        layout["positions"] = new JsonArray(layout["positions"]!.AsArray().Where(item =>
            nodes.Contains(ElementId.Parse(item!["node"]!.GetValue<string>()))).Select(static item => item!.DeepClone()).ToArray());
        var collapsed = Editor.Document.CollapsedGroups.Where(groups.Contains).Order().ToArray();
        if (collapsed.Length == 0)
        {
            layout.Remove("collapsed");
        }
        else
        {
            layout["collapsed"] = new JsonArray(collapsed.Select(static id => (JsonNode)JsonValue.Create(id.ToString())!).ToArray());
        }
    }

    static void AddPasteGroups(GraphDocument source, Dictionary<ElementId, ElementId> identities,
        List<IGraphCommand> steps)
    {
        foreach (var group in source.ChildGroups(null))
        {
            AddPasteGroup(source, group, identities, steps);
        }
    }

    static void AddPasteGroup(GraphDocument source, GraphGroup group, Dictionary<ElementId, ElementId> identities,
        List<IGraphCommand> steps)
    {
        steps.Add(new AddGroupCommand(group.Title, group.Parent is { } parent ? identities[parent] : null,
            group.Nodes.Select(node => identities[node]).ToArray(), identities[group.Id]));
        if (source.CollapsedGroups.Contains(group.Id))
        {
            steps.Add(new SetGroupCollapsedCommand(identities[group.Id], true));
        }
        foreach (var child in source.ChildGroups(group.Id))
        {
            AddPasteGroup(source, child, identities, steps);
        }
    }
}
