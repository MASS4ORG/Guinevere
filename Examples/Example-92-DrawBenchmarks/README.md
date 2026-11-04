# Drawing benchmarks

Build in Release and run the compiled executable to avoid including build work in measurements:

```powershell
dotnet build Examples/Example-92-DrawBenchmarks -c Release -p:NoLocalPackages=true -m:4
dotnet Examples/Example-92-DrawBenchmarks/bin/Release/net10.0/Example-92-DrawBenchmarks.dll --samples --backend raster
```

`--backend` accepts `raster`, `opengl`, or `vulkan`. GPU setup uses the production integrations and rejects CPU fallback. Sample mode warms each workload for 100 frames, then emits nine CSV samples of 200 frames with managed allocation counts and a SHA-256 hash of one rendered frame. `--quick` uses three samples of 30 frames; `--scenario chart-lines` selects one workload. Run comparisons sequentially on the same machine and alternate build order across repeated runs.

GPU sample mode renders to an offscreen surface on the production renderer's context and synchronizes submission and completion after each frame. Its elapsed time includes CPU work and GPU waiting; it is not a GPU timestamp measurement. `--present` instead measures the production render path, including any swapchain waits; a hidden window can still be limited by presentation. Default mode without `--samples` reports recording, replay and full-frame timings separately. `--dump <directory>` exports workload PNGs in default mode.

`PrimitiveDrawCalls` counts executed direct `SKCanvas` draw calls. Opaque mutable drawables and custom callbacks have separate submission counts because their internal draws are unknown. These diagnostics do not report driver draw calls or GPU batches. `RenderSortTime` and `RenderReplayTime` measure CPU phases and exclude integration flush and presentation; the full sample duration includes target completion.

Replay preserves command order and executes callbacks every time. Pixel-aligned axis-aligned lines on a surface use rectangle draws; other lines and recording canvases retain quadrilateral paths. Scratch paint and path resources belong to one replay and are disposed with its renderer. Caller-owned mutable shapes remain caller-owned and must stay valid until replay finishes.
