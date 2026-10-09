using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [ListDrawerSettings] tunes paging, drag reordering and index labels per list.
public sealed class ListDrawerSettingsDemo
{
    [ListDrawerSettings(NumberOfItemsPerPage = 5)]
    public List<int> Paged { get; set; } = [.. Enumerable.Range(1, 23)];

    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = false)]
    public List<string> Labelled { get; set; } = ["first", "second", "third"];

    [ListDrawerSettings(ShowPaging = false)]
    public List<int> Unpaged { get; set; } = [.. Enumerable.Range(1, 12)];
}
