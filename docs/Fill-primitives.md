# Fill primitives and game UI composition

Guinevere core provides fills, image presentation, targeting geometry, text presentation and keyed state helpers. Bars, cooldowns, health/shield displays and damage trails compose these pieces; the host owns gameplay values, assets and visibility rules. None of these APIs requires Excalibur or layout anchors.

## Fills and layers

`DrawFill(bounds, rawValue, FillOptions)` reveals a rectangle, an elliptical sector/annulus or separated cells. `FillOptions.Maximum` and `Minimum` map host units into a fraction; `Normalizer` can replace that mapping with any raw-value-to-fraction function, including an eased cooldown curve. Mapping runs during the render pass. Non-finite mapping results render empty. Overflow clamps or repeats the mapped fraction, and thresholds select the smallest matching normalized upper bound.

```csharp
gui.DrawFill(barRect, health, new FillOptions { Maximum = maxHealth, Color = Color.Lime });
gui.DrawFill(abilityRect, elapsedSeconds, new FillOptions
{
    Kind = FillKind.Radial,
    Normalizer = seconds => MathF.Pow(1 - seconds / cooldownSeconds, 2),
    Color = new Color(0x000000b0)
});
```

Rectangular fills grow from left, right, top or bottom. Radial fills specify a degree origin (zero points right), clockwise/counterclockwise direction, sweep from zero to 360 degrees and optional fractional inner radius. Segmented fills specify cell count and pixel gaps; the last occupied cell can be partial. Empty and full values preserve silhouettes and annulus holes.

`DrawFill(bounds, value, FillLayers, ghostValue)` draws background, ghost and foreground in that order. Each layer has an independent `FillOptions` skin. Background always draws at full geometric coverage, bypassing input mapping. Ghost draws only when both its skin and value are present. Values remain caller-supplied; use `AnimatedRange` when a delayed trail is wanted.

```csharp
var values = gui.AnimatedRange(health, lagDelay: 0.3f, lagDuration: 0.6f, id: "health");
var skin = new FillOptions { Maximum = maxHealth, Sprite = barImage, SpriteBorder = new Insets(6) };
gui.DrawFill(barRect, values.Value, new FillLayers
{
    Background = skin with { Color = Color.Gray },
    Ghost = skin with { Color = Color.Yellow },
    Fill = skin with { Color = Color.Lime }
}, values.Ghost);
```

Shield is another fill with its own skin, bounds or mask. Cast progress is a fill in elapsed/total units. Pip displays use segmented geometry. Compose labels with ordinary text nodes built in both passes; the host formats health counts, duration text and localized labels.

## Image presentation and transitions

`Sprite` supplies the image skin and `Source` selects a sprite-atlas rectangle without allocating a cropped image. `SpriteBorder` supplies nine-slice borders relative to that source region. The complete sprite or nine-slice grid is drawn at the full destination size, then revealed by the fill geometry, preserving corner sizes while progress changes. Each background, ghost and fill layer can use a different image or border.

`Mask` multiplies coverage by a texture's alpha, ignoring its RGB channels. `Shape` intersects coverage with an `SKPath` expressed in destination coordinates. Sprite tint multiplies all color channels, including alpha. Cache images and paths across frames, keep them alive and unchanged until `gui.Render()` finishes, and dispose them when the host releases its resources.

Animated values and colors compose with skins: `AnimateFloat` or `Smoothdamp` can feed `DrawFill`, and `AnimateColor` can feed `FillOptions.Color`, including opacity. Sprite replacement can use a host-controlled fade. Surrounding styled nodes support stylesheet transitions and bitmap/nine-slice backgrounds; direct `FillOptions` are caller-supplied and do not resolve stylesheet properties automatically.

## Keyed state and text presentation

`AnimatedRange(target, duration, lagDelay, lagDuration, easing, id)` returns raw foreground and ghost values. Foreground duration defaults to zero. A decrease holds the ghost for the configured delay, then catches up using Guinevere's shared tween engine. Repeated decreases restart the hold from the current trail. An increase resets the ghost to the new target, and the returned ghost never falls below the foreground. Zero catch-up duration snaps after the hold; `ReduceMotion` snaps both values immediately.

Call the helper from the same place in both GUI passes. It uses the same node/item/data identity as `GetData`, samples once per frame, requests a timer for the hold and requests frames while interpolating. Distinct IDs or `gui.ItemKey(entityId)` isolate entities, and `ClearData` clears helper state. A previously queued host timer may still cause one wake after a trail is reset. Hidden items retain their state and advance to the appropriate current value when shown again.

Text presentation is an explicit composition category: labels, duration text and damage numbers use Guinevere's existing font roles, text layout, text effects and transforms. The game supplies formatting, localization, damage events and lifetimes. Keyed animation helpers can supply movement, opacity and size without placing gameplay state in the drawing primitive.

## Targeting geometry and larger recipes

`DrawReticle` supplies four arms with configurable spread, length, thickness and optional center dot. `DrawDirection` supplies a triangular warning at a radius and degree angle. Custom shapes and sprites compose through `DrawShape` and `DrawImage`.

A radial menu needs reusable sector geometry together with pointer hit testing and directional selection with a dead zone. Concentric rings should be ready-made combinables, with a compact complete recipe demonstrating their use. Radial fill drawing alone does not supply selection behavior. A minimap similarly composes clipping, an image transform around a point and marker placement; the host supplies map data and world positions. Inventory/hotbar, dialogue and world-marker recipes belong to the separate roadmap work in #101, #102 and #104.

## Moving overlays and world panels

Fills take explicit rectangles and can be placed anywhere in the canvas, independently of flow layout. For a screen overlay, Turian supplies a per-frame projection result containing position, scale and visibility for each entity. Guinevere draws at the resulting 2D position, with `AbsoluteScreen` when a containing text/layout node is useful. Positions must use the active viewport's logical coordinates when UI scaling is enabled.

```csharp
using var item = gui.ItemKey(entityId);
var projected = host.Project(entityId);
if (projected.Visible)
{
    var size = new Vector2(80, 8) * projected.Scale;
    var bounds = new Rect(projected.Position.X - size.X / 2, projected.Position.Y - size.Y, size.X, size.Y);
    gui.DrawFill(bounds, health, new FillOptions { Maximum = maxHealth, Sprite = healthImage });
}
```

Projection and visibility policies remain swappable: Turian handles camera projection, occlusion queries, distance scaling, timed visibility after damage and any game-specific prioritization. Screen-edge clamping and marker overlap policies can consume the projected result without depending on engine camera or scene types. Separate camera viewports need independent coordinate and visibility contexts.

World panels render Guinevere into textures that Turian places on world geometry. The host chooses billboard orientation, filtering and depth testing. Avoid a render target per enemy: design #104 around shared atlas textures with batched/instanced quads, or engine-rendered quads that reuse Guinevere layout and skin metadata. Texture resolution limits sharpness, and update scheduling should follow dirty or animating panels. Screen overlays can share one UI surface and are usually appropriate for nameplates and markers; world panels suit diegetic displays where scene placement and depth matter.

Open **Game UI → HUD** in Example-01 to exercise raw health values, nine-slice skins, held ghost trails, eased cooldown mapping, segmented cast progress, texture masks, reticles and duration labels.
