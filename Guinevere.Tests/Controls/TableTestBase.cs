using System.Text.RegularExpressions;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// What the table tests share: the frame loop that draws a table over a source, readers of the tree it built,
/// ready-made columns, and the fake sources and renderers the tests read and count.
/// </summary>
public abstract class TableTestBase
{
    protected const int Width = 400;
    protected const int Height = 400;
    protected static readonly TableTheme Theme = new() { RowHeight = 24f, HeaderHeight = 28f };

    protected static IReadOnlyList<string> Modifiers(Gui gui, string column) =>
        gui.RootNode!.FindChildById($"table/header/{column}")!.StyleTarget?.Modifiers ?? [];

    /// <summary>The width of the text node a cell drew, which follows the text it shows.</summary>
    protected static float TextWidth(Gui gui, int row, string column) =>
        gui.RootNode!.FindChildById($"table/row{row}/{column}")!.Children[0].Rect.W;

    /// <summary>Columns whose values are <c>r{row}/c{n}</c> strings, counting how often they are read.</summary>
    protected static TableColumns<int> Columns(int count, Counter? counter = null) =>
        new(Enumerable.Range(0, count).Select(i => Make(i, counter, null)));

    protected static TableColumns<int> Fixed(int count, float width) =>
        new(Enumerable.Range(0, count).Select(i => Make(i, null, width)));

    protected static TableColumn<int> Make(int index, Counter? counter, float? width) =>
        TableColumn<int>.Of($"Column {index}", row =>
        {
            if (counter is not null)
            {
                counter.Reads++;
            }

            return $"r{row}/c{index}";
        }, id: $"c{index}", width: width);

    protected static Gui RunFrames<TRow>(ITableViewSource<TRow> source, TableColumns<TRow> columns, TableState? state = null,
        TableOptions? options = null, int frames = 1, Gui? reuse = null, Action? betweenPasses = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        state ??= new TableState();

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        var gui = reuse ?? new TestableGui { Input = input };
        gui.Input = input;
        if (gui is TestableGui testable)
        {
            testable.SetScreenRect(Width, Height);
        }

        for (var frame = 0; frame < frames; frame++)
        {
            void Draw() => gui.Table(state, source, columns, options, Theme);

            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            betweenPasses?.Invoke();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.Render();
            gui.EndFrame();
        }

        return gui;
    }

    protected static List<int> RowIndices(Gui gui)
    {
        var indices = new List<int>();
        Visit(gui.RootNode!);
        return indices;

        void Visit(LayoutNode node)
        {
            var match = Regex.Match(node.Id, @"^table/row(\d+)$");
            if (match.Success)
            {
                indices.Add(int.Parse(match.Groups[1].Value));
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }
    }

    /// <summary>The column ids of the cells built in one row.</summary>
    protected static List<string> CellIds(Gui gui, int row)
    {
        var ids = new List<string>();
        var prefix = $"table/row{row}/";
        Visit(gui.RootNode!);
        return ids;

        void Visit(LayoutNode node)
        {
            // Cell nodes only: a cell's own children extend its id, and the spacers are not cells.
            if (node.Id.StartsWith(prefix, StringComparison.Ordinal) && !node.Id[prefix.Length..].Contains('/')
                && !node.Id.EndsWith("padLeft", StringComparison.Ordinal)
                && !node.Id.EndsWith("padRight", StringComparison.Ordinal))
            {
                ids.Add(node.Id[prefix.Length..]);
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }
    }

    protected sealed class Counter
    {
        public int Reads { get; set; }
    }

    protected sealed record Item(string Version, string FileName, int Downloads);

    /// <summary>Rows are their own indices; counts how often the table reads one.</summary>
    /// <summary>
    /// A source that follows the frame contract: changes are set aside in <see cref="Pending"/> and published at
    /// <see cref="BeginFrame"/>, once per frame number. It counts what the table asks of it, and can also misbehave
    /// on purpose by changing its count in the middle of a frame.
    /// </summary>
    protected sealed class PublishingSource : ITableViewSource<int>
    {
        long _lastFrame = -1;
        int _published;
        int? _live;
        EventHandler? _handlers;

        public int Pending { get; set; }

        public int BeginFrameCalls { get; set; }

        public int Publications { get; set; }

        public int Subscribers { get; private set; }

        public int RowCount => _live ?? _published;

        public event EventHandler? Invalidated
        {
            add
            {
                Subscribers++;
                _handlers += value;
            }
            remove
            {
                Subscribers--;
                _handlers -= value;
            }
        }

        public int this[int index] => index;

        public void BeginFrame(long frame)
        {
            BeginFrameCalls++;
            if (frame == _lastFrame)
            {
                return;
            }

            _lastFrame = frame;
            _published = Pending;
            _live = null;
            Publications++;
        }

        public RowIdentity GetRowIdentity(int index) => RowIdentity.UnstableIndex(index);

        public void ChangeNowBehindTheTablesBack(int count) => _live = count;

        public void RaiseInvalidated() => _handlers?.Invoke(this, EventArgs.Empty);
    }

    protected sealed class GridSource(int rows) : ITableViewSource<int>
    {
        public int Reads { get; set; }

        public bool Loading { get; init; }

        public int RowCount => rows;

        public bool IsLoading => Loading;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public int this[int index]
        {
            get
            {
                Reads++;
                return index;
            }
        }

        public RowIdentity GetRowIdentity(int index) => RowIdentity.UnstableIndex(index);
    }

    protected sealed class ItemSource(Item[] items) : ITableViewSource<Item>
    {
        public int RowCount => items.Length;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public Item this[int index] => items[index];

        public RowIdentity GetRowIdentity(int index) => new(items[index].FileName);
    }

    protected sealed class RecordingRenderer : ITableCellRenderer<int, string>
    {
        public List<TableCellContext<int, string>> Cells { get; } = [];

        public void Render(Gui gui, in TableCellContext<int, string> cell)
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                Cells.Add(cell);
            }
        }
    }

    /// <summary>Receives the row itself, as a renderer-only column does.</summary>
    protected sealed class ItemRenderer : ITableCellRenderer<Item, Item>
    {
        public List<string> Seen { get; } = [];

        public void Render(Gui gui, in TableCellContext<Item, Item> cell)
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                Seen.Add(cell.Value.FileName);
            }
        }
    }

    protected sealed class RecordingHeader : ITableHeaderContent
    {
        public List<TableHeaderContext> Seen { get; } = [];

        public void Render(Gui gui, in TableHeaderContext context)
        {
            Seen.Add(context);
            gui.DrawText("icon", context.FontSize);
        }
    }
}
