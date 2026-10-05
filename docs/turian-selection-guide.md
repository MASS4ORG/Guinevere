# New selection controls: Turian quick guide

Reference `MASS4.Guinevere.Excalibur` and use `using Guinevere;`. Call controls at the same site in both GUI passes. Keep your values between frames.

## 1. Enum fields

```csharp
gui.EnumDropdown(ref mode);
gui.EnumDropdown(ref permissions); // [Flags] gives checkboxes automatically.
gui.EnumDropdown(ref mode, EnumPresentation.ToggleButtons);
gui.EnumDropdown(ref mode, EnumPresentation.Paging);
```

For Autoformers, use `using MASS4.Attributes;` and add `[EnumButtons]` or `[EnumPaging]` to the field/property. Plain enum fields already get search. Add `[EnumLabel("Label")]` to an enum member for a custom label. Buttons fit one row; use dropdowns for long lists. Paging replaces the full value, including flags.

## 2. Tags

```csharp
// tagOptions: IReadOnlyList<string>; selectedTags: IReadOnlyList<string>.
var result = gui.MultiDropdown(tagOptions, selectedTags, chips: true);
if (result.Changed) selectedTags = result.Selected;
```

Search and check several entries. Select all/Clear affect only the filtered options. Use `display:` for custom labels and `comparer:` for custom equality. Unknown selected values are preserved.

## 3. Custom layer masks

Pass stable layer slots as the options, not their display names. This example uses a 64-bit mask and slots from 0 through 63:

```csharp
var selected = layerSlots.Where(slot => (mask & (1UL << slot)) != 0).ToArray();
var result = gui.MultiDropdown(layerSlots, selected, display: slot => LayerName(slot));
if (result.Changed)
{
    foreach (var change in result.Changes)
    {
        var bit = 1UL << change.Item;
        mask = change.Selected ? mask | bit : mask & ~bit;
    }
}
```

For multiple owners, apply those same `Changes` to each owner's own mask; each retains its other bits. Pass `mixed: true` when values differ and `isMixed:` to identify slots that differ between owners. Start one undo transaction per changed result and write through Turian's existing setter/dirty-tracking path. `Changed` is delivered once in the build pass; do not guard it with a render-pass check.

Give widgets in a loop distinct node scopes or `filePath` identities. See [Example 50](../Examples/Example-50-Excalibur-Controls/SelectionContent.cs) for a live demo and the [control README](../Guinevere.Excalibur/README.md#searchable-selection) for the full behavior.
