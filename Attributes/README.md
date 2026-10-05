# MASS4.Attributes

Shared metadata for forms, editor tools, stable type identity and service discovery. Consumers implement the behavior; this library references neither Turian, Gaya nor a GUI toolkit.

`Autoformers` builds editable forms from these attributes. Engine-specific component, asset and serialization rules live in Turian.Engine.Attributes.

## Form attributes

Use `using MASS4.Attributes;`. Presentation is implemented by [Autoformers.Excalibur](../Autoformers.Excalibur/README.md); the attributes themselves contain only metadata.

| Attribute | Purpose |
|---|---|
| `[Show]`, `[Hide]` | Include a nonpublic field/property or exclude a member from the form. |
| `[SetOrder(priority)]` | Order members; lower priorities appear first. |
| `[ReadOnly]` | Display a member without allowing edits. |
| `[Range(min, max)]` | Constrain the numeric editor. |
| `[Required]`, `[Required("Message")]` | Display a missing-value message without blocking writes. |
| `[Title("Heading", "Subtitle")]` | Add a heading above a field. |
| `[Tooltip("Help")]` | Add hover help. |
| `[HideLabel]` | Give the editor the full row width. |
| `[GuiColor("#RRGGBB")]` | Tint the controls and nested content; float channel constructors are also available. |
| `[Button]` | Expose a public parameterless method as a form action. |
| `[TextArea(minLines, maxLines)]` | Edit text in a growing multiline box, then scroll. |
| `[ListDrawerSettings(...)]` | Configure collection paging, drag reordering and index labels. |
| `[EnumLabel("Label")]` | Override an enum member's display name. |
| `[EnumButtons]` | Display an enum field/property as one horizontal button group; flags toggle independently. |
| `[EnumPaging]` | Add previous/next controls to the searchable enum dropdown. |

Enum fields use searchable dropdowns by default; `[Flags]` is the standard .NET attribute that enables multiple selection. Selecting zero clears a flag value, while paging replaces its full value. `[EnumButtons]` takes precedence over `[EnumPaging]` if both are present. Labels use member names unless overridden; enum aliases share one numeric choice. Enum member visibility, icons and obsolete diagnostics are not currently implemented. See the [enum examples](../Autoformers.Excalibur/README.md#enum-fields).

## Host metadata

These attributes expose information for application consumers. Autoformers retains member metadata, but a host must implement behavior beyond the form attributes above.

| Attribute | Purpose |
|---|---|
| `[Expand(defaultExpanded)]` | Request an initial expanded state. |
| `[NumericUpDown]` | Request numeric increment/decrement controls. |
| `[CustomEditor(typeof(Editor))]`, `[DefaultEditor]` | Declare editor selection metadata. |
| `[EditorSetting("Path", ...)]` | Describe a settings page or option. |
| `[MenuItem("Path")]`, `[Panel("Name")]` | Describe commands and dock panels. |
| `[DefaultOption]` | Mark a fallback importer. |
| `[TypeId("guid")]` | Declare a stable type identity for serialization consumers. |
| `[InternalService(...)]` | Declare a service type and `InternalServiceLifetime`. |
| `[SuppressPrivate]` | Prevent an attribute from implicitly exposing nonpublic members. |

For direct GUI widgets and custom layer-mask drawers, see the [short Turian guide](../docs/turian-selection-guide.md).
