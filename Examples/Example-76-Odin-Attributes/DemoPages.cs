namespace Example_76_Odin_Attributes;

/// <summary>Every page of the tour, in sidebar order.</summary>
public static class DemoPages
{
    /// <summary>
    /// The pages: plain types first, then attributes. Attributes still to come stay listed without a demo and are
    /// hidden from the sidebar until they ship.
    /// </summary>
    public static IReadOnlyList<DemoPage> All { get; } =
    [
        new("Types", "Primitives", "Booleans, text, enums and every numeric type get an editor without any attribute.",
            typeof(PrimitivesDemo)),
        new("Types", "Vectors & Colors", "System.Numerics vectors edit per axis; drag an axis letter to scrub it.",
            typeof(VectorsAndColorsDemo)),
        new("Types", "Lists & Arrays", "Lists grow, shrink and reorder by their grip; arrays reorder but keep their size.",
            typeof(ListsAndArraysDemo)),
        new("Types", "Dictionaries", "Entries are edited through their keys; adding invents a fresh key to rename later.",
            typeof(DictionariesDemo)),
        new("Types", "Nested Objects", "A plain class inside another opens as a foldable compartment.",
            typeof(NestedObjectsDemo)),
        new("Types", "Nested Structs", "Edits to a struct are written back to the field that holds it.",
            typeof(NestedStructsDemo)),
        new("Types", "Nullables", "Nullable values edit like their underlying type.", typeof(NullablesDemo)),

        new("Essentials", "Show", "Shows a private member, or a get-only property as read-only text.", typeof(ShowDemo)),
        new("Essentials", "Hide", "Hides a public member from the form.", typeof(HideDemo)),
        new("Essentials", "SetOrder", "Moves members before or after the declaration order.", typeof(SetOrderDemo)),
        new("Essentials", "ReadOnly", "Shows a value without letting it change.", typeof(ReadOnlyDemo)),
        new("Essentials", "Required", "Shows an error while a value is missing; never blocks typing.", typeof(RequiredDemo)),
        new("Essentials", "Title", "A heading, subtitle and line above a member.", typeof(TitleDemo)),
        new("Essentials", "Tooltip", "Explains a member when its row is hovered.", typeof(TooltipDemo)),
        new("Essentials", "HideLabel", "Lets the editor use the whole row.", typeof(HideLabelDemo)),
        new("Essentials", "GUIColor", "Tints a member's controls; nested content inherits the tint.", typeof(GuiColorDemo)),
        new("Buttons", "Button", "Turns a parameterless method into a button.", typeof(ButtonDemo)),
        new("Numbers", "Range", "Clamps typing and label scrubbing between two bounds.", typeof(RangeDemo)),
        new("Text", "TextArea", "A multi-line box that grows with its text, then scrolls.", typeof(TextAreaDemo)),
        new("Collections", "ListDrawerSettings", "Paging, drag reordering and index labels per list.",
            typeof(ListDrawerSettingsDemo)),
        new("Misc", "EnumLabel", "Friendlier names for enum values.", typeof(EnumLabelDemo)),

        new("Coming Soon", "ShowIf / HideIf", "Shows or hides a member from another member's value."),
        new("Coming Soon", "EnableIf / DisableIf", "Locks a member from another member's value."),
        new("Coming Soon", "InfoBox", "A note, warning or error box above a member."),
        new("Coming Soon", "OnValueChanged", "Calls a method whenever a member changes."),
        new("Coming Soon", "ValueDropdown", "Picks a value from a list the object supplies."),
        new("Coming Soon", "FoldoutGroup", "Groups members under a foldable heading."),
        new("Coming Soon", "BoxGroup", "Groups members in a titled box."),
        new("Coming Soon", "TabGroup", "Splits members across tabs."),
        new("Coming Soon", "HorizontalGroup", "Lays members out side by side."),
        new("Coming Soon", "InlineProperty", "Draws a nested object's members without a compartment."),
        new("Coming Soon", "MinMaxSlider", "Edits a range with one two-handled slider."),
        new("Coming Soon", "ProgressBar", "Shows a number as a bar."),
        new("Coming Soon", "LabelText", "Replaces a member's label."),
        new("Coming Soon", "SuffixLabel", "Adds a unit after a member's editor."),
        new("Coming Soon", "TableList", "Draws a list of objects as a table."),
        new("Coming Soon", "Searchable", "Filters a long form or list by text."),
    ];
}
