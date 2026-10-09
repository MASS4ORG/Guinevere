using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Stylesheet font descriptors, cascade replacement and token-selected roles.</summary>
public class StyledFontsTests
{
    static string UriOf(string name) => new Uri(FontRegistryTests.FontPath(name)).AbsoluteUri;

    /// <summary>Stylesheet face descriptors select weighted and italic faces in the runtime registry.</summary>
    [Fact]
    public void FontFaceDescriptorsChooseWeightAndStyleAndShareTheRuntimeRegistry()
    {
        using var frame = new FrameHarness();
        var sheet = StyleSheet.Parse($$"""
            @font-face { font-family = Brand; src = url("{{UriOf("font.ttf")}}"); }
            @font-face { font-family = Brand; src = url("{{UriOf("widget-icons.ttf")}}");
                font-weight = 600; font-style = italic; }
            card { font-family = Brand; font-weight = 600; font-style = italic; }
            """);
        Assert.Equal(600, sheet.FontFaces[1].Weight);
        Assert.True(sheet.FontFaces[1].Italic);
        frame.Gui.StyleSheets.Add(sheet);
        var chosen = frame.Gui.Fonts.Resolve("Brand", weight: 600, italic: true)!;
        Assert.Contains("Font Awesome", chosen.FamilyName);
        frame.Frame(gui =>
        {
            using (gui.StyledNode("card").Enter())
            {
                var font = gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value;
                Assert.Same(chosen.SkFont.Typeface, font.SkFont.Typeface);
                using (gui.StyledNode("child").Enter())
                    Assert.Equal(600, gui.GetTextFont().Weight);
            }
        });
    }

    /// <summary>Sheet replacement and removal restore the cascade without removing runtime faces.</summary>
    [Fact]
    public void RemovingAndReplacingSheetsRestoresEarlierFacesAndRetainsRuntimeFonts()
    {
        var gui = new Gui();
        gui.Fonts.RegisterFile("Runtime", FontRegistryTests.FontPath());
        var first = StyleSheet.Parse($$"""
            @font-face { font-family = Brand; src = url("{{UriOf("font.ttf")}}"); }
            """);
        var second = StyleSheet.Parse($$"""
            @font-face { font-family = Brand; src = url("{{UriOf("widget-icons.ttf")}}"); }
            """);
        gui.StyleSheets.Add(first);
        gui.StyleSheets.Add(second);
        Assert.Contains("Font Awesome", gui.Fonts.Resolve("Brand")!.FamilyName);
        gui.StyleSheets.Remove(second);
        Assert.Equal("Roboto", gui.Fonts.Resolve("Brand")!.FamilyName);
        gui.StyleSheets[0] = second;
        Assert.Contains("Font Awesome", gui.Fonts.Resolve("Brand")!.FamilyName);
        gui.StyleSheets.Clear();
        Assert.Null(gui.Fonts.Resolve("Brand"));
        Assert.Equal("Roboto", gui.Fonts.Resolve("Runtime")!.FamilyName);
    }

