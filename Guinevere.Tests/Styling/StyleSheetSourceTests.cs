namespace Guinevere.Tests.Styling;

/// <summary>Tests non-destructive stylesheet provider reloads.</summary>
public class StyleSheetSourceTests
{
    /// <summary>An invalid provider result reports a diagnostic and retains the last valid sheet.</summary>
    [Fact]
    public void Reload_KeepsLastValidSheetOnError()
    {
        var text = "button { width = 10; }";
        var source = StyleSheetSource.FromProvider(() => text);
        var original = source.Current;
        text = "button { width = 20;";

        Assert.False(source.TryReload());
        Assert.Same(original, source.Current);
        Assert.NotNull(source.Diagnostic);

        text = "button { width = 30; }";
        Assert.True(source.TryReload());
        Assert.Null(source.Diagnostic);
        var style = StyleResolver.Resolve([source.Current], new StyleTarget("button", null, []));
        Assert.Equal("30", style.Get("width"));
    }

    /// <summary>A reload diagnostic carries the source name, line and column of the parse error.</summary>
    [Fact]
    public void Diagnostic_CarriesSourceLineAndColumn()
    {
        var text = "button { width = 10; }";
        var source = StyleSheetSource.FromProvider(() => text, new StyleSheetOptions { SourceName = "theme.pss" });
        text = "button {\n  width = 10;\n  height 4;\n}";

        Assert.False(source.TryReload());

        var diagnostic = source.Diagnostic!;
        Assert.Equal(("theme.pss", 3, 3), (diagnostic.Source, diagnostic.Line, diagnostic.Column));
        Assert.StartsWith("theme.pss:3:3:", diagnostic.Message);
        Assert.IsType<StyleSheetException>(diagnostic.Exception);
    }

    /// <summary>File sources report their path, and I/O failures keep the last sheet with a diagnostic.</summary>
    [Fact]
    public void FileSource_ReportsPathAndKeepsSheetOnIoFailure()
    {
        var directory = Directory.CreateTempSubdirectory("pss-");
        try
        {
            var path = Path.Combine(directory.FullName, "theme.pss");
            File.WriteAllText(path, "button { width = 10; }");
            var source = StyleSheetSource.FromFile(path);
            Assert.False(source.TryReloadIfChanged());

            File.WriteAllText(path, "button { width: 10; }");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
            Assert.False(source.TryReloadIfChanged());
            Assert.Equal((path, 1, 10), (source.Diagnostic!.Source, source.Diagnostic.Line, source.Diagnostic.Column));

            File.Delete(path);
            Assert.False(source.TryReload());
            Assert.Equal(0, source.Diagnostic!.Line);
            Assert.Equal("10", StyleResolver.Resolve([source.Current], new StyleTarget("button", null, [])).Get("width"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>A fixed-text source parses with its options and reloads the same text.</summary>
    [Fact]
    public void StringSource_UsesOptions()
    {
        var source = StyleSheetSource.FromString("button { width: 12; }", new StyleSheetOptions { AllowCssSyntax = true });

        Assert.True(source.TryReload());
        Assert.Equal("12", StyleResolver.Resolve([source.Current], new StyleTarget("button", null, [])).Get("width"));
    }

    /// <summary>A GUI tracks successful source replacements without accepting invalid ones.</summary>
    [Fact]
    public void Gui_TracksReloadedSource()
    {
        var text = "button { width = 10; }";
        var source = StyleSheetSource.FromProvider(() => text);
        var gui = new Gui();
        gui.AddStyleSheet(source);
        text = "button { width = 25; }";

        Assert.True(source.TryReload());
        Assert.Equal("25", gui.ResolveStyle("button").Get("width"));
    }
}
