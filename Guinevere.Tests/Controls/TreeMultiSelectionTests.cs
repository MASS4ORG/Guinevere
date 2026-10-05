namespace Guinevere.Tests.Controls;

/// <summary>Checks tree selection order and fixed range anchors independently of hierarchy content.</summary>
public sealed class TreeMultiSelectionTests
{
    /// <summary>Control toggles rows while Shift selects contiguous rows around the original anchor.</summary>
    [Fact]
    public void ModifierSelectionsKeepOrderAndRangeAnchor()
    {
        var state = new TreeViewState { MultiSelect = true };
        string[] rows = ["a", "b", "c", "d"];
        state.Select("b", rows);
        state.Select("d", rows, toggle: true);
        Assert.Equal(new[] { "b", "d" }, state.SelectedIds);
        Assert.Equal("d", state.SelectedId);
        state.Select("a", rows, range: true);
        Assert.Equal(rows, state.SelectedIds);
        state.Select("c", rows, range: true);
        Assert.Equal(new[] { "c", "d" }, state.SelectedIds);
        state.Select("a", rows, toggle: true, range: true);
        Assert.Equal(new[] { "c", "d", "a", "b" }, state.SelectedIds);
        state.Select("a", rows, toggle: true);
        Assert.DoesNotContain("a", state.SelectedIds);
        state.SetSelection([]);
        Assert.Null(state.SelectedId);
    }

    /// <summary>Single-selection trees keep replacing their selection even when modifiers are held.</summary>
    [Fact]
    public void MultiSelectionIsOptIn()
    {
        var state = new TreeViewState();
        state.Select("a", ["a", "b"]);
        state.Select("b", ["a", "b"], toggle: true, range: true);
        Assert.Equal(new[] { "b" }, state.SelectedIds);
        state.SelectedId = null;
        Assert.Empty(state.SelectedIds);
    }
}
