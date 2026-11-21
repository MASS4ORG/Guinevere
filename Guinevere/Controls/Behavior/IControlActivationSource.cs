namespace Guinevere;

/// <summary>
/// Optional command source for controller, assistive-technology, or application-defined activation.
/// The focused control passes its stable id to the source during the render pass.
/// </summary>
public interface IControlActivationSource : IPlatformCapability
{
    /// <summary>Returns true when a routed activation command targets this control.</summary>
    bool IsActivated(string controlId);
}
