# Platform capabilities

Guinevere integrations publish host services through `gui.Platform`. Required capabilities use
`Require<T>()`; optional features use `Supports<T>()` or `TryGet<T>()`. `Require<T>()` throws
`PlatformCapabilityException` with the missing contract, so unsupported behavior is explicit.

## Contracts

| Contract | Purpose | Required |
| --- | --- | --- |
| `IInputHandler` | Pointer, keyboard and typed text | Yes |
| `IClipboard` | Text clipboard | Yes |
| `IWindowHandler` | Native window operations | Yes |
| `ITimeCapability` | Monotonic frame timing | Yes |
| `ICanvasRenderer` | Canvas and renderer resource lifecycle | Yes for window integrations |
| `IDisplayCapability` | Logical size, framebuffer size and DPI scale | Desktop integrations |
| `ICursorCapability` | Native pointer cursor | Optional |
| `IPlatformFileDialogCapability` | Native open/save/folder picker | Optional |
| `ITextureCapability` | Platform-owned textures | Optional |
| `IGpuEffectsCapability` | Named accelerated effects | Optional |
| `IAccessibilityCapability` | Semantic control frame bridge | Optional |

The embedded Excalibur file browser remains available without a native file-dialog capability.
Rendering continues through Skia when texture or GPU-effect capabilities are absent.

## Lifecycle and ownership

Create capabilities on the window/render thread and register them before the first frame. The
integration owns native handles and disposes them after the final `Gui.EndFrame`. Re-registering a
contract replaces it without disposing the previous service; the host owns both instances. Remove a
service during shutdown only after controls can no longer use it.

Input snapshots, display values and renderer operations belong to the render thread. Asynchronous
file dialogs may complete elsewhere, but their result must be consumed on a later frame. Accessibility
adapters receive semantic data during rendering and should publish the completed tree after the frame.

## Implementing an integration

1. Implement input, clipboard and window contracts on the host window.
2. Assign `gui.Input` and `gui.WindowHandler`; these compatibility properties publish their contracts.
3. Register the renderer and display service explicitly with `gui.Platform.Register<T>()`.
4. Register only optional capabilities the backend actually supports.
5. Run `PlatformConformance.Validate(gui.Platform)` after initialization and fail startup if it returns errors.
6. Dispose native resources in reverse ownership order after the GUI loop ends.

The OpenGL Silk.NET, OpenGL OpenTK, OpenGL Raylib and Vulkan Silk.NET integrations publish input,
clipboard, window, time, renderer and display capabilities. Optional cursor, native file-dialog,
external texture, GPU-effect and accessibility adapters are discoverable contracts with explicit
fallbacks; integrations do not claim them until they provide native implementations.
