# Autoformers

A GUI-free form model. `FormBuilder.Build(target, options)` reflects a plain C# object into a `FormModel`: sections of `FormField`s, `CollectionField` views over lists, arrays and dictionaries, and `[Button]` actions. Member visibility, order, read-only state, ranges and tooltips come from `MASS4.Attributes`.

The model never saves, validates or logs on its own. `FormOptions` supplies the consumer's policy:

- `MutationNotifier`: called with the mutated object after each write, for dirty tracking, undo or autosave.
- `ReadOnly`: disables writes, resizing and actions throughout the form, including collection entries.
- `AcceptsNull`: which member types may be set to null (default: `Nullable<T>` only).
- `FailureReporter`: receives exceptions from getters, setters and actions; the form otherwise discards them.

Consumers add their own structure on top, for example a heading switch through `FormSection.EnabledField`, or calculated rows with `FormField.Display`.

Dependency direction: `MASS4.Attributes` → `Autoformers` → renderers → applications. This library references no GUI toolkit.

Enum presentation metadata (`[EnumButtons]`, `[EnumPaging]` and member `[EnumLabel]`) is consumed by [Autoformers.Excalibur](../Autoformers.Excalibur/README.md#enum-fields). See [MASS4.Attributes](../Attributes/README.md) for the attribute catalog.
