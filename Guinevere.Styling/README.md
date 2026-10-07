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
$accent = #4a90e2;

button {
    padding = 10 16;
    bg-color = #354158;
    :hover(0.15 ease-out) { bg-color = $accent; }
}
```

The package covers:
- Type, class, id, descendant and `>` selectors.
- Nesting, `#inherit` and custom modifiers.
- `$tokens` layered across sheets, and `@const` values the host can read.
- `@font-face`, `url()` and `@import` through a host resolver.
- `file:line:col` errors, and reloads that keep the last valid sheet.
- A resolved-style cache whose hits allocate nothing.
