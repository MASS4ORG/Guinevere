namespace Guinevere;

/// <summary>
/// The semantic kind of a control, as seen by accessibility and automation adapters. The set is open:
/// any package defines its own role with <c>new ControlRole("ColorWell", ControlRole.Button)</c>, naming
/// the standard role that native adapters fall back to; derive from it when a role needs to carry data.
/// </summary>
public record ControlRole
{
    /// <summary>Creates a role.</summary>
    /// <param name="name">Stable role name; native adapters match standard names exactly.</param>
    /// <param name="base">The role this one specializes, used when an adapter does not know <paramref name="name"/>.</param>
    /// <param name="activatable">Whether assistive technology may activate it; null inherits from <paramref name="base"/> (true at the root).</param>
    public ControlRole(string name, ControlRole? @base = null, bool? activatable = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Base = @base;
        Activatable = activatable ?? @base?.Activatable ?? true;
    }

    /// <summary>Stable role name; native adapters match standard names exactly.</summary>
    public string Name { get; }

    /// <summary>The role this one specializes, or null for a standard role.</summary>
    public ControlRole? Base { get; }

    /// <summary>Whether assistive technology may activate a control with this role, not only focus it.</summary>
    public bool Activatable { get; }

    /// <summary>Returns whether this role is <paramref name="role"/> or specializes it.</summary>
    public bool Is(ControlRole role)
    {
        for (var current = this; current is not null; current = current.Base)
        {
            if (current == role)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns <see cref="Name"/>.</summary>
    public override string ToString() => Name;

    /// <summary>No specialized semantic role.</summary>
    public static ControlRole None { get; } = new("None", activatable: false);
    /// <summary>The application root.</summary>
    public static ControlRole Application { get; } = new("Application", activatable: false);
    /// <summary>A top-level window or a tool window.</summary>
    public static ControlRole Window { get; } = new("Window", activatable: false);
    /// <summary>A generic container.</summary>
    public static ControlRole Panel { get; } = new("Panel", activatable: false);
    /// <summary>A labeled group of related controls.</summary>
    public static ControlRole Group { get; } = new("Group", activatable: false);
    /// <summary>A free-form drawing or editing surface.</summary>
    public static ControlRole Canvas { get; } = new("Canvas", activatable: false);
    /// <summary>Static, read-only text.</summary>
    public static ControlRole Text { get; } = new("Text", activatable: false);
    /// <summary>A navigation link or a relationship between elements.</summary>
    public static ControlRole Link { get; } = new("Link");
    /// <summary>A list of items.</summary>
    public static ControlRole List { get; } = new("List", activatable: false);
    /// <summary>One item of a list.</summary>
    public static ControlRole ListItem { get; } = new("ListItem");
    /// <summary>A hierarchical list of items.</summary>
    public static ControlRole Tree { get; } = new("Tree", activatable: false);
    /// <summary>One item of a tree.</summary>
    public static ControlRole TreeItem { get; } = new("TreeItem");
    /// <summary>A command button.</summary>
    public static ControlRole Button { get; } = new("Button");
    /// <summary>A binary checkbox.</summary>
    public static ControlRole Checkbox { get; } = new("Checkbox");
    /// <summary>A binary switch; adapters without a switch role present a checkbox.</summary>
    public static ControlRole Switch { get; } = new("Switch", Checkbox);
    /// <summary>One option in a single-selection group.</summary>
    public static ControlRole Radio { get; } = new("Radio");
    /// <summary>One page in a tab set.</summary>
    public static ControlRole Tab { get; } = new("Tab");
    /// <summary>A bounded numeric value.</summary>
    public static ControlRole Slider { get; } = new("Slider");
    /// <summary>An expandable single-choice list.</summary>
    public static ControlRole ComboBox { get; } = new("ComboBox");
    /// <summary>A menu that exposes commands.</summary>
    public static ControlRole Menu { get; } = new("Menu");
    /// <summary>A command within a menu.</summary>
    public static ControlRole MenuItem { get; } = new("MenuItem");
    /// <summary>A text entry field.</summary>
    public static ControlRole TextInput { get; } = new("TextInput", activatable: false);
    /// <summary>A grid of rows and columns.</summary>
    public static ControlRole Table { get; } = new("Table", activatable: false);
    /// <summary>The header of one table column.</summary>
    public static ControlRole ColumnHeader { get; } = new("ColumnHeader", activatable: false);
    /// <summary>One row of a table.</summary>
    public static ControlRole Row { get; } = new("Row", activatable: false);
    /// <summary>One cell of a table row.</summary>
    public static ControlRole Cell { get; } = new("Cell", activatable: false);
}
