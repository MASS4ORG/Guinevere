namespace Example_76_Odin_Attributes;

public sealed class Waypoint
{
    public string Name { get; set; } = "Waypoint";
    public float Wait { get; set; } = 1;
}

// Lists add, remove and reorder by their grip; arrays reorder but keep their length.
public sealed class ListsAndArraysDemo
{
    public List<string> Tags { get; set; } = ["knight", "round-table", "quest"];
    public int[] HighScores { get; set; } = [900, 750, 600];
    public List<Waypoint> Route { get; set; } = [new() { Name = "Camelot" }, new() { Name = "Avalon", Wait = 3 }];
}
