namespace Example02;

/// <summary>A real nested-group example with editable titles, membership and saved collapse state.</summary>
public sealed class GroupsScene
{
    string _title = "";
    ElementId _inspected;

    /// <summary>Groups the arithmetic inputs inside a larger calculation frame.</summary>
    public GroupsScene()
    {
        var view = Sandbox.View;
        var nodes = view.Editor.Document.Nodes.ToArray();
        view.Canvas.Navigation.SetSelection(nodes.Take(3).Select(static node => node.Id.ToString()));
        var outer = view.GroupSelection("Calculation");
        view.Canvas.Navigation.SetSelection(nodes.Take(2).Select(static node => node.Id.ToString()));
        view.GroupSelection("Inputs", outer);
        view.Canvas.Navigation.ClearSelection();
        view.Editor.ClearHistory();
        view.Refresh();
        view.Canvas.Camera.SetView(new(-40, -80), 0.8);
    }

    /// <summary>The ordinary sandbox hosting groups without a separate topology model.</summary>
    public SandboxScene Sandbox { get; } = new("Nested groups", "groups-canvas");

    /// <summary>Draws the selected frame's title editor and ordinary graph tools.</summary>
    public void Draw(Gui gui, Action<string, string, Action> button)
        => Sandbox.Draw(gui, button);

    /// <summary>Draws a selected frame's title and collapse actions for any scenario.</summary>
    public void DrawInspector(Gui gui, GraphDocumentView view, Action<string, string, Action> button)
    {
        var key = view.Canvas.Navigation.FocusedKey;
        if (ElementId.TryParse(key, out var id) && view.Editor.Document.TryGetGroup(id, out var group))
        {
            if (gui.Pass == Pass.Pass1Build && _inspected != id)
            {
                _inspected = id;
                _title = group.Title;
            }
            using (gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter())
            {
                DemoControls.TextInput(gui, ref _title, "Group title", "group-title", width: 220);
                button("Rename", "rename-group", () => view.RenameGroup(id, _title));
                button(view.Editor.Document.CollapsedGroups.Contains(id) ? "Expand" : "Collapse", "collapse-group",
                    () => view.SetGroupCollapsed(id, !view.Editor.Document.CollapsedGroups.Contains(id)));
                button("Ungroup", "ungroup", () => view.Ungroup(id));
            }
        }
    }
}
