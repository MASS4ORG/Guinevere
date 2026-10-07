namespace Guinevere.Tests.Styling;

/// <summary>
/// PanGui conformance corpus: every published PanGui stylesheet example (<c>Styling/Corpus/*.pss</c>, copied verbatim)
/// must parse, and the subsets Guinevere applies must resolve to the declared values.
/// </summary>
public class PanGuiCorpusTests
{
    static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "Styling", "Corpus");

    /// <summary>Names of every corpus fixture.</summary>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(CorpusDirectory, "*.pss").Order(StringComparer.Ordinal))
            data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    static StyleSheet Load(string name) => StyleSheetSource.FromFile(Path.Combine(CorpusDirectory, $"{name}.pss")).Current;

    static ResolvedStyle Resolve(string fixture, StyleTarget target) => StyleResolver.Resolve([Load(fixture)], target);

    static StyleTarget Target(string type, IReadOnlyList<string>? modifiers = null, params string[] ancestors) =>
        new(type, null, [], Modifiers: modifiers, Ancestors: [.. ancestors.Select(a => new StyleTarget(a, null, []))]);

    /// <summary>The corpus holds every PanGui blog example.</summary>
    [Fact]
    public void Corpus_HasEveryPublishedExample() => Assert.Equal(15, Fixtures().Count);

    /// <summary>Every corpus fixture parses.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_Parses(string name) => Assert.NotNull(Load(name));

    /// <summary>Toggle: sizes apply; shapes, effects and the background shape are deferred.</summary>
    [Fact]
    public void Toggle_ResolvesSizesAndDefersShapes()
    {
        var sheet = Load("toggle");
        var style = StyleResolver.Resolve([sheet], Target("toggle"));

        Assert.Equal(("60", "ratio(2)"), (style.Get("height"), style.Get("width")));
        Assert.Equal(6, sheet.Deferred.Count);
        Assert.Contains(sheet.Deferred, d => d.Kind == StyleDeferredKind.BackgroundShape);
    }

    /// <summary>Descendant, direct-child and nested selectors from the toolbar example.</summary>
    [Fact]
    public void ToolbarAndSidebar_ResolveHierarchy()
    {
        Assert.Equal("80", Resolve("toolbar-sidebar", Target("button", null, "dialog")).Get("min-width"));
        var sidebarButton = Resolve("toolbar-sidebar", Target("button", null, "sidebar"));
        Assert.Equal(("15", "#2a2a2a"), (sidebarButton.Get("padding"), sidebarButton.Get("bg-color")));

        var toolbarButton = Resolve("toolbar-sidebar", Target("button", null, "toolbar"));
        Assert.Equal(("10", "expand"), (toolbarButton.Get("padding"), toolbarButton.Get("height")));
        var hovered = StyleResolver.Resolve([Load("toolbar-sidebar")],
            Target("button", null, "toolbar") with { State = StyleState.Hover });
        Assert.Equal("#3a3a3a", hovered.Get("bg-color"));
        Assert.Equal("2", Resolve("toolbar-sidebar", Target("separator", null, "toolbar")).Get("width"));
        Assert.Null(Resolve("toolbar-sidebar", Target("separator", null, "group", "toolbar")).Get("width"));
    }

    /// <summary><c>#inherit</c> copies properties and joins parent selectors.</summary>
    [Fact]
    public void Inherit_CopiesAndMatches()
    {
        var warning = Resolve("inherit", Target("warning-box"));
        Assert.Equal(("20", "orange"), (warning.Get("padding"), warning.Get("bg-color")));
        Assert.Equal("10", Resolve("inherit", Target("button", null, "error-box")).Get("padding"));
    }

    /// <summary>Property/selector-only inheritance and multi-parent inheritance parse into their maps.</summary>
    [Fact]
    public void InheritVariants_AreRecorded()
    {
        var variants = Load("inherit-properties-selector");
        Assert.Equal(new[] { "box" }, variants.PropertyInheritance["info-panel"]);
        Assert.Equal(new[] { "box" }, variants.SelectorInheritance["info-panel"]);
        Assert.Equal(new[] { "card", "hoverable", "clickable" }, Load("inherit-multiple").Inheritance["interactive-card"]);
    }

    /// <summary>Checkbox and button: sizes, scale and rule-scoped variables apply; effects are deferred.</summary>
    [Fact]
    public void CheckboxAndButton_ResolveScalars()
    {
        var checkbox = Resolve("checkbox-button-effects", Target("checkbox", ["checked"]));
        Assert.Equal(("24", "24"), (checkbox.Get("width"), checkbox.Get("height")));
        var held = StyleResolver.Resolve([Load("checkbox-button-effects")],
            Target("button") with { State = StyleState.Active });
        Assert.Equal("0.95", held.Get("scale"));
    }

    /// <summary>Progress bar, spinner, node and card examples resolve their scalar declarations.</summary>
    [Fact]
    public void ShapeExamples_ResolveScalars()
    {
        Assert.Equal("30", Resolve("progress-bar", Target("progress-bar")).Get("height"));
        Assert.Equal(("50", "50"), (Resolve("spinner", Target("loading-spinner")).Get("width"),
            Resolve("spinner", Target("loading-spinner")).Get("height")));
        Assert.Equal("true", Resolve("node-example", Target("nodeExample")).Get("clip-content"));
        Assert.Contains(Load("elevated-gradient").Deferred, d => d.Kind == StyleDeferredKind.Effect);
    }

    /// <summary><c>@const</c> values substitute and are exposed to the host.</summary>
    [Fact]
    public void Const_SubstitutesAndExposes()
    {
        var sheet = Load("const");
        var box = StyleResolver.Resolve([sheet], Target("box"));

        Assert.Equal(("16", "16 * 0.5", "#4a90e2"), (box.Get("padding"), box.Get("border-radius"), box.Get("bg-color")));
        Assert.Equal("0.3", sheet.Constants["animSpeed"]);
    }

    /// <summary>Mixins, style macros and shape/effect macros are kept as deferred constructs.</summary>
    [Theory]
    [InlineData("mixin-show-all-children", StyleDeferredKind.Mixin, 2)]
    [InlineData("mixin-card-appearance", StyleDeferredKind.Mixin, 2)]
    [InlineData("style-icons", StyleDeferredKind.StyleMacro, 6)]
    [InlineData("shape-effect-macros", StyleDeferredKind.ShapeMacro, 10)]
    public void Macros_AreDeferred(string fixture, StyleDeferredKind first, int count)
    {
        var sheet = Load(fixture);
        Assert.Equal(first, sheet.Deferred[0].Kind);
        Assert.Equal(count, sheet.Deferred.Count);
    }
}
