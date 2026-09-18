# Example 60 — Docking

An editor-style shell built on `gui.DockSpace`: five panels arranged into a `DockLayout` of nested
splits and tab groups.

```sh
dotnet run --project Examples/Example-60-Docking/
```

## What to try

| Action | Result |
|---|---|
| Drag a tab onto another group's **edge** | splits that region and docks the panel beside it |
| Drag a tab onto another group's **centre** | joins that group's tabs |
| Drag a tab along its own **tab strip** | reorders it within the group |
| Drag a tab onto anything else | tears it off into a floating window |
| Drag a floating window's title strip | moves the window |
| Drag a **splitter** | resizes the two regions it divides |
| Click a tab's **✕** | closes the panel; *Reopen …* appears in the toolbar |
| **Save layout** / **Load layout** | round-trips the arrangement through `DockLayout.ToJson()` |

## What it demonstrates

- `DockLayout` — the model: `DockSplit` / `DockLeaf` trees, floating windows, and versioned JSON.
- `gui.DockSpace(...)` — the renderer; every edit is applied on pointer release, never mid-frame.
- `gui.Splitter(ref fraction, axis)` — the divider control, usable on its own.
- The drag-and-drop primitives underneath: `gui.DragSource`, `gui.DropTarget`, `gui.DragGhost`.
- `LayoutNode.AbsoluteScreen(...)` and `LayoutNode.BlockInput()` — how floating windows sit above the
  dock and keep the panels underneath from reacting to the pointer.
