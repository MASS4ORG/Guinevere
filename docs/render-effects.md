# Render effects

`RenderEffectStack` captures drawing and descendants on a node's z-layer, applies filters in order, then composites the result. Create and reuse a stack across frames; dispose it after the final frame. Skia manages the temporary layer storage. The stack caches native filters and paint until its parameters or runtime compilation support change.

```csharp
using var effects = new RenderEffectStack(
    new RenderEffect.Blur(2),
    new RenderEffect.Saturation(1.5f));

// Inside the frame callback, in both passes:
using (gui.Node(300, 200).Effects(effects).Enter())
{
    gui.DrawBackgroundRect(Color.Blue);
    gui.DrawText("Filtered panel");
}
```

`using (gui.RenderEffects(effects))` creates a layout group for a drawing scope. `gui.SetRenderEffects(effects)` applies a stack to the current node. Nested effect stacks and opacity groups composite in tree order. Filters use screen coordinates, preserve ancestor clips and may extend past the source node. Layers use the enclosing clip as a conservative bound so nested filter outsets are retained. Popups on another z-layer render independently of the captured layer.

Built-ins are `Blur`, `Glow`, `DropShadow`, `Saturation`, `Brightness`, `Contrast`, `Tint`, `HueRotation`, `Grayscale`, `Invert`, `Pixelate`, `ChromaticAberration` and `Sharpen`. Blur radii are Gaussian standard deviations in pixels. Tint, grayscale and inversion amounts range from zero to one. Chromatic aberration separates red and blue while preserving the source alpha mask. Direct shape and text shadows remain available for cheaper effects on individual primitives.

Animate parameters by passing sampled values to `effects.Update([new RenderEffect.Blur(radius)])`, where `radius` can come from `gui.AnimateFloat`. Equal sequences retain their cached native resources. Changing parameters rebuilds the filter graph on its next use.

PSS accepts ordered `filter` functions, for example `filter = blur(2px) saturate(150%);`. Additional functions are `glow(radius,#color)`, `drop-shadow(x,y,radius,#color)`, `brightness(amount)`, `contrast(amount)`, `tint(#color,amount)`, `hue-rotate(degrees)`, `grayscale(amount)`, `invert(amount)`, `pixelate(size)`, `chromatic-aberration(x,y)` and `sharpen(amount)`. `none` clears local filters. Parameters support style tokens; numeric APIs support animation. The Render Effects page in Example-01 shows every built-in on a panel containing text, shapes and an image.

`RuntimeColorFilter` accepts SkSL with a `half4 main(half4 color)` entry point and no unbound uniforms or child effects. `CompiledColorFilter` accepts a caller-owned `SKColorFilter`, including one compiled and bound by the host. Keep that native filter alive until the stack is disposed. These contracts process one pixel at a time; custom effects that sample neighboring pixels require a separate shader/render-target implementation.

Desktop integrations publish `IGpuEffectsCapability.SupportsRenderTargets` and `SupportsRuntimeShaderCompilation`. Runtime source compiles only when the integration permits it. Raster hosts can register `SkiaEffectsCapability.Instance` explicitly to permit runtime compilation. Missing runtime support and compilation failures skip that operation and appear in `effects.Diagnostics`; explicitly unsupported render targets skip the stack and report the fallback. Precompiled color filters require no runtime compilation permission.
