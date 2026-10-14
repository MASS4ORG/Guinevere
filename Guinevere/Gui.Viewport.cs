using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    /// <summary>Logical bounds for layouts and overlays in the current viewport, or the physical screen outside it.</summary>
    public Rect LayoutBounds => LayoutNodeScopeStack.TryPeek(out var scope) ? BoundsFor(scope) : ScreenRect;

    internal Rect BoundsFor(LayoutNodeScope scope) =>
        scope.Get<LayoutNodeScopeViewport>().Value?.ContentBounds ?? ScreenRect;

    /// <summary>Enters a clipped safe viewport whose transform also maps pointer input. Call in both passes.</summary>
    public LayoutNodeScope Viewport(ViewportOptions options, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(options);
        var layout = options.Resolve();
        var node = Node(layout.ContentBounds.W, layout.ContentBounds.H, id, filePath, lineNumber)
            .AbsoluteScreen(0, 0);
        node.Scope.Set(new LayoutNodeScopeViewport { Value = layout });
        var scope = node.Enter();
        SetTransform(layout.Transform);
        TransformsUsed = true;
        ClipContent();
        return scope;
    }

    /// <summary>Creates an anchored or stretched node against the current viewport's safe logical bounds.</summary>
    public LayoutNode AnchoredNode(Vector2 min, Vector2 max, Vector2 size, Vector2 pivot,
        ViewportInsets offsets = default, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var layout = new ViewportLayout(default, LayoutBounds, 1, 1, Matrix3x2.Identity);
        var rect = layout.Anchor(min, max, size, pivot, offsets);
        return Node(rect.W, rect.H, id, filePath, lineNumber).AbsoluteScreen(rect.X, rect.Y);
    }

    static bool IsViewport(LayoutNode node) => node.Scope.HasLocal<LayoutNodeScopeViewport>();

    static bool IsOutsideViewport(LayoutNode node, Vector2 position) =>
        IsViewport(node) && !ViewportClipBounds(node).Contains(node.ScreenToLocal(position));

    static Rect ViewportClipBounds(LayoutNode node)
    {
        var layout = node.Scope.Get<LayoutNodeScopeViewport>().Value!.Value;
        var safe = layout.SafeBounds;
        var transform = layout.Transform;
        var x = Math.Max(0, (safe.X - transform.M31) / layout.Scale);
        var y = Math.Max(0, (safe.Y - transform.M32) / layout.Scale);
        var right = Math.Min(layout.ContentBounds.W, (safe.X + safe.W - transform.M31) / layout.Scale);
        var bottom = Math.Min(layout.ContentBounds.H, (safe.Y + safe.H - transform.M32) / layout.Scale);
        return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }
}
