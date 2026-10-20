using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>Retains a single parsed filter stack per styled node.</summary>
static class StyledRenderEffects
{
    sealed class Cached
    {
        internal string? Source;
        internal RenderEffectStack? Stack;
    }

    static readonly ConditionalWeakTable<LayoutNode, Cached> Cache = new();

    internal static void Apply(Gui gui, LayoutNode node, ResolvedStyle style)
    {
        var source = style.Get("filter");
        if (source is null && !Cache.TryGetValue(node, out _))
        {
            return;
        }
        var cached = Cache.GetOrCreateValue(node);
        if (cached.Source != source)
        {
            cached.Stack?.Dispose();
            cached.Stack = null;
            cached.Source = source;
            if (source is not null)
            {
                cached.Stack = RenderEffectParser.Parse(source);
            }
        }
        if (cached.Stack is { } stack)
        {
            gui.SetRenderEffects(stack, node.Scope);
        }
    }
}
