namespace Guinevere.Tests;

/// <summary>Runtime family lookup, descriptor selection, roles and ordered fallback.</summary>
public class FontRegistryTests
{
    internal static string FontPath(string name = "font.ttf") => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../Integrations/Resources/Fonts", name));

    /// <summary>File and stream registrations retain aliases and ordered fallback without closing caller streams.</summary>
    [Fact]
    public void FilesAndStreamsRegisterAliasesAndRetainCallerStream()
    {
        var registry = new FontRegistry();
        registry.RegisterFile("Brand", FontPath());
        using var stream = File.OpenRead(FontPath("widget-icons.ttf"));
        registry.RegisterStream("Symbols", stream);
        var primary = registry.Resolve("'missing-family-xyz', 'Brand', Symbols", 19)!;
        Assert.Equal("Roboto", primary.FamilyName);
        Assert.Equal(19, primary.Size);
        Assert.Single(primary.Fallbacks);
        Assert.NotEqual((ushort)0, primary.Fallbacks[0].SkFont.GetGlyph(0xf007));
        Assert.True(stream.CanRead);
        Assert.Same(primary, registry.Resolve("'missing-family-xyz', 'Brand', Symbols", 19));
    }

    /// <summary>Numeric weight lookup follows directional preferences around regular and bold weights.</summary>
    [Theory]
    [InlineData(300, 200)]
    [InlineData(400, 500)]
    [InlineData(450, 500)]
    [InlineData(550, 600)]
    [InlineData(700, 800)]
    [InlineData(900, 800)]
    public void WeightSelectionFollowsDirectionalPreference(int requested, int expected)
    {
        var registry = new FontRegistry();
        var faces = new Dictionary<int, Font>();
        foreach (var (weight, file) in new[] { (200, "font.ttf"), (500, "Roboto-Regular.ttf"),
                     (600, "widget-icons.ttf"), (800, "icons.ttf") })
        {
            var face = Font.FromFile(FontPath(file));
            faces.Add(weight, face);
            registry.Register("Brand", face, weight);
        }
        var selected = registry.Resolve("brand", weight: requested)!;
        Assert.Same(faces[expected].SkFont.Typeface, selected.SkFont.Typeface);
        Assert.Equal(requested, selected.Weight);
    }

    /// <summary>Slant selection precedes weight selection, and synthesis survives resizing.</summary>
    [Fact]
    public void SlantWinsBeforeWeightAndMissingStylesAreSynthesized()
    {
        var registry = new FontRegistry();
        var regular = Font.FromFile(FontPath());
        var italic = Font.FromFamilyName("serif", style: FontStyle.Italic);
        registry.Register("Brand", regular, 700, false);
        registry.Register("Brand", italic, 300, true);
        Assert.Same(italic.SkFont.Typeface, registry.Resolve("Brand", weight: 700, italic: true)!.SkFont.Typeface);
        registry.Register("Single", regular);
        var synthesized = registry.Resolve("Single", weight: 700, italic: true)!.WithSize(30);
        Assert.True(synthesized.SkFont.Embolden);
        Assert.Equal(-0.25f, synthesized.SkFont.SkewX);
        Assert.Equal(700, synthesized.Weight);
        Assert.True(synthesized.Italic);
    }

    /// <summary>Replacing faces invalidates cached misses and refreshes role assignments.</summary>
    [Fact]
    public void ReplacementInvalidatesCachedMissesAndKeepsRolesCurrent()
    {
        var registry = new FontRegistry();
        Assert.Null(registry.Resolve("Brand"));
        registry.SetRole(FontRole.Code, "Brand, monospace");
        registry.RegisterFile("Brand", FontPath());
        var first = registry.ResolveRole(FontRole.Code)!;
        Assert.Equal("Roboto", first.FamilyName);
        var version = registry.Version;
        var replacement = Font.FromFile(FontPath("widget-icons.ttf"));
        registry.Register("Brand", replacement, 400);
        Assert.True(registry.Version > version);
        Assert.Same(replacement.SkFont.Typeface, registry.ResolveRole(FontRole.Code)!.SkFont.Typeface);
        registry.SetRole(FontRole.Code, replacement);
        Assert.Same(replacement, registry.ResolveRole(FontRole.Code));
        version = registry.Version;
        registry.SetRole(FontRole.Code, replacement);
        Assert.Equal(version, registry.Version);
    }

