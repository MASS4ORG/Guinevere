namespace Guinevere;

/// <summary>Composes application chrome with menus, custom content and actions.</summary>
public sealed class AppBarBuilder
{
    internal string TitleText = "";
    internal Action<Gui>? LeadingContent;
    internal float LeadingWidth;
    internal Action<Gui>? MainContent;
    internal readonly List<AppBarAction> Actions = [];

    /// <summary>Sets the title shown in the draggable space.</summary>
    public AppBarBuilder Title(string title)
    {
        TitleText = title;
        return this;
    }

    /// <summary>Reserves a leading region for custom controls such as a compact menu.</summary>
    public AppBarBuilder Leading(Action<Gui> draw, float width = 0)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        LeadingContent = draw;
        LeadingWidth = width;
        return this;
    }

    /// <summary>Fills the flexible content region with controls such as search or a project selector.</summary>
    public AppBarBuilder Content(Action<Gui> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        MainContent = draw;
        return this;
    }

    /// <summary>Adds a trailing action that moves into the overflow menu when space is constrained.</summary>
    public AppBarBuilder Action(string text, Action onClick, bool enabled = true, string shortcut = "")
    {
        ArgumentNullException.ThrowIfNull(onClick);
        Actions.Add(new AppBarAction(text, onClick, enabled, shortcut));
        return this;
    }
}

/// <summary>A trailing application action and its menu representation.</summary>
sealed record AppBarAction(string Text, Action Run, bool Enabled, string Shortcut);
