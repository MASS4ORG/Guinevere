# MASS4.Autoformers.Excalibur

Draws a `MASS4.Autoformers` model with Excalibur controls.

```csharp
var context = new FormRenderContext();                 // keep one per panel: fold state lives here
gui.Form(FormBuilder.Build(settings), "settings", context);
```

- `gui.FormField(field, id, context)` draws one labelled row; `gui.FormFieldEditor(...)` only its editor.
- `FormDrawers` is a registry scope. `Add(...)` returns an `IDisposable` that removes exactly that registration;
  a child scope falls back to its parent and `FormDrawers.Default` holds the built-ins.
- Dispatch: attribute decorators wrap → writable type drawer → predicate drawers → collection → nested object →
  built-in (bool, string, enum, numbers) → read-only summary.
- Colors and sizes come from inherited Guinevere control style tokens (`ControlPalette`, `CompactHeight`,
  `CompactFontSize`) plus `FormLabelWidth`, `FormIndent`, `FormCaretOpen` and `FormCaretClosed`.
- Drawing is single-threaded (the GUI thread); registration may happen from any thread.

Depends on Guinevere, Guinevere.Excalibur and MASS4.Autoformers only; never on Gaya or Turian.
