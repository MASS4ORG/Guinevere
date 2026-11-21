namespace Guinevere;

/// <summary>What a <see cref="Content"/> holds.</summary>
public enum ContentKind
{
    /// <summary>Nothing; the widget shows only its own chrome.</summary>
    Empty,

    /// <summary>A fixed string: plain text, emoji or icon-font glyphs.</summary>
    Text,

    /// <summary>A string asked for while building, such as a localization lookup.</summary>
    LazyText,

    /// <summary>An <see cref="Guinevere.Icon"/>: glyph, raster image or vector picture.</summary>
    Icon,

    /// <summary>A delegate that builds any widgets or nodes as children.</summary>
    Build,

    /// <summary>A consumer type implementing <see cref="IContent"/>.</summary>
    Custom,

    /// <summary>Several contents laid out in a row, such as an icon followed by text.</summary>
    Row,
}

/// <summary>
/// Content a consumer type builds itself inside a widget, such as a localized string or a composite badge.
/// A type may also declare <c>implicit operator Content</c> so it can be passed to widgets directly.
/// </summary>
public interface IContent
{
    /// <summary>Builds the content as children of the current node; called in both passes with the same result.</summary>
    /// <param name="gui">The GUI, positioned inside the widget.</param>
    void Build(Gui gui);

    /// <summary>A readable text for accessibility and semantics, or <c>null</c> when the content has none.</summary>
    string? Label => null;
}

/// <summary>
/// What a widget such as a button, checkbox or label shows: text, an icon, a lazily asked string, any widgets, a custom
/// type, or a row of these. Strings, characters and icons convert implicitly; the factories cover the rest, since C#
/// cannot convert a lambda or an interface implicitly. A string literal is stored without allocating.
/// </summary>
public readonly struct Content
{
    readonly object? _value;
    readonly string? _label;

    Content(ContentKind kind, object? value, string? label = null)
    {
        Kind = kind;
        _value = value;
        _label = label;
    }

    /// <summary>What this content holds.</summary>
    public ContentKind Kind { get; }

    /// <summary>Whether there is nothing to show.</summary>
    public bool IsEmpty => Kind == ContentKind.Empty;

    /// <summary>The icon when <see cref="Kind"/> is <see cref="ContentKind.Icon"/>, otherwise <c>null</c>.</summary>
    public Icon? Icon => _value as Icon;

    /// <summary>
    /// The readable text for semantics and tooltips: the explicit label from <see cref="Labeled"/>, else the text, the
    /// custom type's <see cref="IContent.Label"/>, or the row's labels joined by spaces; <c>null</c> for an unlabeled
    /// icon or builder.
    /// </summary>
    public string? Label => _label ?? Kind switch
    {
        ContentKind.Text => (string)_value!,
        ContentKind.LazyText => ((Func<string>)_value!)(),
        ContentKind.Custom => ((IContent)_value!).Label,
        ContentKind.Row => RowLabel((Content[])_value!),
        _ => null,
    };

    /// <summary>The parts of a <see cref="ContentKind.Row"/>, otherwise empty.</summary>
    public ReadOnlySpan<Content> Parts => Kind == ContentKind.Row ? (Content[])_value! : default;

    /// <summary>The empty content.</summary>
    public static Content None => default;

    /// <summary>Text, emoji or icon-font glyphs; <c>null</c> or empty is <see cref="None"/>.</summary>
    /// <param name="text">The text.</param>
    public static implicit operator Content(string? text) =>
        string.IsNullOrEmpty(text) ? default : new Content(ContentKind.Text, text);

    /// <summary>A single character, such as an icon glyph.</summary>
    /// <param name="glyph">The character.</param>
    public static implicit operator Content(char glyph) => new(ContentKind.Text, glyph.ToString());

    /// <summary>An icon; <c>null</c> is <see cref="None"/>.</summary>
    /// <param name="icon">The icon.</param>
    public static implicit operator Content(Icon? icon) =>
        icon is null ? default : new Content(ContentKind.Icon, icon);

    /// <summary>
    /// Text asked for while building, so a localization lookup follows the current language. It is asked in both
    /// passes and must return the same string within a frame; keep it cheap.
    /// </summary>
    /// <param name="text">Returns the text.</param>
    public static Content Text(Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Content(ContentKind.LazyText, text);
    }

    /// <summary>
    /// Any widgets or nodes, built as children of the widget by <paramref name="build"/> in both passes. Give it a
    /// <see cref="Labeled"/> text when the widget needs a readable name.
    /// </summary>
    /// <param name="build">Builds the children.</param>
    public static Content Build(Action build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new Content(ContentKind.Build, build);
    }

    /// <summary>A consumer type that builds itself.</summary>
    /// <param name="content">The content.</param>
    public static Content From(IContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new Content(ContentKind.Custom, content);
    }

    /// <summary>Several contents in a row, such as an icon then text; empty parts are skipped.</summary>
    /// <param name="parts">The parts, in order.</param>
    public static Content Row(params Content[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.Length == 0 ? default : new Content(ContentKind.Row, parts);
    }

    /// <summary>The same content with a readable label, such as the name of an icon-only button.</summary>
    /// <param name="label">The label used for semantics and tooltips.</param>
    public Content Labeled(string label) => new(Kind, _value, label);

    /// <summary>
    /// Builds the content as children of the current node; <paramref name="selectable"/> lets text content be
    /// selected and copied with the pointer.
    /// </summary>
    internal void BuildIn(Gui gui, bool selectable = false)
    {
        switch (Kind)
        {
            case ContentKind.Text:
                gui.DrawText((string)_value!, selectable, centerInRect: false);
                break;
            case ContentKind.LazyText:
                gui.DrawText(((Func<string>)_value!)(), selectable, centerInRect: false);
                break;
            case ContentKind.Icon:
                var icon = (Icon)_value!;
                gui.Icon(icon, tint: icon.Color ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value);
                break;
            case ContentKind.Build:
                ((Action)_value!)();
                break;
            case ContentKind.Custom:
                ((IContent)_value!).Build(gui);
                break;
            case ContentKind.Row:
                BuildRow(gui, (Content[])_value!);
                break;
        }
    }

    /// <summary>A horizontal node, centred vertically, with a gap of a third of the text size between parts.</summary>
    static void BuildRow(Gui gui, Content[] parts)
    {
        var gap = gui.CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value / 3f;
        using (gui.Node().Direction(Axis.Horizontal).Gap(gap).AlignContentY(0.5f).Enter())
        {
            foreach (var part in parts)
            {
                part.BuildIn(gui);
            }
        }
    }

    static string? RowLabel(Content[] parts)
    {
        string? label = null;
        foreach (var part in parts)
        {
            if (part.Label is { Length: > 0 } text)
            {
                label = label is null ? text : $"{label} {text}";
            }
        }

        return label;
    }
}
