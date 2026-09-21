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
