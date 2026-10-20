using SumTree;
using TextTree = SumTree.SumTree<char, SumTree.LineNumberSummary, Guinevere.EditorLineSummaryOperations>;

namespace Guinevere;

/// <summary>
/// A persistent rope with logarithmic line lookups and boundary edits, plus the cost of inserted or copied text.
/// Each edit shares unchanged text with the previous version, making it suited to large documents.
/// </summary>
public sealed class SumTreeTextBuffer : ITextBuffer
{
    readonly TextTree _tree;

    /// <summary>Creates a buffer holding <paramref name="text"/> with its line breaks normalized.</summary>
    public SumTreeTextBuffer(string text = "") =>
        _tree = new TextTree(TextBufferText.NormalizeLineBreaks(text).AsMemory());

    SumTreeTextBuffer(TextTree tree) => _tree = tree;

    /// <inheritdoc />
    public int Length => (int)_tree.Length;

    /// <inheritdoc />
    public int LineCount => _tree.Summary.Lines + 1;

    /// <inheritdoc />
    public char this[int offset] => _tree[(long)offset];

    /// <inheritdoc />
    public string Text => _tree.ToString();

    /// <inheritdoc />
    public int LineStart(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        if (line == 0)
        {
            return 0;
        }

        var lineBreak = _tree.FindPosition(new LineTarget(line));
        if (lineBreak < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        return (int)lineBreak + 1;
    }

    /// <inheritdoc />
    public int LineLength(int line) =>
        (line + 1 < LineCount ? LineStart(line + 1) - 1 : Length) - LineStart(line);

    /// <inheritdoc />
    public int LineOf(int offset)
    {
        offset = Math.Clamp(offset, 0, Length);
        return _tree.GetPrefixSummary(offset).Lines;
    }

    /// <inheritdoc />
    public string GetText(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
        return string.Create(length, (_tree, start),
            static (destination, state) => state._tree.CopyTo(state.start, destination));
    }

    /// <inheritdoc />
    public ITextBuffer Replace(int start, int length, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
        var tree = length > 0 ? _tree.RemoveRange(start, length) : _tree;
        var inserted = TextBufferText.NormalizeLineBreaks(text);
        return new SumTreeTextBuffer(inserted.Length > 0 ? tree.InsertRange(start, inserted.AsMemory()) : tree);
    }

    /// <inheritdoc />
    public override string ToString() => Text;

    readonly struct LineTarget(int line) : ISummarySeek<LineNumberSummary>
    {
        /// <inheritdoc />
        public bool IsReached(in LineNumberSummary summary) => summary.Lines >= line;
    }
}

readonly struct EditorLineSummaryOperations : ISummaryOperations<char, LineNumberSummary>
{
    /// <inheritdoc />
    public static LineNumberSummary Identity => LineNumberSummary.Zero;

    /// <inheritdoc />
    public static LineNumberSummary SummarizeElement(char element) => LineNumberSummary.FromCharacter(element);

    /// <inheritdoc />
    public static LineNumberSummary SummarizeSpan(ReadOnlySpan<char> elements) =>
        LineNumberSummary.SummarizeSpan(elements);

    /// <inheritdoc />
    public static LineNumberSummary Combine(in LineNumberSummary left, in LineNumberSummary right) => left + right;
}