    /// <summary>Quoted family names retain commas while generic families remain available.</summary>
    [Fact]
    public void QuotedFamilyNamesCanContainCommas()
    {
        var registry = new FontRegistry();
        registry.RegisterFile("Brand, Display", FontPath());
        Assert.Equal("Roboto", registry.Resolve("\"Brand, Display\"")!.FamilyName);
        Assert.Null(registry.Resolve("unknown-family-xyz"));
        Assert.NotNull(registry.Resolve("monospace"));
    }

    /// <summary>Invalid font data and descriptors are rejected explicitly.</summary>
    [Fact]
    public void InvalidRegistrationAndLookupArgumentsThrow()
    {
        var registry = new FontRegistry();
        var font = Font.FromFile(FontPath());
        Assert.Throws<ArgumentNullException>(() => registry.Register("Brand", null!));
        Assert.Throws<ArgumentException>(() => registry.Register("", font));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Register("Brand", font, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Register("Brand", font, 1001));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Resolve("Brand", float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Resolve("Brand", weight: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Resolve("Brand", weight: 1001));
        using var invalid = new MemoryStream([1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => registry.RegisterStream("Brand", invalid));
        Assert.Throws<FileNotFoundException>(() => registry.RegisterFile("Brand", FontPath("missing.ttf")));
    }

    /// <summary>Assigned roles preserve their default font and honor explicit size and style requests.</summary>
    [Fact]
    public void RoleDescriptorsResizeAndRestyleAssignedFonts()
    {
        using var registry = new FontRegistry();
        var font = Font.FromFile(FontPath());
        registry.SetRole(FontRole.Code, font);
        Assert.Same(font, registry.ResolveRole(FontRole.Code));
        var styled = registry.ResolveRole(FontRole.Code, 20, 700, true)!;
        Assert.Equal(20, styled.Size);
        Assert.True(styled.SkFont.Embolden);
        Assert.True(styled.Italic);
        Assert.Null(registry.ResolveRole(FontRole.Ui));
        registry.ClearRole(FontRole.Code);
        Assert.Null(registry.ResolveRole(FontRole.Code));
        registry.ClearRole(FontRole.Code);
    }

    /// <summary>Font measurement preserves native ink bounds for ordinary runs and handles empty input.</summary>
    [Fact]
    public void MeasurementPreservesNativeInkBounds()
    {
        var font = Font.FromFile(FontPath());
        font.SkFont.MeasureText("'", out var bounds);
        Assert.Equal(new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height), font.MeasureText("'"));
        Assert.Equal(new Rect(), font.MeasureText(""));
        Assert.Throws<ArgumentNullException>(() => font.MeasureText(null!));
    }

    /// <summary>Registry disposal releases owned fonts while preserving borrowed fonts.</summary>
    [Fact]
    public void DisposalReleasesOwnedResourcesAndPreservesCallerFonts()
    {
        var registry = new FontRegistry();
        var borrowed = Font.FromFile(FontPath());
        registry.Register("Borrowed", borrowed);
        registry.RegisterFile("Loaded", FontPath("widget-icons.ttf"));
        var loaded = registry.Resolve("Loaded")!;
        registry.Dispose();
        registry.Dispose();
        Assert.Equal(IntPtr.Zero, loaded.SkFont.Handle);
        Assert.NotEqual(IntPtr.Zero, borrowed.SkFont.Handle);
        Assert.Throws<ObjectDisposedException>(() => registry.Resolve("Borrowed"));
        Assert.Throws<ObjectDisposedException>(() => registry.Register("Borrowed", borrowed));
        Assert.Throws<ObjectDisposedException>(() => registry.SetRole(FontRole.Code, "Borrowed"));
        Assert.Throws<ObjectDisposedException>(() => registry.ResolveRole(FontRole.Code));
        Assert.Throws<ObjectDisposedException>(() => registry.ClearRole(FontRole.Code));
    }
}
