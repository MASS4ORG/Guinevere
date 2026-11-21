namespace Guinevere.Charts;

/// <summary>Derives flame chart rows from source-indexed parent links.</summary>
public static class FlameLayout
{
    /// <summary>Writes the source indices from root to selection. A short destination receives no partial path.</summary>
    /// <returns>The required count, or zero if the selection or its parent links are invalid.</returns>
    public static int WritePath(ReadOnlySpan<int> parents, int selected, Span<int> destination)
    {
        var count = 0;
        var current = selected;
        while (current != -1)
        {
            if ((uint)current >= (uint)parents.Length || count >= parents.Length)
            {
                return 0;
            }
            count++;
            current = parents[current];
        }
        if (destination.Length < count || parents.Overlaps(destination))
        {
            return count;
        }
        current = selected;
        for (var i = count - 1; i >= 0; i--)
        {
            destination[i] = current;
            current = parents[current];
        }
        return count;
    }

    /// <summary>Finds case-insensitive label matches in source order, returning the required destination count.</summary>
    public static int Search(ReadOnlySpan<string> labels, ReadOnlySpan<char> query, Span<int> destination)
    {
        if (query.IsEmpty)
        {
            return 0;
        }
        var count = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            if (!labels[i].AsSpan().Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (count < destination.Length)
            {
                destination[count] = i;
            }
            count++;
        }
        return count;
    }

    /// <summary>Writes zero-based depths for parents in any order. A parent index of -1 indicates a root.</summary>
    /// <remarks>Returns false for invalid parent links, cycles or overlapping buffers. The output is then unspecified.</remarks>
    public static bool TryCalculateDepths(ReadOnlySpan<int> parents, Span<int> depths)
    {
        if (depths.Length < parents.Length || parents.Overlaps(depths))
        {
            return false;
        }
        const int Unvisited = -1;
        depths[..parents.Length].Fill(Unvisited);
        for (var index = 0; index < parents.Length; index++)
        {
            if (depths[index] == Unvisited && !ResolveDepth(parents, depths, index))
            {
                return false;
            }
        }
        return true;
    }

    static bool ResolveDepth(ReadOnlySpan<int> parents, Span<int> depths, int index)
    {
        const int Visiting = -2;
        var current = index;
        var distance = 0;
        while (current != -1)
        {
            if ((uint)current >= (uint)parents.Length || depths[current] == Visiting)
            {
                return false;
            }
            if (depths[current] >= 0)
            {
                break;
            }
            depths[current] = Visiting;
            distance++;
            current = parents[current];
        }
        var depth = (current == -1 ? -1 : depths[current]) + distance;
        while (index != current)
        {
            depths[index] = depth--;
            index = parents[index];
        }
        return true;
    }
}
