# Guinevere.Styling

Runtime `.pss` (PanGui Style Sheet) theming for Guinevere. Styles are optional: the core library draws and lays out without them, so add this package only where sheets drive the look.

```csharp
var theme = StyleSheetSource.FromFile("theme.pss");
gui.AddStyleSheet(theme);
gui.StyleSheets.SetToken("accent", "#ff8800"); // host override above every sheet

using (gui.StyledNode("button", classes: ["primary"]).Enter())
    gui.DrawText("Save");

var style = gui.ResolveStyle("tooltip"); // read declarations for a self-drawn control
```

```css
@const theme-name = "Night";
$base = #2e3440;
$accent = #88c0d0;
$contrast = 0.3;
$surface = shade($base, 1.1, 0.9);   // Godot-style elevation ladder

button {
    padding = 0.5em 1em;
    bg-color = $surface;
    color = contrast-ink($surface);
    :hover(0.15 ease-out) { bg-color = mix($surface, $accent, 25%); }
}
```

`gui.StyleSheets.GetTokenColor("surface")` returns the evaluated token, so host code can use the same derived colors.

The package covers:

- Type, class, id, descendant and `>` selectors.
- Nesting, `#inherit` and custom modifiers.
- `$tokens` layered across sheets, and `@const` values the host can read.
- Tokens inherited down the node tree like CSS custom properties: a rule's `$token = value;` and `gui.SetStyleToken(name, value)` theme a node's whole subtree.
- Value expressions: arithmetic, `calc()`, `em`, `min`/`max`/`clamp`, `rgb()`/`rgba()` (every channel 0..255), `rgb1()`/`rgba1()` (every channel 0..1), `hsl()`, `mix`, `alpha`, `lighten`, `darken`, `contrast-ink` and `shade`.
- Visual properties on `StyledNode`: `background` colors and `linear-gradient`s, `box-shadow` (outer and inset), borders drawn inside the box (per side with `border-<side>-width`/`-color`), per-corner `border-radius`, `outline`, `opacity`, `cursor`, and text `color`/`font-*`/`text-shadow` inherited by child text.
- Icon themes: `icon#scene\.move { glyph = "\f0b2"; font-family = "fa6-solid"; }` or `{ src = url("move.png"); tint = true; }`, drawn by `gui.StyledIcon("scene.move", 16)`.
- `@font-face`, `url()` and `@import` through a host resolver.
- `file:line:col` errors, and reloads that keep the last valid sheet.
- Inspection: `gui.StyleSheets.MatchedRules(target)` lists the rules an element uses, and `StyleRule.Lines` points at their source.
- A resolved-style cache whose hits allocate nothing.

## Text shadows and glow

`text-shadow = x y [blur] [color], ...;` uses pixel lengths, including unitless pixels. Blur is a nonnegative CSS radius (twice the core Gaussian sigma), and omitted colors use the declaration's resolved text color. The first listed shadow paints above subsequent shadows, behind the glyph fill. Malformed declarations are ignored as a whole; percentages, spread and `inset` are unsupported.

Shadows inherit through node scopes into ordinary `DrawText`, glyphs and Excalibur controls. A local declaration replaces the inherited shadow list, and `text-shadow = none;` clears it for the subtree. An explicit `DrawText(effects: ...)` replaces all inherited effects for that draw; `gui.SetTextEffects(...)` provides the same scope mechanism to core callers. Shadows affect visual ink bounds and opacity layers while text measurement stays based on font metrics. Explicit text clips and ancestor clips apply to the effects too. Sheet reloads and host or subtree token changes refresh the resolved shadows; shadow lists change immediately rather than interpolating through transitions.

```css
$phosphor = #66ff88;
terminal {
    bg-color = #061009;
    color = $phosphor;
    font-family = monospace;
    text-shadow = 0 0 4px $phosphor, 0 0 12px #33ff6680;
}
terminal .crisp { text-shadow = none; }
```