    /// <summary>Changing ordinary tokens preserves loaded font faces.</summary>
    [Fact]
    public void TokenChangesDoNotReloadFontFaces()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse($$"""
            @font-face { font-family = Brand; src = url("{{UriOf("font.ttf")}}"); }
            icon#test { glyph = "A"; font-family = Brand; }
            """));
        var original = gui.Fonts.Resolve("Brand");
        gui.StyleSheets.SetToken("color", "#ff0000");
        gui.ResolveIcon("test");
        Assert.Same(original, gui.Fonts.Resolve("Brand"));
    }

    /// <summary>Font role tokens honor host and subtree overrides.</summary>
    [Fact]
    public void RoleTokensRespectHostAndSubtreeOverrides()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Brand", FontRegistryTests.FontPath());
        frame.Gui.Fonts.RegisterFile("Widgets", FontRegistryTests.FontPath("widget-icons.ttf"));
        frame.Gui.StyleSheets.Add(StyleSheet.Parse("""
            $font-ui = Brand;
            $font-ui-mono = monospace;
            $font-code = $font-ui-mono;
            $font-icon = Widgets;
            $font-emoji = Brand;
            """));
        Assert.Equal("Roboto", frame.Gui.ResolveFontRole(FontRole.Ui)!.FamilyName);
        Assert.NotNull(frame.Gui.ResolveFontRole(FontRole.Code));
        frame.Gui.StyleSheets.SetToken("font-code", "Brand");
        Assert.Equal("Roboto", frame.Gui.ResolveFontRole(FontRole.Code)!.FamilyName);
        frame.Frame(gui =>
        {
            gui.UseFontRole(FontRole.Ui);
            Assert.Equal("Roboto", gui.GetTextFont().FamilyName);
            using (gui.Node().Enter())
            {
                gui.SetStyleToken("font-ui", "Widgets");
                gui.UseFontRole(FontRole.Ui);
                Assert.Contains("Font Awesome", gui.GetTextFont().FamilyName);
                gui.UseFontRole(FontRole.Icon);
                gui.UseFontRole(FontRole.Emoji);
                Assert.Equal("Roboto", gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.FamilyName);
            }
            Assert.Equal("Roboto", gui.GetTextFont().FamilyName);
        });
    }

    /// <summary>Scoped font role aliases expand through local and global tokens.</summary>
    [Fact]
    public void ScopedRoleAliasesExpandAndInvalidRolesThrow()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Brand", FontRegistryTests.FontPath());
        frame.Gui.StyleSheets.Add(StyleSheet.Parse("$font-ui = Brand; $font-ui-mono = monospace;"));
        frame.Frame(gui =>
        {
            gui.SetStyleToken("font-ui", "Brand");
            gui.SetStyleToken("font-code", "$font-ui");
            Assert.Equal("Roboto", gui.ResolveFontRole(FontRole.Code)!.FamilyName);
            gui.SetStyleToken("font-ui", "monospace");
            Assert.Equal(gui.ResolveFontRole(FontRole.UiMono)!.FamilyName,
                gui.ResolveFontRole(FontRole.Code)!.FamilyName);
            gui.SetStyleToken("font-code", "missing-family-xyz");
            gui.UseFontRole(FontRole.Code);
            Assert.Null(gui.ResolveFontRole(FontRole.Code));
            Assert.Throws<ArgumentOutOfRangeException>(() => gui.ResolveFontRole((FontRole)99));
        });
    }

    /// <summary>Unavailable local and remote faces leave other families in the list usable.</summary>
    [Fact]
    public void MissingAndRemoteFontFacesLeaveFallbacksUsable()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse("""
            @font-face { font-family = Missing; src = url("/no-such-directory/font.ttf"); }
            @font-face { font-family = Remote; src = url("https://example.invalid/font.ttf"); }
            """));
        Assert.Null(gui.Fonts.Resolve("Missing, Remote"));
        Assert.NotNull(gui.Fonts.Resolve("Missing, Remote, monospace"));
    }

    /// <summary>Runtime registration invalidates icon caches and supplies glyph fallback families.</summary>
    [Fact]
    public void RegisteredFallbackFontsReachStyledIcons()
    {
        using var frame = new FrameHarness();
        var gui = frame.Gui;
        gui.Fonts.RegisterFile("Text", FontRegistryTests.FontPath());
        gui.StyleSheets.Add(StyleSheet.Parse("icon#test { glyph = \"\\f007\"; font-family = Text, Widgets; }"));
        var initial = gui.ResolveIcon("test")!;
        gui.Fonts.RegisterFile("Widgets", FontRegistryTests.FontPath("widget-icons.ttf"));
        var updated = gui.ResolveIcon("test")!;
        Assert.NotSame(initial, updated);
        Assert.Single(updated.Font!.Fallbacks);
        frame.Frame(g => g.StyledIcon("test", 24));
        using var actual = frame.Snapshot();
        using var actualPixels = actual.PeekPixels();
        using var reference = new FrameHarness();
        reference.Frame(g => g.Icon(Icon.FromGlyph("\uf007", gui.Fonts.Resolve("Widgets")), 24));
        using var expected = reference.Snapshot();
        using var expectedPixels = expected.PeekPixels();
        Assert.Equal(expectedPixels.GetPixelSpan().ToArray(), actualPixels.GetPixelSpan().ToArray());
    }

    /// <summary>Unregistered inherited file fonts synthesize requested styles without requiring a system face.</summary>
    [Fact]
    public void DirectScopeFontsCanBeRestyledWithoutFamilyRegistration()
    {
        using var frame = new FrameHarness();
        var font = Font.FromFile(FontRegistryTests.FontPath("widget-icons.ttf"));
        frame.Gui.StyleSheets.Add(StyleSheet.Parse("child { font-weight = bold; font-style = italic; }"));
        frame.Frame(gui =>
        {
            gui.SetTextFont(font);
            using (gui.StyledNode("child").Enter())
            {
                var styled = gui.GetTextFont();
                Assert.Equal(font.FamilyName, styled.FamilyName);
                Assert.True(styled.Italic);
                Assert.Equal(700, styled.Weight);
                Assert.True(styled.SkFont.Embolden || styled.SkFont.Typeface.FontWeight >= 600);
            }
        });
    }
}
