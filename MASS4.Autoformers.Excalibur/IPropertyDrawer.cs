namespace MASS4.Autoformers;

/// <summary>
/// Draws a field: its whole labelled presentation, or only its value when the caller owns the label. Called in
/// both passes; it must build the same nodes in each and apply input only in the render pass.
/// </summary>
public interface IPropertyDrawer
{
    /// <summary>Draws the labelled field.</summary>
    void Draw(Gui gui, FormField field, string id, FormRenderContext context);

    /// <summary>Draws only the value; returns false when this drawer has no value-only form.</summary>
    bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context);
}

/// <summary>Wraps a field's drawer for one attribute, such as a tooltip, without replacing it.</summary>
public interface IAttributeDrawer
{
    /// <summary>Lower orders wrap higher orders; ties keep attribute declaration order.</summary>
    int Order => 0;

    /// <summary>Draws the decoration and invokes <paramref name="next"/> for the inner field.</summary>
    void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context, Action next);
}
