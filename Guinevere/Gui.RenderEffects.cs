using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    /// <summary>Applies an ordered postprocess stack to the current subtree on its z-layer.</summary>
    public void SetRenderEffects(RenderEffectStack effects, LayoutNodeScope? scope = null)
    {
        ArgumentNullException.ThrowIfNull(effects);
        (scope ?? CurrentNodeScope).Set(effects.ScopeValue);
        _opacityUsed = true;
    }

    /// <summary>Enters a layout group that captures its drawing and descendants through a reusable effect stack.</summary>
    public LayoutNodeScope RenderEffects(RenderEffectStack effects, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        Node(id: id, filePath: filePath, lineNumber: lineNumber).Effects(effects).Enter();

    int SaveCompositeLayer(int index, LayoutNode node, float opacity)
    {
        if (!node.Scope.HasLocal<LayoutNodeScopeRenderEffects>())
        {
            return opacity < 1 ? SaveOpacityLayer(GroupInkBounds(index), opacity) : -1;
        }

        var save = Canvas!.Save();
        var matrix = Canvas.TotalMatrix;
        if (TransformsUsed)
        {
            ApplyTransformedClips(node, Canvas);
        }
        else
        {
            ApplyAncestorClips(node, Canvas);
        }
        Canvas.SetMatrix(matrix);

        var runtime = Platform.TryGet<IGpuEffectsCapability>(out var capability)
            && capability!.SupportsRuntimeShaderCompilation;
        var paint = node.Scope.Get<LayoutNodeScopeRenderEffects>().Value!.Paint(runtime, opacity,
            capability?.SupportsRenderTargets ?? true);
        // The clip bounds include descendant filter outsets; source ink bounds cannot bound nested filters.
        Canvas.SaveLayer(paint);
        return save;
    }
}

/// <summary>The stack applied locally to a node's composited subtree.</summary>
public sealed class LayoutNodeScopeRenderEffects : ILayoutNodeScopeValue<LayoutNodeScopeRenderEffects>
{
    /// <summary>No postprocess operations.</summary>
    public static LayoutNodeScopeRenderEffects Default { get; } = new();

    /// <summary>The reusable stack, owned by the caller.</summary>
    public RenderEffectStack? Value { get; init; }
}
