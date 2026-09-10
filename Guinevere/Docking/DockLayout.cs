namespace Guinevere;

/// <summary>
/// The arrangement <c>gui.DockSpace</c> renders: a tree of docked regions plus any floating
/// windows. It is a plain model with no dependency on <see cref="Gui"/>, so a host can seed, edit and
/// persist a layout without rendering one.
/// </summary>
public sealed class DockLayout
{
    /// <summary>
    /// The schema version written by <see cref="ToJson"/>. <see cref="FromJson"/> refuses anything
    /// else, so a host that has changed the format re-seeds instead of loading a stale tree.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The version this layout was created with.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// The docked tree, or null when every panel is floating or closed.
    /// </summary>
    public DockNode? Root { get; set; }

    /// <summary>
    /// The floating windows, drawn over the docked tree in list order.
    /// </summary>
    public List<DockFloat> Floating { get; } = [];

    /// <summary>
    /// Bumped by every edit. A host watches this to decide whether the layout is worth persisting,
    /// rather than re-serializing it each frame to find out.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Records that something changed. Call it after editing a <see cref="DockSplit.Fraction"/> or a
    /// <see cref="DockFloat.Bounds"/> directly, which the layout cannot observe for itself.
    /// </summary>
    public void MarkChanged() => Revision++;

    /// <summary>
    /// Every leaf in the layout, docked first and then floating.
    /// </summary>
    public IEnumerable<DockLeaf> Leaves()
    {
        if (Root is not null)
            foreach (var leaf in Root.Leaves())
                yield return leaf;

        foreach (var window in Floating)
        foreach (var leaf in window.Root.Leaves())
            yield return leaf;
    }

    /// <summary>
    /// Every panel id in the layout, in leaf order.
    /// </summary>
    public IEnumerable<string> PanelIds => Leaves().SelectMany(leaf => leaf.PanelIds);

    /// <summary>
    /// Whether the layout holds this panel.
    /// </summary>
    public bool Contains(string panelId) => FindLeaf(panelId) is not null;

    /// <summary>
    /// The leaf holding this panel, or null when the layout does not.
    /// </summary>
    public DockLeaf? FindLeaf(string panelId) => Leaves().FirstOrDefault(leaf => leaf.PanelIds.Contains(panelId));

    /// <summary>
    /// Brings a panel to the front of its tab group.
    /// </summary>
    public void Activate(string panelId)
    {
        MarkChanged();
        var leaf = FindLeaf(panelId);
        if (leaf is null) return;

        leaf.ActiveIndex = leaf.PanelIds.IndexOf(panelId);
    }

    /// <summary>
    /// Moves a tab within its group.
    /// </summary>
    /// <param name="leaf">The group to reorder.</param>
    /// <param name="from">The tab's current index.</param>
    /// <param name="to">The index to move it to.</param>
    public static void Reorder(DockLeaf leaf, int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= leaf.PanelIds.Count) return;

        to = Math.Clamp(to, 0, leaf.PanelIds.Count - 1);
        var active = leaf.ActivePanelId;

        var panelId = leaf.PanelIds[from];
        leaf.PanelIds.RemoveAt(from);
        leaf.PanelIds.Insert(to, panelId);

