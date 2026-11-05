# Graph Playground

A showcase of node-graph editing. Six scenarios share one editor — canvas, selection, linking, clipboard, undo/redo and persistence all behave identically; only the graph domain changes.

## Scenarios

- **Sandbox** — a small arithmetic graph (2 × 3 → Result) for trying out the editor itself.
- **Running flow** — the same graph, but live: it recompiles after every change and animates the values flowing through the links.
- **State machine** — Patrol ⇄ Chase with a self-transition, running 1,000 independent instances you can inspect and step through.
- **Groups** — frames that contain, nest and collapse nodes, with links crossing the boundary as real ports.
- **Shader** — a material graph producing a preview and the generated GLSL fragment shader.
- **Compositor** — a video composition graph with a playhead, frame thumbnails and the output frame rendering live.

## Using the editor

- Drag between ports (or click two ports) to link; dragging over an occupied port replaces its link.
- Click to select, Ctrl+click to add to the selection, drag to move; Shift+arrows move the selection.
- Ctrl+K opens the searchable node palette. Delete removes the focused node or link.
- Ctrl+C/X/V/D copy, cut, paste and duplicate; Alt+drag duplicates and moves in one step.
- Ctrl+Z / Ctrl+Y undo and redo everything — edits, moves, layout — in one history.
- **Copy JSON / Load JSON** save and restore the whole document through the clipboard.
- The **View** menu controls minimap and link routing; the **Style** dropdown switches between Blueprint, Paper and High contrast themes. Zooming out progressively drops detail.
