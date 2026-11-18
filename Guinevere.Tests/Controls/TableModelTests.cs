using System.Runtime.CompilerServices;
using Guinevere.Tests.Mocks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Guinevere.Tests.Controls;

/// <summary>Covers the table's data model: column definitions and keys, and row identities.</summary>
public class TableModelTests
{
    [Fact]
    public void ARowIdentityFromAnIndexIsItsInvariantNumber()
    {
        Assert.Equal("42", RowIdentity.FromIndex(42).Key);
        Assert.Equal("42", RowIdentity.FromIndex(42).ToString());
        Assert.Equal(RowIdentity.FromIndex(7), new RowIdentity("7"));
    }

    [Fact]
    public void AColumnNeedsATitleAndAnIdThatIsNotEmpty()
    {
        Assert.ThrowsAny<ArgumentException>(static () => new TableColumn("Title", new TableColumnId("")));
        Assert.ThrowsAny<ArgumentException>(static () => new TableColumn("Title", default(TableColumnId)));
        Assert.Throws<ArgumentNullException>(static () => new TableColumn(null!, new("id")));
        Assert.Equal("", new TableColumn("", new("id")).Title);
    }

    [Fact]
    public void ATitleCannotBePassedWhereAnIdGoesAndTheOtherWayRound()
    {
        const string swapped = """
            using Guinevere;
            class C
            {
                void M()
                {
                    var byTitleAsId = new TableColumn("Title", "id");
                    string idAsText = new TableColumnId("id");
                    TableColumnId textAsId = "id";
                    var sort = new TableSort("id");
                }
            }
            """;
        const string correct = """
            using Guinevere;
            class C
            {
                void M()
                {
                    var column = new TableColumn("Title", new TableColumnId("id"));
                    string text = column.Id.Value;
                    var sort = new TableSort(column.Id);
                }
            }
            """;

        Assert.Empty(CompileErrors(correct));
        Assert.Equal(4, CompileErrors(swapped).Count);
    }

    [Fact]
    public void AnIdLeftOutIsDerivedFromTheDeclaringFileAndLine()
    {
        var column = new TableColumn("Title"); var line = Line();

        Assert.Equal(new TableColumnId($"TableModelTests.cs:{line}"), column.Id);
        Assert.DoesNotContain('/', column.Id.Value);
    }

    [Fact]
    public void ColumnsOnDifferentLinesGetDifferentDerivedIdsAndAnExplicitIdWins()
    {
        var first = new TableColumn("A");
        var second = new TableColumn("A");
        var explicitId = new TableColumn("A", new("stable"));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("stable", explicitId.Id.Value);
        Assert.Equal("stable", explicitId.Id.ToString());
    }

    static int Line([CallerLineNumber] int line = 0) => line;

    /// <summary>Compiles a snippet against the loaded assemblies and returns its compile errors.</summary>
    static List<string> CompileErrors(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && assembly.Location.Length > 0)
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create("TableIdProbe", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return [.. compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.Id)];
    }

    [Fact]
    public void AColumnStartsVisibleUnpinnedAndSharingTheFreeWidth()
    {
        var column = new TableColumn("Title", new("id"));

        Assert.True(column.InitiallyVisible);
        Assert.Equal(TablePin.None, column.Pin);
        Assert.Equal(TableAlignment.Left, column.Alignment);
        Assert.Equal(TableLengthKind.Star, column.Width.Kind);
        Assert.Null(column.Renderer);
    }

    [Fact]
    public void AnAutoColumnIsAsWideAsItsHeaderText()
    {
        TableColumn[] columns =
        [
            new("A", new("short")) { Width = TableLength.Auto, MinWidth = 1f },
            new("A considerably longer header title", new("long")) { Width = TableLength.Auto, MinWidth = 1f }
        ];

        var gui = RunFrame(columns);

        var shortWidth = gui.RootNode!.FindChildById("table/header/short")!.Rect.W;
        var longWidth = gui.RootNode!.FindChildById("table/header/long")!.Rect.W;
        Assert.True(shortWidth >= 2 * TableTheme.Default.CellPadding, "padding is part of the width");
        Assert.True(longWidth > shortWidth + 50f, $"{longWidth} should be well over {shortWidth}");
    }

    static Gui RunFrame(TableColumn[] columns)
    {
        using var surface = SKSurface.Create(new SKImageInfo(600, 200));
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(600, 200);
        var state = new TableState();
        var source = new EmptySource();

        for (var frame = 0; frame < 2; frame++)
        {
            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            gui.Table(state, source, columns);
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            gui.Table(state, source, columns);
            gui.Render();
            gui.EndFrame();
        }

        return gui;
    }

    sealed class EmptySource : ITableViewSource
    {
        public int RowCount => 0;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public object? GetValue(int row, TableColumn column) => null;

        public RowIdentity GetRowIdentity(int row) => RowIdentity.FromIndex(row);
    }
}
