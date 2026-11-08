# MASS4.Guinevere.DevTools

A separate GUI for live sources and immutable exported captures. Its clock, input, stylesheets and frame work are independent of the target application.

```csharp
using Guinevere.DevTools;
using MASS4.Profiling;

using var session = new ProfileSession();
using var viewer = new ProfilerView(session);
using var target = viewer.Attach(applicationGui, "Application");
target.Profiler.Detailed = true;
// Give viewer.Gui its own host/input route and call viewer.Draw in both passes.
// Keep running the application's existing draw callback through its normal host.
```

The source/history list chooses a producer and a completed frame. Live follows new captures; selecting history or importing a capture pins owned values rather than inspecting current nodes. Timeline tracks show measured CPU scopes/counters. Layout selection drives copied bounds and property inspectors; text includes runs, glyph IDs and resolved fallback faces. Unsupported state/resources are explicitly opaque. Preview is the current frozen target image, not a historical screenshot. Detach targets before application-code unload. Viewer and target presentation operate on the host UI thread; shared capture collection/export is independent of the viewer.

Panels dock, split, float and persist through `viewer.Layout.ToJson()` and `viewer.RestoreLayout(json)`. F6 selects the next panel, F8 pauses/resumes, F10 steps. Tab/Enter navigate and activate controls; arrow keys navigate virtualized trees. Toolbar docking actions provide a keyboard alternative to dragging. Logical source/frame/object roles and actions use the existing IAccessibilityTreeSink/IAccessibilityActionSource contracts, including offscreen items. Native screen-reader qualification is separate from publishing semantics.

`ProfilerSkins.Light` and `.Dark` are PSS stylesheets. `viewer.SetSkin(consumerSheet)` replaces them without code palettes or target-style changes. All viewer selectors, control appearance and focus indicators remain styleable. `viewer.Import(stream)` and `viewer.Export(stream)` use the shared capture schema. Toolbar and input-panel file buttons save/open captures and input recordings or legacy scripts. An optional host-supplied `FileDialogState` customizes the file browser; cancellation preserves the selection, and invalid files produce a viewer diagnostic.

Run `dotnet run --project Examples/Example-03-Profiler -c Release` for the sample. Before publishing the shared core, supply `-p:ProfilingLocalPath=/mass4/dev/libs/Profiling` or the equivalent ignored local override. The sample can select a native integration with `-p:ProfilerBackend=OpenGL.OpenTK`, `OpenGL.Raylib` or `Vulkan.SilkNET`; the default is `OpenGL.SilkNET`. `--headless preview.png` exercises the tool offscreen. Native qualification uses a real window and remains distinct from that headless mode.
