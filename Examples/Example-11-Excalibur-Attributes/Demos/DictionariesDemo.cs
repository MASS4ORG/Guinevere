namespace Example_76_Odin_Attributes;

// Entries are edited through their keys; "+" invents a key ("New Key", 0, 1, ...).
public sealed class DictionariesDemo
{
    public Dictionary<string, int> Inventory { get; set; } = new() { ["Sword"] = 1, ["Potion"] = 5 };
    public Dictionary<int, string> LevelNames { get; set; } = new() { [1] = "Forest", [2] = "Castle" };
}
