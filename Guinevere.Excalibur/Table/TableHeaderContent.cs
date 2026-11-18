namespace Guinevere;

/// <summary>What a header's content is drawn from: the column, the room it has, and the table's state.</summary>
/// <param name="Column">The column whose header this is.</param>
/// <param name="FontSize">The table's text size, in pixels.</param>
/// <param name="Width">The header cell's width, in pixels.</param>
/// <param name="Height">The header cell's height, in pixels.</param>
/// <param name="State">The table's state, e.g. to read its sort.</param>
public readonly record struct TableHeaderContext(TableColumn Column, float FontSize, float Width, float Height,
    TableState State);

/// <summary>
/// Draws the inside of a column's header cell, in place of its <see cref="TableColumn.Title"/> text: an icon,
/// a localized text, another widget, or anything else a class or struct of yours can draw. The cell, its
/// padding, alignment and clipping, and the sort marker after the content, are the table's.
/// </summary>
public interface ITableHeaderContent
{
    /// <summary>Draws the content inside the header cell; called once per frame for each built header.</summary>
    /// <param name="gui">The Gui, positioned inside the header cell.</param>
    /// <param name="context">The column and the room it has.</param>
    void Render(Gui gui, in TableHeaderContext context);
}

/// <summary>Ready-made header contents.</summary>
public static class TableHeaderContent
{
    /// <summary>A header that shows an icon glyph, e.g. one from <c>WidgetIcons</c>.</summary>
    /// <param name="glyph">The icon's text.</param>
    public static ITableHeaderContent Icon(string glyph)
    {
        ArgumentNullException.ThrowIfNull(glyph);
        return new IconContent(glyph);
    }

    /// <summary>
    /// A header whose text is asked for every frame, so a localization lookup follows the current language
    /// without rebuilding the table.
    /// </summary>
    /// <param name="text">Returns the text to show; keep it cheap, it runs each frame.</param>
    public static ITableHeaderContent Text(Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TextContent(text);
    }

    sealed class IconContent(string glyph) : ITableHeaderContent
    {
        public void Render(Gui gui, in TableHeaderContext context) =>
            gui.DrawText(glyph, context.FontSize, centerInRect: false, clip: true);
    }

    sealed class TextContent(Func<string> text) : ITableHeaderContent
    {
        public void Render(Gui gui, in TableHeaderContext context) =>
            gui.DrawText(text(), context.FontSize, centerInRect: false, clip: true);
    }
}
