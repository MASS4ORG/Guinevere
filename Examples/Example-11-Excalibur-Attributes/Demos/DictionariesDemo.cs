namespace Example11ExcaliburAttributes;

// Entries are edited through their keys; "+" invents a key ("New Key", 0, 1, ...).
/// <summary>Demonstrates dictionaries with string and integer keys.</summary>
public sealed class DictionariesDemo
{
    /// <summary>Inventory counts keyed by item name.</summary>
    public Dictionary<string, int> Inventory { get; set; } = new() { ["Sword"] = 1, ["Potion"] = 5 };
    /// <summary>Level names keyed by level number.</summary>
    public Dictionary<int, string> LevelNames { get; set; } = new() { [1] = "Forest", [2] = "Castle" };
}
