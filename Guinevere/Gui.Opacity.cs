namespace Guinevere;

public partial class Gui
{
    /// <summary>A faded subtree being composited into one layer: its z-layer, last descendant and canvas save.</summary>
    readonly record struct OpacityGroup(LayoutNode Node, int Z, int End, int SaveCount);

    readonly List<OpacityGroup> _opacityGroups = [];
    bool _opacityUsed;

    /// <summary>
    /// Keeps the open opacity groups in step with the render order, opening one for a node that sets an opacity,
    /// and returns the opacity the node itself must still apply. Inside an open group a subtree composites as one
    /// image, like CSS; descendants drawn on another z-layer (popups) multiply the opacity instead.
    /// </summary>
    float EnterOpacityGroups(int index)
    {
        var (z, sequence, end, node) = _renderNodes[index];
        CloseOpacityGroups(z, sequence);
        if (node.Scope.HasLocal<LayoutNodeScopeOpacity>() && !Hidden())
        {
            var opacity = node.Scope.Get<LayoutNodeScopeOpacity>().Value;
            var save = opacity is > 0f and < 1f ? SaveOpacityLayer(GroupInkBounds(index), opacity) : -1;
            _opacityGroups.Add(new OpacityGroup(node, z, end, opacity > 0f ? save : int.MinValue));
        }
        return Hidden() ? 0f : UngroupedOpacity(node);
    }

    /// <summary>
    /// The area the group's members on its z-layer paint, or <c>null</c> when one of them cannot be bounded.
    /// </summary>
    SKRect? GroupInkBounds(int index)
    {
        var (z, _, end, _) = _renderNodes[index];
        var ink = SKRect.Empty;
        for (var i = index; i < _renderNodes.Count && _renderNodes[i].Z == z && _renderNodes[i].Sequence <= end; i++)
        {
            var member = _renderNodes[i].Node;
            if (member.DrawList.InkBounds(member) is not { } bounds) return null;
            ink = Ink.Join(ink, bounds);
        }
        return ink;
    }

    /// <summary>
    /// Opens an offscreen layer composited at <paramref name="opacity"/>, as small as the painted area allows; an
    /// unbounded area falls back to a layer over the current clip. Returns the count to restore to.
    /// </summary>
    int SaveOpacityLayer(SKRect? bounds, float opacity) => bounds is { } area
        ? Canvas!.SaveLayer(SKRect.Inflate(area, 1f, 1f), OpacityPaint(opacity))
        : Canvas!.SaveLayer(OpacityPaint(opacity));

    /// <summary>Closes every group that the node at <paramref name="z"/>/<paramref name="sequence"/> is not inside.</summary>
    void CloseOpacityGroups(int z, int sequence)
    {
        while (_opacityGroups.Count > 0 && (_opacityGroups[^1].Z != z || sequence > _opacityGroups[^1].End))
        {
            if (_opacityGroups[^1].SaveCount > 0) Canvas!.RestoreToCount(_opacityGroups[^1].SaveCount);
            _opacityGroups.RemoveAt(_opacityGroups.Count - 1);
        }
    }

    /// <summary>Whether an open group is fully transparent, so its subtree draws nothing.</summary>
    bool Hidden()
    {
        foreach (var group in _opacityGroups)
            if (group.SaveCount == int.MinValue) return true;
        return false;
    }

    /// <summary>The product of the opacities set on the node and its ancestors that no open group already applies.</summary>
    float UngroupedOpacity(LayoutNode node)
    {
        var opacity = 1f;
        for (var current = node; current is not null; current = current.Parent)
            if (current.Scope.HasLocal<LayoutNodeScopeOpacity>() && !IsOpenGroup(current))
                opacity *= current.Scope.Get<LayoutNodeScopeOpacity>().Value;
        return opacity;
    }

    bool IsOpenGroup(LayoutNode node)
    {
        foreach (var group in _opacityGroups)
            if (ReferenceEquals(group.Node, node)) return true;
        return false;
    }

    static SKPaint OpacityPaint(float opacity) =>
        new() { Color = new SKColor(255, 255, 255, (byte)(opacity * 255f + 0.5f)) };
}