Example 50's styling page demonstrates this Terminal Green glow on a label and button. `ResolvedStyle.GetTextShadows(defaultColor)` exposes the typed list for custom controls. PanGui's [published effects vocabulary](https://www.pangui.io/blog/03-immediate-mode-styling/#effects) includes `innerShadow` on shapes, but does not specify its text binding. Inner text effect syntax is deferred; shape effect blocks remain available in `StyleSheet.Deferred`, and core callers can use `TextEffects.InnerShadow` directly.

## Font registration and roles

Register faces through `gui.Fonts`, or declare them in an active sheet. Numeric weights and italic faces are selected before synthetic bold or italic is applied. Later sheets replace matching family/weight/style descriptors; removing a sheet restores earlier faces. Missing files and unsupported remote sources leave other fallback families usable.

```css
@font-face { font-family = "Brand"; src = url("fonts/Brand-Regular.ttf"); font-weight = 400; }
@font-face { font-family = "Brand"; src = url("fonts/Brand-Bold.ttf"); font-weight = 700; }
$font-ui = "Brand", "Noto Emoji";
$font-ui-mono = monospace;
$font-code = $font-ui-mono;
$font-icon = "fa6-solid";
$font-emoji = "Noto Emoji";
label { font-family = $font-ui; }
```

```csharp
gui.Fonts.RegisterFile("Brand", "fonts/Brand-Regular.ttf");
gui.Fonts.RegisterStream("Symbols", fontStream);
gui.Fonts.SetRole(FontRole.Ui, "Brand, Noto Emoji");

gui.BeginFrame(canvas);
gui.UseFontRole(FontRole.Ui); // reads $font-ui, including host and subtree token overrides
using (gui.StyledNode("label").Enter())
    gui.DrawText("Hello 😀");

var width = gui.MeasureTextWidth("Hello 😀", 14);
var codeFont = gui.ResolveFontRole(FontRole.Code, 14, weight: 700);
```

Call `UseFontRole` from the same call site in both passes. `font-family` lists fall back per Unicode code point, preserving surrogate pairs; `MeasureTextWidth` and `GetTextFont` share the renderer's scope font and DPI scaling. Display scaling is applied to logical text sizes and adjusted for an existing canvas scale. `ConfigureFonts` and the font-taking `BeginFrame` shortcuts register supplied faces while preserving configured defaults and per-frame overrides.

Custom controls can resolve declarations with `gui.ResolveStyle` and obtain their face with `gui.GetStyleFont(style, scope)`. This preserves inherited weight and slant when a rule omits them, and retains fallback families.

The registry owns faces loaded from files/streams and fonts returned by lookup. Dispose `gui.Fonts` when the GUI is no longer used. Fonts passed to `Register`, `ConfigureFonts` or `BeginFrame` remain caller-owned. Shaping and ligatures are outside this API.

## Bitmap backgrounds

`background-image = url("tile.png");` (also accepted on `background`) loads a raster asset relative to the declaring sheet's `BaseUri`, including imported sheets. Files load directly; set `StyleSheetOptions.ResourceResolver` to open embedded resources or custom URI schemes. The resolver receives the resolved URI, and the returned stream is disposed after decoding. Unsupported or missing assets leave the solid background visible and appear in `gui.BackgroundDiagnostics` with the winning declaration's source, line and column.

The following vocabulary is provisional Guinevere syntax: PanGui's [published styling overview](https://www.pangui.io/blog/03-immediate-mode-styling/) establishes the `=` declaration and resource conventions but does not specify bitmap background properties.

- `background-repeat`: `repeat` (default), `repeat-x`, `repeat-y`, `no-repeat`.
- `background-size`: `auto` (default, one bitmap pixel per logical unit), `contain`, `cover`, or one/two positive pixel or percentage lengths with `auto` allowed per axis. One explicit length sets width and preserves aspect ratio; percentages refer to the box dimensions.
- `background-position`: one/two pixel or percentage lengths or `left`, `center`, `right`, `top`, `bottom` (default `0% 0%`). One value centers the other axis. Percentages position within the remaining space after sizing; negative offsets are allowed.

Images paint above the solid fill and below inset shadows and borders, clipped to the rounded border box. Transparent pixels reveal the fill. Node and part opacity, ancestor clips and canvas transforms apply normally. Dimensions are logical units; display/viewport scaling uses the same canvas transform as other geometry. Image layers, `space`/`round` repetition, four-value edge offsets, atlas regions and nine-slice background syntax are unsupported and produce diagnostics when a bitmap background is resolved.

Decoded images (including failed loads) are cached per GUI by URI and resolver, and resolved bitmap settings are cached with their styles. Sheet reloads/replacements and host token changes dispose and invalidate that cache; file changes alone do not reload it. Call `gui.ClearBackgroundImages()` between frames to refresh changed assets or release them at GUI shutdown. Native image wrappers are reclaimed with the GUI if no explicit cleanup is performed. Diagnostics are deduplicated until invalidation. Resolve/render a background before reading its diagnostics.

```css
panel {
    background-color = #203040;
    background-image = url("surface.png");
    background-repeat = no-repeat;
    background-size = cover;
    background-position = center;
    border-radius = 8;
}
```

Example 50's Styling page includes embedded bitmap Win95 patterns, Luna and Aqua skins, and transparent CRT scanlines, selected entirely by stylesheet declarations.
