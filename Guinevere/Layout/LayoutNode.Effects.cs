namespace Guinevere;

public partial class LayoutNode
{
    /// <summary>Composites this subtree on its z-layer through a reusable ordered effect stack.</summary>
    public LayoutNode Effects(RenderEffectStack effects)
    {
        _gui.SetRenderEffects(effects, Scope);
        return this;
    }
}
