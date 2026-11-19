using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>How each kind of <see cref="Content"/> builds and labels itself.</summary>
public class ContentTests
{
    /// <summary>Builds <paramref name="content"/> in a fitting horizontal container and returns that container.</summary>
    static LayoutNode Build(FrameHarness harness, Content content)
    {
        LayoutNode? host = null;
        harness.Frame(g =>
        {
            host = g.Node(Size.FitContent(), Size.FitContent()).Direction(Axis.Horizontal);
            using (host.Enter())
            {
                g.Content(content);
            }
        });
        return host!;
    }

    [Fact]
    public void TextBuildsOneSelfSizingNode()
    {
        using var harness = new FrameHarness();
        var host = Build(harness, "Save");

        var text = Assert.Single(host.ChildNodes);
        Assert.True(text.Rect.W > 10);
        Assert.True(text.Rect.H > 5);
        Assert.Equal(text.Rect.W, host.Rect.W);
    }

    [Fact]
    public void IconBuildsASquareOfTheTextSize()
    {
        using var harness = new FrameHarness();
        var host = Build(harness, Icon.FromGlyph(WidgetIcons.Check));

        var icon = Assert.Single(host.ChildNodes);
        Assert.Equal(icon.Rect.W, icon.Rect.H);
        Assert.Equal(harness.Gui.CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value, icon.Rect.W);
    }

    [Fact]
    public void EmptyAndNullBuildNothing()
    {
        using var harness = new FrameHarness();

        Assert.Empty(Build(harness, Content.None).ChildNodes);
        Assert.Empty(Build(harness, (string?)null).ChildNodes);
        Assert.True(((Content)"").IsEmpty);
        Assert.True(((Content)(Icon?)null).IsEmpty);
    }

    [Fact]
    public void LazyTextIsAskedWhileBuilding()
    {
        using var harness = new FrameHarness();
        var language = "Save";
        var content = Content.Text(() => language);

        var english = Build(harness, content).ChildNodes[0].Rect.W;
        language = "Speichern";
        var german = Build(harness, content).ChildNodes[0].Rect.W;

        Assert.True(german > english);
        Assert.Equal("Speichern", content.Label);
    }

    [Fact]
    public void BuilderAndCustomBuildTheirOwnChildren()
    {
        using var harness = new FrameHarness();
        var built = Build(harness, Content.Build(() =>
        {
            harness.Gui.Node(10, 10);
            harness.Gui.Node(20, 10);
        }));
        Assert.Equal(2, built.ChildNodes.Count);
        Assert.Equal(30, built.Rect.W);

        var custom = Build(harness, Content.From(new Badge()));
        Assert.Equal(7, Assert.Single(custom.ChildNodes).Rect.W);
    }

    [Fact]
    public void RowLaysPartsOutHorizontallyWithAGap()
    {
        using var harness = new FrameHarness();
        var host = Build(harness, Content.Row(Icon.FromGlyph(WidgetIcons.Check), "Save"));

        var row = Assert.Single(host.ChildNodes);
        Assert.Equal(2, row.ChildNodes.Count);
        var first = row.ChildNodes[0].Rect;
        Assert.True(row.ChildNodes[1].Rect.X > first.X + first.W);
    }

    [Fact]
    public void LabelsComeFromTextCustomTypesRowsOrAnExplicitLabel()
    {
        Content icon = Icon.FromGlyph(WidgetIcons.Check);

        Assert.Equal("Save", ((Content)"Save").Label);
        Assert.Equal("x", ((Content)'x').Label);
        Assert.Null(icon.Label);
        Assert.Null(Content.Build(static () => { }).Label);
        Assert.Equal("Done", icon.Labeled("Done").Label);
        Assert.Equal("badge", Content.From(new Badge()).Label);
        Assert.Equal("badge Save", Content.Row(icon, Content.From(new Badge()), "Save").Label);
        Assert.Same(icon.Icon, icon.Labeled("Done").Icon);
        Assert.Equal(ContentKind.Icon, icon.Labeled("Done").Kind);
        Assert.True(Content.Row().IsEmpty);
    }

    [Fact]
    public void FactoriesRejectNull()
    {
        Assert.Throws<ArgumentNullException>(static () => Content.Text(null!));
        Assert.Throws<ArgumentNullException>(static () => Content.Build(null!));
        Assert.Throws<ArgumentNullException>(static () => Content.From(null!));
        Assert.Throws<ArgumentNullException>(static () => Content.Row(null!));
    }

    /// <summary>A consumer type that draws a fixed-size node and names itself.</summary>
    sealed class Badge : IContent
    {
        public void Build(Gui gui) => gui.Node(7, 7);

        public string Label => "badge";
    }
}
