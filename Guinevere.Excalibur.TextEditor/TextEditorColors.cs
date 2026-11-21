namespace Guinevere;

/// <summary>
/// The editor's colors and token styles, read from the <c>editor</c>, <c>editor-*</c> and <c>syntax-*</c> rules of the
/// sheets. A part no sheet defines falls back to a dark palette, so an editor stays legible under a custom base sheet.
/// </summary>
readonly record struct TextEditorColors(
    Color Background, Color Text, Color Gutter, Color LineNumber, Color ActiveLineNumber, Color CurrentLine,
    Color Selection, Color Caret, Color FindMatch, Color BracketMatch, Color Error, Color Warning,
    Color Information, Color Hint, Color Popup, Color PopupBorder, Color PopupSelection, Color Scrollbar,
    TextSpanStyle?[] Syntax)
{
    static readonly string[] Current = ["current"];

    static readonly string[] SyntaxParts =
        [.. Enum.GetValues<SyntaxKind>().Select(static kind => "syntax-" + kind.ToString().ToLowerInvariant())];

    /// <summary>
    /// Reads every color for the current node. It resolves about thirty parts, so callers read it once per frame, not
    /// per line or glyph.
    /// </summary>
    public static TextEditorColors Read(Gui gui) => new(
        Background: Back(gui, "editor", Rgb(30, 30, 30)),
        Text: Ink(gui, "editor", Rgb(212, 212, 212)),
        Gutter: Back(gui, "editor-gutter", Rgb(30, 30, 30)),
        LineNumber: Ink(gui, "editor-line-number", Rgb(110, 118, 129)),
        ActiveLineNumber: Ink(gui, "editor-line-number", Rgb(200, 200, 200), Current),
        CurrentLine: Back(gui, "editor-current-line", Rgb(40, 40, 44)),
        Selection: Back(gui, "editor-selection", Rgb(38, 79, 120)),
        Caret: Ink(gui, "editor-caret", Rgb(174, 175, 173)),
        FindMatch: Back(gui, "editor-find-match", Color.FromArgb(110, 234, 92, 0)),
        BracketMatch: Back(gui, "editor-bracket-match", Color.FromArgb(80, 120, 140, 160)),
        Error: Ink(gui, "editor-error", Rgb(241, 76, 76)),
        Warning: Ink(gui, "editor-warning", Rgb(205, 173, 0)),
        Information: Ink(gui, "editor-information", Rgb(55, 148, 255)),
        Hint: Ink(gui, "editor-hint", Color.FromArgb(160, 160, 160, 160)),
        Popup: Back(gui, "editor-popup", Rgb(37, 37, 38)),
        PopupBorder: Ink(gui, "editor-popup", Rgb(69, 69, 69), property: "border-color"),
        PopupSelection: Back(gui, "editor-popup-selection", Rgb(4, 57, 94)),
        Scrollbar: Back(gui, "editor-scrollbar", Color.FromArgb(90, 121, 121, 121)),
        Syntax: ReadSyntax(gui));

    /// <summary>The squiggle color of a severity.</summary>
    public Color ColorOf(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => Error,
        DiagnosticSeverity.Warning => Warning,
        DiagnosticSeverity.Information => Information,
        _ => Hint
    };

    /// <summary>The style of a token kind, or null for plain text.</summary>
    public TextSpanStyle? StyleOf(SyntaxKind kind) => Syntax[(int)kind];

    /// <summary>The token styles as a <see cref="SyntaxTheme"/>, for the Markdown a hover box draws.</summary>
    public SyntaxTheme ToSyntaxTheme()
    {
        var styles = new Dictionary<SyntaxKind, TextSpanStyle>();
        foreach (var kind in Enum.GetValues<SyntaxKind>())
        {
            if (Syntax[(int)kind] is { } style)
            {
                styles[kind] = style;
            }
        }

        return new SyntaxTheme(styles);
    }

    static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    static Color Back(Gui gui, string part, Color fallback) =>
        gui.ResolvePart(part).GetColor("background-color") ?? fallback;

    static Color Ink(Gui gui, string part, Color fallback, IReadOnlyList<string>? modifiers = null,
        string property = "color") =>
        gui.ResolvePart(part, modifiers: modifiers).GetColor(property) ?? fallback;

    static TextSpanStyle?[] ReadSyntax(Gui gui)
    {
        var styles = new TextSpanStyle?[SyntaxParts.Length];
        for (var i = 1; i < styles.Length; i++)
        {
            var part = gui.ResolvePart(SyntaxParts[i]);
            styles[i] = part.GetColor("color") is { } color
                ? new TextSpanStyle { Color = color, Italic = IsItalic(part.Get("font-style")) ? true : null }
                : SyntaxTheme.Dark.StyleOf((SyntaxKind)i);
        }

        return styles;
    }

    static bool IsItalic(string? fontStyle) => fontStyle?.Trim().ToLowerInvariant() is "italic" or "oblique";
}
