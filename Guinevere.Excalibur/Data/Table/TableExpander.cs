namespace Guinevere;

/// <summary>
/// The cell of <see cref="TableColumn{TRow}.Expander"/>: a chevron for a row that has details, which a press
/// turns. The table's details provider is the one in its options, so the column needs no configuration.
/// </summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
sealed class ExpanderRenderer<TRow> : ITableCellRenderer<TRow, TRow>
{
    public void Render(Gui gui, in TableCellContext<TRow, TRow> cell)
    {
        if (cell.State.Details is not ITableRowDetails<TRow> details)
        {
            return;
        }

        var row = new TableRowContext<TRow>(cell.Index, cell.Item, cell.Identity, cell.State);
        if (!details.HasDetails(row))
        {
            return;
        }

        var expansion = cell.State.Expansion;
        var expanded = expansion.IsExpanded(row.Identity);
        using (gui.StyledNode("table-expander").ExpandWidth().Height(cell.Height)
                   .AlignContentX(0.5f).AlignContentY(0.5f).Enter())
        {
            gui.DrawText(expanded ? WidgetIcons.ChevronDown : WidgetIcons.ChevronRight, cell.FontSize);
            if (gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick())
            {
                expansion.Toggle(row.Identity, cell.Index);
            }
        }
    }
}
