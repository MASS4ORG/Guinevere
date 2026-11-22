namespace Example11ExcaliburAttributes;

/// <summary>A route location and the time to wait there.</summary>
public sealed class Waypoint
{
    /// <summary>Name of the route location.</summary>
    public string Name { get; set; } = "Waypoint";
    /// <summary>Wait duration at this location.</summary>
    public float Wait { get; set; } = 1;
}

/// <summary>Demonstrates editing resizable lists and fixed-length arrays.</summary>
public sealed class ListsAndArraysDemo
{
    /// <summary>Editable tags stored in a resizable list.</summary>
    public List<string> Tags { get; set; } = ["knight", "round-table", "quest"];
    /// <summary>Scores stored in a fixed-length array.</summary>
    public int[] HighScores { get; set; } = [900, 750, 600];
    /// <summary>Ordered locations visited by the example route.</summary>
    public List<Waypoint> Route { get; set; } = [new() { Name = "Camelot" }, new() { Name = "Avalon", Wait = 3 }];
}
