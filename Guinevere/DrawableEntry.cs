namespace Guinevere;

/// <inheritdoc />
public class DrawableEntry : IDrawListEntry
{
    readonly IDrawable _drawable;

    /// <summary>
    /// Represents an entry in a draw list that encapsulates a drawable object.
    /// </summary>
    public DrawableEntry(IDrawable drawable)
    {
        _drawable = drawable;
    }

    /// <inheritdoc />
    public void Execute(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        _drawable.Render(gui, node, canvas);
    }
}
