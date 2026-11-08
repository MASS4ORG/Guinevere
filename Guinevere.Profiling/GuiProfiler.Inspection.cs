namespace Guinevere.Profiling;

public sealed partial class GuiProfiler
{
    void CollectDetails(Gui gui)
    {
        _nodes.Clear();
        if (gui.RootNode is { } root)
        {
            _nodes.Push(root);
        }
        while (_nodes.TryPop(out var node))
        {
            CollectNode(node);
            for (var i = node.ChildNodes.Count - 1; i >= 0; i--)
            {
                _nodes.Push(node.ChildNodes[i]);
            }
        }
        gui.InspectState(this);
        CollectInput(gui);
        Inspect("memory", "", "memory", "measurement",
            "Thread frame deltas include inline instrumentation; observer-internal bytes are a partial attribution. " +
            "Publication, process/native/GPU totals require separate measurements.");
    }

    void CollectInput(Gui gui)
    {
        Inspect("input", "", "input", "focus", gui.Focus.CurrentFocusedId ?? "none");
        Inspect("input", "", "input", "capture", gui.PointerCapture ?? "none");
        Inspect("input", "", "input", "pointer-mode", gui.PointerMode.ToString());
        if (gui.Platform.Supports<IInputHandler>())
        {
            Inspect("input", "", "input", "pointer", gui.Input.MousePosition.ToString());
            foreach (var key in Enum.GetValues<KeyboardKey>().Where(static key => (int)key >= 0))
            {
                if (gui.Input.IsKeyDown(key))
                {
                    Inspect("input", "", "input", "key-held", key.ToString());
                }
            }
        }
    }

    void CollectNode(LayoutNode node)
    {
        var id = Number(node.Identity);
        var parent = node.Parent is { } parentNode ? Number(parentNode.Identity) : "";
        Inspect(id, parent, "layout", node.StyleTarget?.Type ?? "node", node.Id, node.Rect);
        Inspect(id, parent, "constraints", "width", node.Style.Width.ToString());
        Inspect(id, parent, "constraints", "height", node.Style.Height.ToString());
        Inspect(id, parent, "interactables", "pointer", node.IsPointerInteractive ? "interactive" : "passive", node.Rect);
        Inspect(id, parent, "constraints", "padding", FormattableString.Invariant(
            $"{node.Style.PaddingLeft},{node.Style.PaddingTop},{node.Style.PaddingRight},{node.Style.PaddingBottom}"));
        var (file, line) = TargetGui.NodeSource(node);
        if (line > 0)
        {
            Inspect(id, parent, "source", "location", $"{file}:{line}");
        }
        node.DrawList.Inspect(this, id);
    }

    /// <inheritdoc />
    public void Text(LayoutNode node, TextLayout layout)
    {
        if (!CaptureDetails)
        {
            return;
        }
        var id = Number(node.Identity);
        Inspect(id, "", "text", "content", layout.Text);
        var index = 0;
        foreach (var run in layout.Runs)
        {
            var name = $"run[{index++}]";
            Inspect(id, "", "text", name, string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{run.Start}..{run.End} '{run.Text}' font={run.Font.FamilyName} x={run.X} width={run.Width} baseline={run.Baseline}"));
            if (run.Glyphs is { } glyphs)
            {
                for (var i = 0; i < glyphs.Ids.Length; i++)
                {
                    Inspect(id, "", "text", $"{name}.glyph[{i}]",
                        FormattableString.Invariant($"id={glyphs.Ids[i]} x={glyphs.X[i]} y={glyphs.Y?[i] ?? 0}"));
                }
            }
        }
    }
}
