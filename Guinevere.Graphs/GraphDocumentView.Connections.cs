using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    CommandResult ConnectPorts(ElementId source, ElementId target)
    {
        var document = Editor.Document;
        if (AvoidReplacingConnections || ReplacementEndpoints(source, target) is not { } ports)
        {
            return Accept(Editor.Connect(source, target));
        }
        if (document.GetEdges(source).Any(edge => edge.Source == source && edge.Target == target))
        {
            return Accept(new CommandResult(true, []));
        }
        var displaced = OccupiedConnections(ports.From).Concat(OccupiedConnections(ports.To)).Distinct().ToArray();
        var connection = Editor.CreateConnection(source, target);
        if (displaced.Length == 0)
        {
            return Accept(Editor.Execute(connection));
        }
        var steps = displaced.Select(static edge => (IGraphCommand)new DisconnectCommand(edge.Id)).ToList();
        steps.Add(connection);
        return Accept(Editor.Execute(new CompositeCommand("Replace connection", steps)));
    }

    (GraphPort From, GraphPort To)? ReplacementEndpoints(ElementId source, ElementId target)
    {
        var document = Editor.Document;
        if (!document.TryGetPort(source, out var from) || !document.TryGetPort(target, out var to))
        {
            return null;
        }
        return from.Direction == PortDirection.Output && to.Direction == PortDirection.Input ? (from, to) : null;
    }

    IEnumerable<GraphEdge> OccupiedConnections(GraphPort port) =>
        port.Declaration.Multiplicity == PortMultiplicity.Single ? Editor.Document.GetEdges(port.Id) : [];

    /// <summary>Disconnects every edge attached to a port as one reversible editor operation.</summary>
    public CommandResult DisconnectPort(ElementId port)
    {
        var steps = Editor.Document.GetEdges(port)
            .Select(static edge => (IGraphCommand)new DisconnectCommand(edge.Id)).ToArray();
        return steps.Length == 0 ? Accept(new CommandResult(false, []))
            : Accept(Editor.Execute(new CompositeCommand("Disconnect port", steps)));
    }
}
