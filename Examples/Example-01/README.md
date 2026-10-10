# Example-01 (Feature Tour)

A tour of Guinevere's core layout, text, motion, interaction and styling systems, one demo per page. It deliberately does not use Excalibur: every control is a plain node, drawn from a `ColorPalette`. See Example-50 for the Excalibur control gallery.

## Demos

### Layout
- **Alignment**: a 3x3 grid of `AlignContent` cells with live previews; the chosen cell drives a large container whose `Direction`, `Gap` and child count you can change. Alignment values animate.
- **Sizing**: drag a container's edge to see `WidthPercent`, `ExpandWidth`, `MaxWidth`, fixed/expanding rows and `Wrap` react.
- **Responsive**: mobile, tablet and desktop arrangements chosen from the width the stage has.

### Text
- **Typography**: size and color of a live specimen, a type ramp, Unicode and font fallback.
- **Wrapping**: drag the handle to change `wrapWidth` of three blocks.
- **Effects**: `TextEffects` (outline, drop and inner shadow, linear, radial and animated gradients).
- **Scrolling**: two independent `ScrollY` columns.

### Motion
- **Easing & Blend**: `UnitValue.Lerp` between pixels and a percentage driven by `AnimateFloat`; six easing curves racing at a chosen duration.
- **Springs**: `Smoothdamp`, `AnimateSpring`, `AnimateFloat`, color and pointer followers, a reduce-motion switch, and cards using `SetTransform` that grow on hover and spin on click.

### Interaction
- **Drag & Drop**: a three-column board with typed payloads, previews, accepted and rejected targets, and keyboard dragging.
- **Events & Cursors**: bubbling clicks, scrubbing with `PointerMode.Wrapped`, orbiting with `PointerMode.Relative`, cursor shapes and a live event log.
- **Headless Controls**: one `Pressable`/`Toggleable` behavior drawn by three skins, with the live `ControlVisualState`.

### Style
- **Stylesheets**: `.pss` selectors, `#inherit`, variables, `:hover`/`:active`/`:selected` transitions and the generated stylesheet source.

## Controls
- **PageUp / PageDown**: previous / next demo.
- **Mouse**: hover, click, drag and scroll as each demo describes.
- **Escape**: clears the event log or cancels a drag.
- **Space / Enter**: drag and drop, and activate focused controls.

## Running
```bash
dotnet run --project Examples/Example-01-OpenGL-SilkNet/
```

`Example-01` is a library; the `Example-01-*` projects host it on each integration.
