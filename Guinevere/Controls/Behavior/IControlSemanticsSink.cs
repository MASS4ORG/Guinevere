namespace Guinevere;

/// <summary>Optional platform capability that receives the controls present in a rendered frame.</summary>
public interface IControlSemanticsSink : IPlatformCapability
{
    /// <summary>Publishes one control's current metadata.</summary>
    void Publish(ControlSemantics semantics);
}
