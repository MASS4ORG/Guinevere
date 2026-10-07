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
- Value expressions: arithmetic, `calc()`, `em`, `min`/`max`/`clamp`, `hsl()`, `mix`, `alpha`, `lighten`, `darken`, `contrast-ink` and `shade`.
- `@font-face`, `url()` and `@import` through a host resolver.
- `file:line:col` errors, and reloads that keep the last valid sheet.
- A resolved-style cache whose hits allocate nothing.
