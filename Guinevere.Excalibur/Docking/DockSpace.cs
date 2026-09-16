using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Renders a <see cref="DockLayout"/>: nested splits with draggable splitters, tab groups whose
    /// tabs can be reordered, moved between groups, torn off into floating windows and closed.
    /// </summary>
    /// <remarks>
    /// Every edit is applied to <paramref name="layout"/> when the pointer is released, never while
    /// the frame is being built, so the node tree stays identical across the two passes.
    /// </remarks>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="layout">The layout to render. Mutated in place as the user rearranges it.</param>
    /// <param name="panelInfo">Resolves a panel id to its tab label. Return null for an id the host no longer knows.</param>
    /// <param name="renderPanel">Draws a panel's body. Called for the active tab of every visible group.</param>
    /// <param name="theme">Colors and metrics. Defaults to <see cref="DockTheme.Dark"/>.</param>
    /// <param name="renderTabStripActions">
    /// Draws into the space a group's tab strip does not use. Children flow from the right edge, which
    /// is where an overflow or lock button belongs; <see cref="DockTabStrip.FreeArea"/> is there for
    /// anyone who would rather position content absolutely.
    /// </param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void DockSpace(this Gui gui, DockLayout layout,
        Func<string, DockPanelInfo?> panelInfo,
        Action<string, Gui> renderPanel,
        DockTheme? theme = null,
        Action<DockTabStrip, Gui>? renderTabStripActions = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var context = new DockContext(gui, layout, panelInfo, renderPanel, theme ?? DockTheme.Dark,
            renderTabStripActions);

        using (gui.Node(filePath: filePath, lineNumber: lineNumber).Expand().Enter())
        {
            // Declared before any zone so that a drop the zones do not claim falls through to here
            // and tears the panel off: overlapping targets resolve last-declared-wins.
            TearOffTarget(context, gui.CurrentNode.Rect);

            if (layout.Root is null) EmptyDockSpace(context);
            else RenderNode(context, layout.Root, "dock:root");

            RenderFloating(context);
            gui.DragGhost();

            // A close click is the one edit that has no drop to wait for. Applying it at the end of
            // the render pass keeps both passes of this frame agreeing on the tree.
            if (gui.Pass == Pass.Pass2Render && context.Closing is { } closing) layout.Remove(closing);
        }
    }

    private static void RenderNode(DockContext context, DockNode node, string path)
    {
        switch (node)
        {
            case DockSplit split:
                RenderSplit(context, split, path);
                return;
            case DockLeaf leaf:
                RenderLeaf(context, leaf, path);
                return;
        }
    }

    private static void RenderSplit(DockContext context, DockSplit split, string path)
    {
        var gui = context.Gui;
        var horizontal = split.Axis == Axis.Horizontal;

        using (gui.Node(-1, -1, path).Expand().Direction(split.Axis).Enter())
        {
            var first = gui.Node(-1, -1, $"{path}/a");
            if (horizontal) first.ExpandWidth(split.Fraction).ExpandHeight();
            else first.ExpandHeight(split.Fraction).ExpandWidth();

            using (first.Enter()) RenderNode(context, split.First, $"{path}/a");

            var fraction = split.Fraction;
            if (gui.Splitter(ref fraction, split.Axis, context.Theme.SplitterThickness,
                    color: context.Theme.Border, hoverColor: context.Theme.Hover))
            {
                split.Fraction = fraction;
                context.Layout.MarkChanged();
            }

            var second = gui.Node(-1, -1, $"{path}/b");
            if (horizontal) second.ExpandWidth(1f - split.Fraction).ExpandHeight();
            else second.ExpandHeight(1f - split.Fraction).ExpandWidth();

            using (second.Enter()) RenderNode(context, split.Second, $"{path}/b");
        }
    }

    private static void RenderLeaf(DockContext context, DockLeaf leaf, string path)
    {
        var gui = context.Gui;
        var theme = context.Theme;

        using (gui.Node(-1, -1, path).Expand().Direction(Axis.Vertical).Enter())
        {
            var leafRect = gui.CurrentNode.Rect;

            var tabs = leaf.PanelIds
                .Select(panelId => ToTabItem(context, leaf, panelId))
                .ToList();

            var outcome = gui.TabStrip(tabs, leaf.ActivePanelId, context.Theme.ToTabStripTheme(),
                $"{path}/tabs",
                g => context.RenderTabStripActions?.Invoke(
                    new DockTabStrip(leaf, leaf.ActivePanelId, g.CurrentNode.Rect), g),
                (item, id) => DragTab(context, leaf, item, id));

            if (outcome.Activated is { } activated) Activate(context, leaf, activated);
            if (outcome.Closed is { } closed) context.Closing = closed.Id;

            using (gui.Node(-1, -1, $"{path}/body").Expand().Enter())
            {
                gui.DrawBackgroundRect(theme.Panel);
                gui.ClipContent();

                if (leaf.ActivePanelId is { } activeId && context.PanelInfo(activeId) is not null)
                    context.RenderPanel(activeId, gui);
            }

            DropZones(context, leaf, leafRect);
        }
    }

    /// <summary>
    /// Hands the host whatever width the tabs left over. The node exists in both passes even without a
    /// callback, so the strip's structure does not change when a host adds or drops one.
    /// </summary>
    private static void TabStripActions(DockContext context, DockLeaf leaf, string id)
    {
        var gui = context.Gui;

        using (gui.Node(-1, context.Theme.TabHeight, id)
                   .ExpandWidth()
                   .Direction(Axis.Horizontal)
                   .ContentAlignX(1f)
                   .Enter())
        {
            context.RenderTabStripActions?.Invoke(
                new DockTabStrip(leaf, leaf.ActivePanelId, gui.CurrentNode.Rect), gui);
        }
    }

    private static TabStripItem ToTabItem(DockContext context, DockLeaf leaf, string panelId)
    {
        var info = context.PanelInfo(panelId) ?? new DockPanelInfo(panelId);
        return new TabStripItem(panelId, info.Title, info.Closable, Icon: info.Icon, Tag: leaf);
    }

    private static void Activate(DockContext context, DockLeaf leaf, TabStripItem item)
    {
        var index = leaf.PanelIds.IndexOf(item.Id);
        if (index < 0 || index == leaf.ActiveIndex) return;

        leaf.ActiveIndex = index;
        context.Layout.MarkChanged();
    }

    /// <summary>Starts a tab drag, and accepts a drop from a sibling tab as a reorder.</summary>
    private static bool DragTab(DockContext context, DockLeaf leaf, TabStripItem item, string id)
    {
        var gui = context.Gui;

        var dragging = gui.DragSource(id, new DockTabPayload(item.Id, leaf), g =>
        {
            using (g.Node(120, context.Theme.TabHeight).Enter())
            {
                g.DrawBackgroundRect(context.Theme.Accent, 3);
                g.DrawText(item.Label, context.Theme.FontSize, context.Theme.Ink);
            }
        });

        var index = leaf.PanelIds.IndexOf(item.Id);
        gui.DropTarget(gui.CurrentNode.Rect, id,
            payload => payload is DockTabPayload p && ReferenceEquals(p.Leaf, leaf) && p.PanelId != item.Id,
            payload =>
            {
                DockLayout.Reorder(leaf, leaf.PanelIds.IndexOf(((DockTabPayload)payload).PanelId), index);
                context.Layout.MarkChanged();
            });

        return dragging;
    }

    /// <summary>
    /// Hands the host whatever width the tabs left over. The node exists in both passes even without a
    /// callback, so the strip's structure does not change when a host adds or drops one.
    /// </summary>
    /// <summary>
    /// Overlays the five drop zones of a group while a tab is being dragged, and highlights the one
    /// under the cursor with a preview of the region the panel would take.
    /// </summary>
    /// <summary>
    /// While a tab is being dragged, offers the whole group as a drop target and previews where the
    /// panel would land. The edge bands take priority and everything else is the centre, so no part of
    /// a group is dead space that silently tears the panel off instead.
    /// </summary>
    private static void DropZones(DockContext context, DockLeaf leaf, Rect rect)
    {
        var gui = context.Gui;
        if (!gui.IsDragging) return;

        using (gui.Node(-1, -1, $"__dropZones_{leaf.GetHashCode()}").AbsoluteScreen(rect.X, rect.Y).Enter())
        {
            gui.SetZIndex(Gui.DragGhostZIndex - 1);

            if (gui.Pass != Pass.Pass2Render) return;
            if (rect is { W: <= 0 } or { H: <= 0 }) return;

            var zone = ZoneAt(rect, gui.Input.MousePosition, context.Theme.DropZoneFraction);

            var hovered = gui.DropTarget(rect, $"__zone_{leaf.GetHashCode()}",
                payload => payload is DockTabPayload p && !IsNoOpDrop(p, leaf, zone),
                payload => context.Layout.DockInto(((DockTabPayload)payload).PanelId, leaf, zone));

            if (!hovered) return;

            var preview = ZoneRect(rect, zone, zone == DockZone.Center ? 1f : 0.5f);
            gui.DrawRect(preview, Color.FromArgb(70, context.Theme.Accent), 2);
            gui.DrawRectBorder(preview, context.Theme.Accent, 2, 2);
        }
    }

    /// <summary>Which zone a point falls in: an edge band if it is within one, otherwise the centre.</summary>
    private static DockZone ZoneAt(Rect rect, Vector2 point, float fraction)
    {
        foreach (var zone in EdgeZones)
            if (ZoneRect(rect, zone, fraction).Contains(point))
                return zone;

        return DockZone.Center;
    }

    private static bool IsNoOpDrop(DockTabPayload payload, DockLeaf leaf, DockZone zone) =>
        zone == DockZone.Center && ReferenceEquals(payload.Leaf, leaf) && leaf.PanelIds.Count == 1;

    /// <summary>Sentinel for "no drag anchor recorded"; a real window position never reaches it.</summary>
    private static readonly Vector2 NoAnchor = new(float.NaN, float.NaN);

    private static readonly DockZone[] EdgeZones =
        [DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom];

    private static Rect ZoneRect(Rect rect, DockZone zone, float fraction) => zone switch
    {
        DockZone.Left => new Rect(rect.X, rect.Y, rect.W * fraction, rect.H),
        DockZone.Right => new Rect(rect.X + rect.W * (1f - fraction), rect.Y, rect.W * fraction, rect.H),
        DockZone.Top => new Rect(rect.X, rect.Y, rect.W, rect.H * fraction),
        DockZone.Bottom => new Rect(rect.X, rect.Y + rect.H * (1f - fraction), rect.W, rect.H * fraction),
        _ => new Rect(
            rect.X + rect.W * (1f - fraction) / 2f,
            rect.Y + rect.H * (1f - fraction) / 2f,
            rect.W * fraction,
            rect.H * fraction)
    };

    private static void TearOffTarget(DockContext context, Rect rect)
    {
        var gui = context.Gui;
        if (gui.Pass != Pass.Pass2Render || !gui.IsDragging) return;

        gui.DropTarget(rect, "__dockTearOff",
            payload => payload is DockTabPayload,
            payload =>
            {
                var panelId = ((DockTabPayload)payload).PanelId;
                var at = gui.Input.MousePosition;
                context.Layout.Float(panelId, new Rect(at.X - 60, at.Y - 12, 320, 240));
            });
    }

    private static void RenderFloating(DockContext context)
    {
        var gui = context.Gui;
        var theme = context.Theme;

        for (var i = 0; i < context.Layout.Floating.Count; i++)
        {
            var window = context.Layout.Floating[i];
            var bounds = window.Bounds;

            using (gui.Node(bounds.W, bounds.H, $"dock:float/{i}")
                       .AbsoluteScreen(bounds.X, bounds.Y)
                       .BlockInput()
                       .Direction(Axis.Vertical)
                       .Enter())
            {
                gui.SetZIndex(100 + i);
                gui.DrawBackgroundRect(theme.Panel, 3);
                gui.DrawRectBorder(gui.CurrentNode.Rect, theme.Border, 1, 3);

                using (gui.Node(-1, 18, $"dock:float/{i}/grip").ExpandWidth().Enter())
                {
                    if (gui.Pass == Pass.Pass2Render)
                    {
                        gui.DrawBackgroundRect(theme.TabStrip);

                        // Anchored to the window's position at the press, then offset by the pointer's
                        // total travel — accumulating per-frame deltas drifts away from the cursor.
                        ref var anchor = ref gui.GetValue(NoAnchor, $"dock:float/{i}/gripAnchor");

                        if (gui.GetInteractable().OnDrag(out var drag))
                        {
                            if (float.IsNaN(anchor.X)) anchor = new Vector2(bounds.X, bounds.Y);

                            var moved = anchor + drag.TotalDelta;
                            if (moved.X != bounds.X || moved.Y != bounds.Y)
                            {
                                window.Bounds = bounds with { X = moved.X, Y = moved.Y };
                                context.Layout.MarkChanged();
                            }
                        }
                        else
                        {
                            anchor = NoAnchor;
                        }
                    }
                }

                using (gui.Node(-1, -1, $"dock:float/{i}/body").Expand().Enter())
                    RenderNode(context, window.Root, $"dock:float/{i}/root");
            }
        }
    }

    private static void EmptyDockSpace(DockContext context)
    {
        var gui = context.Gui;

        using (gui.Node(-1, -1, "dock:empty").Expand().Enter())
        {
            gui.DrawBackgroundRect(context.Theme.TabStrip);
            gui.DrawText("No panels open", context.Theme.FontSize, context.Theme.InkDim);
        }
    }

    /// <summary>
    /// The per-frame plumbing a dock render needs, plus the one deferred edit that cannot wait for a
    /// drop: a close click, applied after the frame so the tree does not change between passes.
    /// </summary>
    private sealed class DockContext(
        Gui gui,
        DockLayout layout,
        Func<string, DockPanelInfo?> panelInfo,
        Action<string, Gui> renderPanel,
        DockTheme theme,
        Action<DockTabStrip, Gui>? renderTabStripActions)
    {
        public Gui Gui { get; } = gui;
        public DockLayout Layout { get; } = layout;
        public Func<string, DockPanelInfo?> PanelInfo { get; } = panelInfo;
        public Action<string, Gui> RenderPanel { get; } = renderPanel;
        public DockTheme Theme { get; } = theme;
        public Action<DockTabStrip, Gui>? RenderTabStripActions { get; } = renderTabStripActions;
        public string? Closing { get; set; }
    }
}