        if (active is not null) leaf.ActiveIndex = leaf.PanelIds.IndexOf(active);
    }

    /// <summary>
    /// Takes a panel out of the layout, collapsing any group and split it leaves empty.
    /// </summary>
    /// <returns>True if the panel was there to remove.</returns>
    public bool Remove(string panelId)
    {
        MarkChanged();
        var leaf = FindLeaf(panelId);
        if (leaf is null) return false;

        var active = leaf.ActivePanelId;
        leaf.PanelIds.Remove(panelId);
        if (active is not null && active != panelId) leaf.ActiveIndex = leaf.PanelIds.IndexOf(active);

        Prune();
        return true;
    }

    /// <summary>
    /// Docks a panel onto an existing group: into its tabs for <see cref="DockZone.Center"/>, or into
    /// a new split beside it for an edge zone. The panel is detached from wherever it was first, so
    /// this doubles as a move.
    /// </summary>
    /// <param name="panelId">The panel to dock.</param>
    /// <param name="target">The group to dock against. Must already be in this layout.</param>
    /// <param name="zone">Which side of the target the panel takes.</param>
    /// <param name="fraction">The share the incoming panel takes of an edge split.</param>
    public void DockInto(string panelId, DockLeaf target, DockZone zone, float fraction = 0.5f)
    {
        MarkChanged();
        if (target.PanelIds.Count == 1 && target.PanelIds[0] == panelId) return;

        Remove(panelId);

        // Removing the panel may have pruned the target away - a group that held only it.
        if (!Leaves().Contains(target))
        {
            EnsureRoot(new DockLeaf(panelId));
            return;
        }

        if (zone == DockZone.Center)
        {
            target.PanelIds.Add(panelId);
            target.ActiveIndex = target.PanelIds.Count - 1;
            return;
        }

        var incoming = new DockLeaf(panelId);
        var axis = zone is DockZone.Left or DockZone.Right ? Axis.Horizontal : Axis.Vertical;
        var incomingFirst = zone is DockZone.Left or DockZone.Top;

        var split = incomingFirst
            ? new DockSplit(axis, incoming, target, fraction)
            : new DockSplit(axis, target, incoming, 1f - fraction);

        Replace(target, split);
    }

    /// <summary>
    /// Docks a panel against an edge of the whole layout, which is how a host seeds a layout from
    /// panel placements before any user has arranged one.
    /// </summary>
    /// <param name="panelId">The panel to dock.</param>
    /// <param name="zone">The edge to dock against. <see cref="DockZone.Center"/> joins the centre group.</param>
    /// <param name="fraction">The share of the layout the panel takes.</param>
    public void DockAtEdge(string panelId, DockZone zone, float fraction = 0.25f)
    {
        MarkChanged();
        Remove(panelId);

        // Reuse the group already seeded against this edge, so three Left panels produce one column
        // of tabs rather than three nested columns.
        if (EdgeLeaf(zone) is { } existing)
        {
            existing.PanelIds.Add(panelId);
            existing.ActiveIndex = existing.PanelIds.Count - 1;
            return;
        }

        var incoming = new DockLeaf(panelId) { Zone = zone };

        if (Root is null || zone == DockZone.Center)
        {
            Root = Root is null ? incoming : new DockSplit(Axis.Horizontal, Root, incoming);
            return;
        }

        var axis = zone is DockZone.Left or DockZone.Right ? Axis.Horizontal : Axis.Vertical;
        var incomingFirst = zone is DockZone.Left or DockZone.Top;

        Root = incomingFirst
            ? new DockSplit(axis, incoming, Root, fraction)
            : new DockSplit(axis, Root, incoming, 1f - fraction);
    }

    /// <summary>
    /// Tears a panel out into its own floating window.
    /// </summary>
    /// <param name="panelId">The panel to float.</param>
    /// <param name="bounds">The window's screen-space rect.</param>
    /// <returns>The new window.</returns>
    public DockFloat Float(string panelId, Rect bounds)
    {
        MarkChanged();
        Remove(panelId);

        var window = new DockFloat(new DockLeaf(panelId), bounds);
        Floating.Add(window);
        return window;
    }

    /// <summary>
    /// Adds a panel at the given edge if the layout does not already hold it. Hosts call this to
    /// reopen a closed panel and to fold newly registered panels into a layout loaded from disk.
    /// </summary>
    public void EnsurePanel(string panelId, DockZone zone = DockZone.Center, float fraction = 0.25f)
    {
        if (Contains(panelId)) return;

        DockAtEdge(panelId, zone, fraction);
        Activate(panelId);
    }

    /// <summary>
    /// Drops panels the host no longer knows about — a layout on disk outlives the plugin that
    /// contributed its panels.
    /// </summary>
    /// <param name="known">The panel ids that still resolve.</param>
    public void RemoveUnknown(IReadOnlyCollection<string> known)
    {
        foreach (var panelId in PanelIds.Where(id => !known.Contains(id)).ToList())
            Remove(panelId);
    }

    private DockLeaf? EdgeLeaf(DockZone zone)
    {
        return Root?.Leaves().FirstOrDefault(leaf => leaf.Zone == zone);
    }

    private void EnsureRoot(DockNode node)
    {
        if (Root is null) Root = node;
        else Root = new DockSplit(Axis.Horizontal, Root, node);
    }

    /// <summary>
    /// Swaps one node for another, wherever it sits. Walks explicitly and stops at the first match: a
    /// lazy walk would find the replacement still holding <paramref name="target"/> and splice a split
    /// in as its own child — unbounded recursion on every later traversal.
    /// </summary>
    private bool Replace(DockNode target, DockNode replacement)
    {
        if (ReferenceEquals(Root, target))
        {
            Root = replacement;
            return true;
        }

        foreach (var window in Floating)
        {
            if (ReferenceEquals(window.Root, target))
            {
                window.Root = replacement;
                return true;
            }

            if (ReplaceChild(window.Root, target, replacement)) return true;
        }

        return Root is not null && ReplaceChild(Root, target, replacement);
    }

    private static bool ReplaceChild(DockNode node, DockNode target, DockNode replacement)
    {
        if (node is not DockSplit split) return false;

        if (ReferenceEquals(split.First, target))
        {
            split.First = replacement;
            return true;
        }

        if (ReferenceEquals(split.Second, target))
        {
            split.Second = replacement;
            return true;
        }

        return ReplaceChild(split.First, target, replacement)
               || ReplaceChild(split.Second, target, replacement);
    }

    private void Prune()
    {
        Root = Prune(Root);

        for (var i = Floating.Count - 1; i >= 0; i--)
        {
            var pruned = Prune(Floating[i].Root);
            if (pruned is null) Floating.RemoveAt(i);
            else Floating[i].Root = pruned;
        }
    }

    private static DockNode? Prune(DockNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case DockLeaf leaf:
                return leaf.PanelIds.Count == 0 ? null : leaf;
            case DockSplit split:
            {
                var first = Prune(split.First);
                var second = Prune(split.Second);

                if (first is null) return second;
                if (second is null) return first;

                split.First = first;
                split.Second = second;
                return split;
            }
            default:
                return node;
        }
    }

    /// <summary>
    /// Serializes the layout, including <see cref="Version"/>.
    /// </summary>
    public string ToJson() => DockLayoutSerializer.Serialize(this);

    /// <summary>
    /// Reads a layout back. Returns null for malformed JSON or a version this build does not write,
    /// which the host should treat as "no saved layout" and re-seed.
    /// </summary>
    public static DockLayout? FromJson(string json) => DockLayoutSerializer.Deserialize(json);
}
