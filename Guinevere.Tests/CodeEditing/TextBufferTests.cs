namespace Guinevere.Tests.CodeEditing;

/// <summary>The buffer contract, run against the string and tree implementations alike.</summary>
public class TextBufferTests
{
    public static TheoryData<string> Kinds => ["string", "tree"];

    static ITextBuffer Create(string kind, string text) =>
        kind == "string" ? new StringTextBuffer(text) : new SumTreeTextBuffer(text);

    [Theory]
    [MemberData(nameof(Kinds))]
    public void LinesAreFoundByNumberAndByOffset(string kind)
    {
        var buffer = Create(kind, "alpha\nbe\n\ngamma");

        Assert.Equal(4, buffer.LineCount);
        Assert.Equal([0, 6, 9, 10], Enumerable.Range(0, 4).Select(buffer.LineStart));
        Assert.Equal([5, 2, 0, 5], Enumerable.Range(0, 4).Select(buffer.LineLength));
        Assert.Equal(0, buffer.LineOf(5));
        Assert.Equal(1, buffer.LineOf(6));
        Assert.Equal(2, buffer.LineOf(9));
        Assert.Equal(3, buffer.LineOf(buffer.Length));
        Assert.Equal("be", buffer.Line(1));
        Assert.Equal(buffer.Text, buffer.ToString());
        Assert.Equal('g', buffer[10]);
        Assert.Equal("a\nbe", buffer.GetText(4, 4));
    }

    /// <summary>Buffer implementations can inherit complete-text and line access from the interface.</summary>
    [Fact]
    public void DefaultInterfaceMembersReadTheUnderlyingBuffer()
    {
        ITextBuffer buffer = new DefaultMemberBuffer("a\nb");

        Assert.Equal("a\nb", buffer.Text);
        Assert.Equal("b", buffer.Line(1));
    }

    /// <summary>A forwarding buffer that inherits the interface's text and line convenience members.</summary>
    sealed class DefaultMemberBuffer(string text) : ITextBuffer
    {
        readonly ITextBuffer _buffer = new StringTextBuffer(text);

        /// <inheritdoc />
        public int Length => _buffer.Length;

        /// <inheritdoc />
        public int LineCount => _buffer.LineCount;

        /// <inheritdoc />
        public char this[int offset] => _buffer[offset];

        /// <inheritdoc />
        public int LineStart(int line) => _buffer.LineStart(line);

        /// <inheritdoc />
        public int LineLength(int line) => _buffer.LineLength(line);

        /// <inheritdoc />
        public int LineOf(int offset) => _buffer.LineOf(offset);

        /// <inheritdoc />
        public string GetText(int start, int length) => _buffer.GetText(start, length);

        /// <inheritdoc />
        public ITextBuffer Replace(int start, int length, string value) => _buffer.Replace(start, length, value);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EditsReturnNewVersionsAndLeaveTheOldOneIntact(string kind)
    {
        var original = Create(kind, "one\ntwo");

        var edited = original.Replace(3, 1, " and ").Replace(0, 0, "zero\r\n");

        Assert.Equal("one\ntwo", original.Text);
        Assert.Equal("zero\none and two", edited.Text);
        Assert.Equal(2, edited.LineCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => original.Replace(5, 9, ""));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void LineBreaksAreNormalizedAndAnEmptyBufferHasOneLine(string kind)
    {
        var buffer = Create(kind, "a\r\nb\rc");
        var empty = Create(kind, "").Replace(0, 0, "x").Replace(0, 1, "");

        Assert.Equal("a\nb\nc", buffer.Text);
        Assert.Equal(1, empty.LineCount);
        Assert.Equal(0, empty.Length);
        Assert.Equal("", empty.Line(0));
        Assert.Equal("\r\n", TextBufferText.DetectLineBreak("a\r\nb\r\nc\n"));
        Assert.Equal("\n", TextBufferText.DetectLineBreak("a\nb"));
    }

    [Fact]
    public void TreeBufferRejectsInvalidRangesIncludingEmptyReadsAndOverflowingEdits()
    {
        var buffer = new SumTreeTextBuffer("abc\n");

        Assert.Equal(buffer.Text, buffer.ToString());
        Assert.Equal("", buffer.GetText(buffer.Length, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetText(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetText(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetText(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetText(3, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Replace(3, int.MaxValue, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Replace(5, 0, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.LineStart(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.LineStart(2));
        Assert.Equal(0, buffer.LineOf(-1));
        Assert.Equal(1, buffer.LineOf(int.MaxValue));
    }

    [Fact]
    public void EditorTypesBelongToTheStandalonePackage()
    {
        var assembly = typeof(TextDocument).Assembly;

        Assert.Equal("Guinevere.Excalibur.TextEditor", assembly.GetName().Name);
        Assert.Equal(assembly, typeof(TextEditorState).Assembly);
        Assert.Equal(assembly, typeof(TextEditorGui).Assembly);
        Assert.Equal(assembly, typeof(SumTreeTextBuffer).Assembly);
        Assert.DoesNotContain(typeof(Gui).Assembly.GetReferencedAssemblies(), name => name.Name == "SumTree");
        Assert.DoesNotContain(typeof(ExcaliburStyles).Assembly.GetReferencedAssemblies(), name => name.Name == "SumTree");
    }

    [Fact]
    public void TreeBufferMatchesAStringUnderRandomEdits()
    {
        var random = new Random(3);
        var model = string.Join("\n", Enumerable.Range(0, 300).Select(i => $"line {i}"));
        ITextBuffer tree = new SumTreeTextBuffer(model);
        for (var i = 0; i < 400; i++)
        {
            var start = random.Next(model.Length + 1);
            var length = random.Next(Math.Min(20, model.Length - start) + 1);
            var text = random.Next(3) == 0 ? "\n" : new string((char)('a' + random.Next(26)), random.Next(4));
            model = model.Remove(start, length).Insert(start, text);
            tree = tree.Replace(start, length, text);
        }

        var lines = model.Split('\n');
        Assert.Equal(model, tree.Text);
        Assert.Equal(lines.Length, tree.LineCount);
        for (var line = 0; line < lines.Length; line += 7)
        {
            Assert.Equal(lines[line], tree.Line(line));
            Assert.Equal(line, tree.LineOf(tree.LineStart(line)));
        }
    }

    [Fact]
    public void TreeBufferEditsLargeDocumentsQuickly()
    {
        var text = string.Join("\n", Enumerable.Range(0, 200_000).Select(i => $"    value_{i} = compute({i});"));
        ITextBuffer buffer = new SumTreeTextBuffer(text);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
        {
            var line = i * 199 % buffer.LineCount;
            buffer = buffer.Replace(buffer.LineStart(line), 0, "x");
            _ = buffer.Line(buffer.LineOf(buffer.Length / 2));
        }

        watch.Stop();
        Assert.Equal(200_000, buffer.LineCount);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"1000 edits took {watch.ElapsedMilliseconds} ms");
    }
}
