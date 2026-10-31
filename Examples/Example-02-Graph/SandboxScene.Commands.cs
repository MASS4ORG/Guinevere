namespace Example02;

public sealed partial class SandboxScene
{
    /// <summary>Commands shared by the application's main menu and context actions.</summary>
    public IReadOnlyList<DemoCommand> Commands(Gui gui) =>
    [
        new("File", "Copy JSON", "save-json", () => WithClipboard(gui, c => c.SetClipboardText(View.Serialize()))),
        new("File", "Load JSON", "load-json", () => WithClipboard(gui, c => Load(c.GetClipboardText()))),
        new("Edit", "Add node", "add-node", () => _paletteOpen = !_paletteOpen),
        new("Edit", "Undo", "undo", () => View.Undo()),
        new("Edit", "Redo", "redo", () => View.Redo()),
        new("Edit", "Copy", "copy-selection", () => WithClipboard(gui, c => c.SetClipboardText(View.CopySelection()))),
        new("Edit", "Cut", "cut-selection", () => WithClipboard(gui, c => View.CutSelection(c.SetClipboardText))),
        new("Edit", "Paste", "paste-selection", () => WithClipboard(gui, c => View.Paste(c.GetClipboardText(), new(16, 16)))),
        new("Edit", "Duplicate", "duplicate-selection", () => View.DuplicateSelection(new(16, 16))),
        new("Edit", "Delete", "delete-selection", () => View.DeleteSelection()),
        new("Layout", "Frame all", "frame-all", Canvas.FitAll),
        new("Layout", "Reset layout", "reset-layout", ResetLayout),
        new("Layout", "Align left", "align-left", () => View.AlignSelection(GraphAlignment.Left)),
        new("Layout", "Align top", "align-top", () => View.AlignSelection(GraphAlignment.Top)),
        new("Layout", "Distribute X", "distribute-x", () => View.DistributeSelection(true)),
        new("Layout", "Distribute Y", "distribute-y", () => View.DistributeSelection(false)),
        new("Layout", "Snap to grid", "snap-grid", View.SnapSelection)
    ];
}
