# Autoformers.Excalibur

Draws a `Autoformers` model with Excalibur controls.

```csharp
var context = new FormRenderContext();                 // keep one per panel: fold state lives here
gui.Form(FormBuilder.Build(settings), "settings", context);
```

- `gui.FormField(field, id, context)` draws one labelled row; `gui.FormFieldEditor(...)` only its editor.
- `FormDrawers` is a registry scope. `Add(...)` returns an `IDisposable` that removes exactly that registration;
  a child scope falls back to its parent and `FormDrawers.Default` holds the built-ins.
- Dispatch: attribute decorators wrap → writable type drawer → predicate drawers → collection → nested object →
  built-in (bool, string, enum, numbers) → read-only summary.
- Colors come from the `form` stylesheet rule and its theme tokens; sizes from inherited control style tokens
  (`CompactHeight`, `CompactFontSize`) plus `FormLabelWidth`, `FormIndent`, `FormCaretOpen` and `FormCaretClosed`.
- Drawing is single-threaded (the GUI thread); registration may happen from any thread.

Depends on `MASS4.Guinevere`, `MASS4.Guinevere.Excalibur` and `Autoformers` only; never on Gaya or Turian.

## Enum fields

```csharp
using MASS4.Attributes;

[Flags]
public enum Permissions { None = 0, Read = 1, Write = 2 }
public enum Mode { Preview, Edit, Play }

public sealed class Settings
{
    public Permissions Access { get; set; }       // Searchable flag dropdown.
    [EnumButtons] public Mode Mode { get; set; } // One horizontal button group.
    [EnumPaging] public Mode Step { get; set; }  // Dropdown plus previous/next.
}
```

No enum attribute is needed for the searchable dropdown. `[Flags]` enables independent checkbox/button selection, and mixed flag edits preserve each owner's unrelated bits. Selecting None clears the full mask; paging replaces the full value and wraps through declared choices. `[EnumButtons]` takes precedence when both presentation attributes are present. Use button groups for short option lists; they currently occupy one row.

Apply `[EnumLabel("Display label")]` to an enum member to override its name. `FormRenderContext.Translate` translates these labels. Read-only enum fields use the read-only summary. See the [attribute catalog](../Attributes/README.md) and [short Turian guide](../docs/turian-selection-guide.md) for custom selection controls.
