namespace Guinevere;

/// <summary>
/// A buffer over one string, for small texts such as input boxes: reads are direct, and every edit copies the whole
/// text. Large documents should use a tree-backed buffer, whose edits cost O(log n).
/// </summary>
public sealed class StringTextBuffer : ITextBuffer
{
    readonly string _text;
    int[]? _lineStarts;

    /// <summary>Creates a buffer holding <paramref name="text"/> with its line breaks normalized.</summary>
    public StringTextBuffer(string text = "") => _text = TextBufferText.NormalizeLineBreaks(text);

    /// <inheritdoc />
    public int Length => _text.Length;

    /// <inheritdoc />
    public int LineCount => LineStarts.Length;

    /// <inheritdoc />
    public char this[int offset] => _text[offset];

    /// <inheritdoc />
    public string Text => _text;

    int[] LineStarts => _lineStarts ??= FindLineStarts(_text);

    /// <inheritdoc />
    public int LineStart(int line) => LineStarts[line];

    /// <inheritdoc />
    public int LineLength(int line) =>
        (line + 1 < LineStarts.Length ? LineStarts[line + 1] - 1 : _text.Length) - LineStarts[line];

    /// <inheritdoc />
    public int LineOf(int offset)
    {
        var index = Array.BinarySearch(LineStarts, Math.Clamp(offset, 0, _text.Length));
        return index >= 0 ? index : ~index - 1;
    }

    /// <inheritdoc />
    public string GetText(int start, int length) => _text.Substring(start, length);

    /// <inheritdoc />
    public ITextBuffer Replace(int start, int length, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, _text.Length);
        return new StringTextBuffer(string.Concat(_text.AsSpan(0, start), TextBufferText.NormalizeLineBreaks(text),
            _text.AsSpan(start + length)));
    }

    /// <inheritdoc />
    public override string ToString() => _text;

    static int[] FindLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = text.IndexOf('\n'); i >= 0; i = text.IndexOf('\n', i + 1))
        {
            starts.Add(i + 1);
        }

        return [.. starts];
    }
}
