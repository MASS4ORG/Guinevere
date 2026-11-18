namespace Guinevere;

/// <summary>
/// Resolution of <see cref="Size.PercentOf"/> terms. A reference reads the referenced node's rect: final when
/// that node was already placed in the current layout run, otherwise from the previous run or frame. Layout reruns in
/// the same frame until every value read matches the size its node ended the run with, so acyclic references settle
/// without an extra frame and cycles fail with the nodes involved.
/// </summary>
public partial class LayoutNode
{
    /// <summary>Most layout runs one frame may take to settle its node references.</summary>
    internal const int MaxReferenceRuns = 8;

    /// <summary>One size read through a node reference during a layout run.</summary>
    /// <param name="Reader">The node whose value held the reference.</param>
    /// <param name="Target">The referenced node.</param>
    /// <param name="Horizontal">Whether the width was read; otherwise the height.</param>
    /// <param name="Value">The size that was read.</param>
    internal readonly record struct ReferenceRead(LayoutNode Reader, LayoutNode Target, bool Horizontal, float Value);

    /// <summary>The frame generation this node was last attached in.</summary>
    int _buildGeneration;

    /// <summary>The layout run that last placed this node.</summary>
    int _layoutRun;

    /// <summary>Assigns the node's rect for the current layout run.</summary>
    void Place(Rect rect)
    {
        _rect = rect;
        _layoutRun = _gui.LayoutRun;
    }

    /// <summary>Records that this node belongs to the frame being built.</summary>
    void StampBuildGeneration() => _buildGeneration = _gui.BuildGeneration;

    /// <summary>Sums the referenced node sizes of <paramref name="size"/> on one axis.</summary>
    float ResolveReferences(Size size, bool horizontal)
    {
        var references = size.References;
        var total = 0f;
        foreach (var reference in references)
        {
            total += reference.Fraction * ReadReferencedSize(reference.Node, horizontal);
        }

        return total;
    }

    float ReadReferencedSize(LayoutNode target, bool horizontal)
    {
        if (!ReferenceEquals(target._gui, _gui) || target._buildGeneration != _gui.BuildGeneration)
        {
            throw new InvalidOperationException(
                $"Node '{Id}' sizes from node '{target.Id}', which is not part of the current frame. " +
                "Reference a node built in the same frame, before layout runs.");
        }

        if (ReferenceEquals(target, this))
        {
            throw new InvalidOperationException(
                $"Node '{Id}' sizes its {(horizontal ? "width" : "height")} from itself. " +
                "Use a ratio for the other axis or a size of another node.");
        }

        var value = horizontal ? target._rect.W : target._rect.H;
        _gui.ReferenceReads.Add(new ReferenceRead(this, target, horizontal, value));
        return value;
    }

    /// <summary>Lays the tree out, repeating the run while node references read sizes that later changed.</summary>
    void RunReferenceAwareLayout()
    {
        for (var run = 1; ; run++)
        {
            _gui.LayoutRun++;
            _gui.ReferenceReads.Clear();
            InitializeRootLayout();
            if (ReferencesSettled())
            {
                return;
            }

            if (run == MaxReferenceRuns)
            {
                throw new InvalidOperationException(DescribeUnsettledReferences());
            }
        }
    }

    bool ReferencesSettled()
    {
        foreach (var read in _gui.ReferenceReads)
        {
            if (!SameSize(read.Value, read.Horizontal ? read.Target._rect.W : read.Target._rect.H))
            {
                return false;
            }
        }

        return true;
    }

    static bool SameSize(float a, float b) => MathF.Abs(a - b) <= 0.01f;

    string DescribeUnsettledReferences()
    {
        var builder = new System.Text.StringBuilder(
            $"Node size references did not settle after {MaxReferenceRuns} layout runs; a size depends on itself:");
        foreach (var read in _gui.ReferenceReads)
        {
            var current = read.Horizontal ? read.Target._rect.W : read.Target._rect.H;
            if (!SameSize(read.Value, current))
            {
                builder.Append($" '{read.Reader.Id}' reads the {(read.Horizontal ? "width" : "height")} of " +
                               $"'{read.Target.Id}' ({read.Value:0.##} then {current:0.##});");
            }
        }

        return builder.Append(" break the cycle with a fixed, content or parent-relative size.").ToString();
    }
}
