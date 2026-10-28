# Graph Playground

The graph-only interaction showcase. Run `dotnet run --project Examples/Example-02-Graph`. Example 93 remains the separate 10,000-tile scale benchmark.

The Sandbox is a real arithmetic dataflow document using published `MASS4.Graph` and `MASS4.Graph.Dataflow` 1.0.0 packages. Number, Add, Multiply and Result types are registered through the actual registry API. The initial graph connects 2 and 3 to Multiply, then to Result; its executable behavior is covered by the headless dataflow test. The GUI reads no payload types; a domain-owned title adapter formats constant values.

Drag between port circles, or click one port and then another, to create a validated connection. Occupied single-connection ports replace their existing link atomically; reconnecting the same endpoints retains the existing edge without an alert or history step. Enable **Avoid replacing connections** to retain the core's duplicate/occupied-port diagnostics. Failed replacements restore the previous connections. Add node opens a searchable registry palette; Ctrl+K/Insert also opens it.

Delete/Backspace removes the focused node or connection. On a port circle, it disconnects all attached links as one undoable step; it never removes the port itself. Ctrl+Z/Ctrl+Y and the toolbar undo/redo document edits, node moves and layout reset in one chronological history through the real `GraphEditor`. A group drag is one step, regardless of its number of frames.

Click a node to replace selection. Ctrl+click toggles membership; dragging an already-selected node moves the group, while a completed plain click reduces it to that node. Arrows navigate and Shift+arrows move the selection. Reset layout restores each existing node's initial position, including subsequently added nodes; it never recreates or renames nodes, disconnects edges, changes payloads, or clears earlier history. Undoable layout commands update only the view dictionary, so they do not alter document layout metadata, emit document changes or invalidate evaluation. The **Minimap** switch shows or hides the minimap independently of history and camera state.

The playground uses Example 01's shared `DemoHeader.cs` and embedded badge. It opens directly to the Sandbox; `GraphPlayground.ShowScenarioTabs` enables the scenario shell. Those tabs support Tab/Shift+Tab, Enter/Space, Left/Right, Home/End, focus indicators, and core accessibility semantics. The playground has no Excalibur, engine, or parallel graph-model dependency.

Each graph issue extends its scenario as part of acceptance:

- **Sandbox:** basic #84 node/port/connector rendering and #85 registry creation, linking, deletion and semantic undo/redo are implemented against the published contracts. Rich domain editors and additional editing gestures extend this scenario.
- **Running flow:** #84 supplies a flow overlay consuming real `ICompiledGraph.Observe` observations. Evaluation stays async-shaped and never blocks the UI. Animation is an overlay type, not a special rendering path; no simulated flow is displayed by the shell.
- **Organizers:** Graph #12 and Guinevere #86 supply groups, notes, and collapsed containers, keeping view state separate from document semantics.
- **Domain scenarios:** #88 and #224 add the shader adapter and FSM editor using real domain contracts.
- **Excalibur Controls:** a later, deliberate optional-styling mirror. The graph-only scenarios remain consumable without Excalibur.

Running flow and Organizers show their pending state when the scenario shell is enabled. A native accessibility bridge receives scenario controls and option-switch states through Guinevere core semantics and the sandbox catalog through `IGraphSemanticsSink`.
