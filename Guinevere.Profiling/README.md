# MASS4.Guinevere.Profiling

Collect GUI frames without viewer assemblies, or attach an optional host gate for pause, single-step and input playback.

```csharp
using Guinevere.Profiling;
using MASS4.Profiling;

using var session = new ProfileSession();
using var profiler = new GuiProfiler(gui, session, "Application") { Detailed = true };
using var controller = new GuiFrameController(gui);
// Existing desktop integrations consult the controller before callbacks and time updates.
window.RunGui(DrawApplication);
```

Collection works with the ordinary BeginFrame/CalculateLayout/SetStage/Render/EndFrame lifecycle. Custom hosts use `gui.UpdateFrameTime(delta)` and `gui.RenderFrame(canvas, draw)` to enable host control. A custom clock needs its own controller; the default controller rejects it. Target execution and viewer operations belong to the host UI thread. The shared core independently supports concurrent producer tracks.

`controller.Paused = true` freezes callbacks, target time and input delivery. `controller.Step(delta)` requests exactly one complete frame. Native presentation snapshots are bounded by `MaximumSurfacePixels`; frozen presentation reuses copied pixels without executing custom callbacks. Targets render on the host's actual canvas/backend; snapshot/readback and presentation costs occur outside measured target scopes. The canvas must be surface-backed. Dispose the controller and collector before unloading application code or destroying its host. Held captures remain owned values.

Set `Recording` to collect delivered input frames, including the actual target delta. `GetRecording()` returns their immutable snapshot. `Play(InputRecording)` preserves simultaneous actions and frame boundaries; `Play(InputScript)` retains legacy 60 Hz expansion and post-frame checks/dumps without changing InputScript's JSON or equality. Inspect ScriptFailures/PlaybackMessage for failed assertions. Native event order is retained where the host exposes it; Raylib supplies polling boundaries. Controller/touch snapshots retain supported device data. Dropped input frames/events are reported. Queued paused input is delivered one boundary per step; input arriving during playback is isolated from it.

Looping requires an application-owned ResetTarget callback. Restore all relevant state, clock assumptions, focus, checkpoints and seeds to claim deterministic repetition; playback alone cannot rewind the application. TimeScale multiplies delivered virtual deltas. Reproduce a recording at its original viewport/DPI; metadata records both but the controller does not resize a native window or rewrite application state.

Summary mode collects CPU phase scopes, work requests, nodes, canvas submissions and thread allocation deltas. Detailed mode copies geometry, applied PSS values/provenance, text/glyph/fallback data, ordered draw descriptors and safe state/input descriptors. Arbitrary state/native objects stay opaque. Frame allocation totals include inline instrumentation; observer-internal allocation is a partial attribution. GPU time, native memory and process-wide allocations remain unavailable.

Development before the first shared-core release uses `-p:ProfilingLocalPath=/mass4/dev/libs/Profiling`. Published builds use the exact `MASS4.Profiling` 0.1.0 pin; NoLocalPackages clears the override and local-source pack guards remain active.
