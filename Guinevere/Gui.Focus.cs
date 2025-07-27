namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Manages focus state and navigation for GUI controls.
    /// </summary>
    public FocusManager Focus { get; } = new();

    /// <summary>
    /// Registers the current layout node as a focusable control.
    /// </summary>
    /// <param name="canReceiveFocus">Whether this control can receive keyboard focus</param>
    /// <param name="isInteractable">Whether this control responds to mouse interactions</param>
    /// <param name="parentId">Optional parent ID for cascaded focus. If null, uses the parent layout node's ID</param>
    public void RegisterFocusable(bool canReceiveFocus = true, bool isInteractable = true, string? parentId = null)
    {
        var controlId = CurrentNode.Id;
        var actualParentId = parentId;

        // If no explicit parent ID provided, try to get it from the layout hierarchy
        if (actualParentId == null && CurrentNode.Parent != null)
        {
            actualParentId = CurrentNode.Parent.Id;
        }

        Focus.RegisterFocusableControl(controlId, actualParentId, canReceiveFocus, isInteractable);
    }

    /// <summary>
    /// Checks if the current layout node has focus.
    /// </summary>
    /// <returns>True if the current node has focus</returns>
    public bool HasFocus()
    {
        return Focus.HasFocus(CurrentNode.Id);
    }

    /// <summary>
    /// Checks if the current layout node or any of its descendants has focus.
    /// </summary>
    /// <returns>True if the current node or its descendants have focus</returns>
    public bool HasFocusWithin()
    {
        return Focus.HasFocusWithin(CurrentNode.Id);
    }

    /// <summary>
    /// Requests focus for the current layout node.
    /// </summary>
    /// <param name="reason">The reason for the focus request</param>
    public void RequestFocus(FocusReason reason = FocusReason.Programmatic)
    {
        Focus.RequestFocus(CurrentNode.Id, reason);
    }

    /// <summary>
    /// Checks if the specified control ID has focus.
    /// </summary>
    /// <param name="controlId">The control ID to check</param>
    /// <returns>True if the control has focus</returns>
    public bool HasFocus(string controlId)
    {
        return Focus.HasFocus(controlId);
    }

    /// <summary>
    /// Checks if the specified control ID or any of its descendants has focus.
    /// </summary>
    /// <param name="controlId">The control ID to check</param>
    /// <returns>True if the control or its descendants have focus</returns>
    public bool HasFocusWithin(string controlId)
    {
        return Focus.HasFocusWithin(controlId);
    }

    /// <summary>
    /// Requests focus for the specified control.
    /// </summary>
    /// <param name="controlId">The control ID to focus</param>
    /// <param name="reason">The reason for the focus request</param>
    public void RequestFocus(string controlId, FocusReason reason = FocusReason.Programmatic)
    {
        Focus.RequestFocus(controlId, reason);
    }

    /// <summary>
    /// Clears focus from all controls.
    /// </summary>
    public void ClearFocus()
    {
        Focus.ClearFocus();
    }

    /// <summary>
    /// Handles focus-related interactions for the current interactable element.
    /// Should be called when a control wants to handle mouse clicks for focus.
    /// </summary>
    /// <param name="interactable">The interactable element to check for clicks</param>
    /// <returns>True if the control was clicked and should receive focus</returns>
    public bool HandleFocusInteraction(InteractableElement interactable)
    {
        if (interactable.OnClick())
        {
            RequestFocus(FocusReason.Mouse);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Internal method to handle keyboard navigation and focus management.
    /// Called automatically during frame processing.
    /// </summary>
    internal void ProcessFocusManagement()
    {
        Focus.HandleKeyboardNavigation(Input);
    }

    /// <summary>
    /// Internal method to initialize focus management for the frame.
    /// Called automatically during BeginFrame.
    /// </summary>
    internal void BeginFrameFocus()
    {
        Focus.BeginFrame();
    }

    /// <summary>
    /// Internal method to finalize focus management for the frame.
    /// Called automatically during EndFrame.
    /// </summary>
    internal void EndFrameFocus()
    {
        Focus.EndFrame();
    }
}
