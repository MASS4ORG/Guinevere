namespace Example02;

public sealed partial class FsmScene
{
    /// <summary>Commands for the application menus, sharing the live State editor and clipboard.</summary>
    public IReadOnlyList<DemoCommand> Commands(Gui gui) =>
    [
        new("File", "Copy JSON", "fsm-save", () => WithClipboard(gui, c => c.SetClipboardText(View.Serialize()))),
        new("File", "Load JSON", "fsm-load", () => WithClipboard(gui, c => Load(c.GetClipboardText()))),
        new("File", "Rename signal + reload", "fsm-rename", () => Load(View.Serialize(), renamed: true)),
        new("Edit", "Undo", "fsm-undo", () => View.Undo()),
        new("Edit", "Redo", "fsm-redo", () => View.Redo()),
        new("Edit", "Add state", "fsm-add-state", AddState),
        new("Edit", "Add transition", "fsm-add-transition", AddTransition),
        new("Edit", "Assign trigger", "fsm-assign", () => AssignTrigger()),
        new("Edit", "Clear trigger", "fsm-clear-trigger", () =>
        {
            if (InspectedTransition() is { } transition)
            {
                Domain.SetTrigger(View, transition.Id, null);
            }
        }),
        new("Layout", "Frame all", "fsm-frame-all", View.Canvas.FitAll)
    ];
}
