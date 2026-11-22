using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [ListDrawerSettings] tunes paging, drag reordering and index labels per list.
/// <summary>Demonstrates list paging and item display options.</summary>
public sealed class ListDrawerSettingsDemo
{
    /// <summary>List shown with five entries per page.</summary>
    [ListDrawerSettings(NumberOfItemsPerPage = 5)]
    public List<int> Paged { get; set; } = [.. Enumerable.Range(1, 23)];

    /// <summary>List shown with index labels and drag reordering disabled.</summary>
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = false)]
    public List<string> Labelled { get; set; } = ["first", "second", "third"];

    /// <summary>List shown without paging.</summary>
    [ListDrawerSettings(ShowPaging = false)]
    public List<int> Unpaged { get; set; } = [.. Enumerable.Range(1, 12)];
}
