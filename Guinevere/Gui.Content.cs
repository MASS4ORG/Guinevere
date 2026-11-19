namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Builds <paramref name="content"/> as children of the current node: text and icons as self-sizing nodes, a
    /// builder or custom type as whatever it builds. Widgets call it for their caption; it works in any node.
    /// </summary>
    /// <param name="content">What to show; <see cref="Guinevere.Content.None"/> builds nothing.</param>
    public void Content(in Content content) => content.BuildIn(this);
}
