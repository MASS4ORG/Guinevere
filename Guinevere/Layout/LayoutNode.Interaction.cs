namespace Guinevere;

public partial class LayoutNode
{
    /// <summary>
    /// Handles a click interaction on the current layout node.
    /// </summary>
    /// <returns>
    /// True if the click interaction is successful, otherwise false.
    /// </returns>
    public bool OnClick()
    {
        return _gui.GetInteractable(this).OnClick();
    }

    /// <summary>
    /// Handles a hover interaction on the current layout node.
    /// </summary>
    /// <returns>
    /// True if the hover interaction is successful; otherwise, false.
    /// </returns>
    public bool OnHover()
    {
        return _gui.GetInteractable(this).OnHover();
    }

    /// <summary>The events the widget that built this node reported this frame; cleared whenever the node is built.</summary>
    public NodeSignals Signals { get; private set; }

    /// <summary>Whether the widget was clicked or activated from the keyboard this frame.</summary>
    public bool Clicked => (Signals & NodeSignals.Clicked) != 0;

    /// <summary>Whether the widget changed its value this frame.</summary>
    public bool Changed => (Signals & NodeSignals.Changed) != 0;

    /// <summary>Whether the widget committed its value this frame.</summary>
    public bool Submitted => (Signals & NodeSignals.Submitted) != 0;

    /// <summary>
    /// Reports events on this node for the caller to read after the widget returns. Custom widgets call it the same way
    /// the built-in ones do.
    /// </summary>
    /// <param name="signals">The events to add.</param>
    /// <returns>This node.</returns>
    public LayoutNode Raise(NodeSignals signals)
    {
        Signals |= signals;
        return this;
    }

    /// <summary>Clears the events when the node is built for a pass.</summary>
    internal void ClearSignals() => Signals = NodeSignals.None;
}
