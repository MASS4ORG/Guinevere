namespace MASS4.Attributes;

/// <summary>How a list or array is presented: paging, reordering by drag and index labels.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ListDrawerSettingsAttribute : Attribute
{
    /// <summary>Entries per page when paging is shown.</summary>
    public int NumberOfItemsPerPage { get; set; } = 15;

    /// <summary>Whether entries can be reordered by dragging their handle.</summary>
    public bool DraggableItems { get; set; } = true;

    /// <summary>Whether each entry is labelled with its index.</summary>
    public bool ShowIndexLabels { get; set; }

    /// <summary>Whether long lists are split into pages with controls in the header.</summary>
    public bool ShowPaging { get; set; } = true;
}
