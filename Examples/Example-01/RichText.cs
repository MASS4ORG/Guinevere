using Guinevere;
using SkiaSharp;

namespace Example01;

public partial class Shared
{
    const string CodeSample = """
        // Rich text spans share one cached layout.
        public sealed class Greeter(string name)
        {
            public string Greet(int times)
            {
                var text = $"Hello, {name}!";
                return string.Concat(Enumerable.Repeat(text, times * 2));
            }
        }
        """;

    static readonly HashSet<string> Keywords =
    [
        "public", "sealed", "class", "string", "int", "var", "return", "new", "static", "void", "using",
    ];

    static (ColorPalette Palette, RichText Label, RichText Code)? _richText;
    static SKImage? _richTextBadge;
    static string _lastLink = "none yet";
    static string _hoveredLink = "none";

    /// <summary>Mixed fonts, sizes, colors, decorations, links and an inline image, wrapping as one block.</summary>
    internal static void RichTextDemo(Gui gui, ColorPalette p)
    {
        var (label, code) = RichTextSamples(p);
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(14).Enter())
        {
            gui.ScrollY(p.Track, p.Border);
            Label(gui, p, "Rich label: wraps to the panel width in the frame it resizes");
            using (gui.Node().ExpandWidth().Padding(16).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                TrackLinks(gui, gui.DrawRichText(label, TextLayoutOptions.FillWidth, 16, p.Text));
            }

            Label(gui, p, $"Hovered link: {_hoveredLink}   Last link clicked: {_lastLink}");
            Label(gui, p, "Syntax-highlighted code");
            using (gui.Node().ExpandWidth().Padding(16).Enter())
            {
                gui.DrawBackgroundRect(p.Surface, 12);
                gui.DrawRichText(code, size: 14, color: p.Text, font: gui.Fonts.ResolveRole(FontRole.Code));
            }
        }
    }

    /// <summary>The last link clicked and the link under the pointer, for the status line.</summary>
    internal static (string Hovered, string Clicked) RichTextLinks => (_hoveredLink, _lastLink);

    static void TrackLinks(Gui gui, RichTextResult result)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        _hoveredLink = result.Hovered?.Target ?? "none";
        _lastLink = result.Clicked?.Target ?? _lastLink;
    }

    static (RichText Label, RichText Code) RichTextSamples(ColorPalette p)
    {
        if (_richText is { } cached && cached.Palette == p)
        {
            return (cached.Label, cached.Code);
        }

        _richTextBadge ??= Badge();
        var label = new RichText()
            .Append("Guinevere draws ")
            .Append("bold", TextSpanStyle.Bold)
            .Append(", ")
            .Append("italic", TextSpanStyle.Slanted)
            .Append(", ")
            .Append("colored", TextSpanStyle.Colored(p.Accent))
            .Append(", ")
            .Append("BIG", new TextSpanStyle { Size = 26, Color = p.Pick(1) })
            .Append(" and ")
            .Append("small", new TextSpanStyle { Size = 11 })
            .Append(" text, ")
            .Append("underlined", TextSpanStyle.Underlined)
            .Append(", ")
            .Append("struck", TextSpanStyle.Struck with { DecorationColor = p.Negative })
            .Append(" and ")
            .Append("highlighted", new TextSpanStyle { Background = new Color(p.Warning, 0.35f) })
            .Append(" spans, E = mc")
            .Append("2", new TextSpanStyle { Size = 10, BaselineShift = 7 })
            .Append(", H")
            .Append("2", new TextSpanStyle { Size = 10, BaselineShift = -3 })
            .Append("O, inline images ")
            .AppendImage(_richTextBadge, 18, 18)
            .Append(" and links like ")
            .AppendLink("the repository", "https://github.com/MASS4ORG/Guinevere",
                new TextSpanStyle { Color = p.Accent, Decoration = TextDecoration.Underline })
            .Append(" or ")
            .AppendLink("the roadmap", "roadmap")
            .Append(", in one wrapped, hit-testable block.");
        var code = Highlight(CodeSample, p);
        _richText = (p, label, code);
        return (label, code);
    }

    /// <summary>A tiny C# tokenizer: comments, strings, numbers, keywords and capitalized type names.</summary>
    internal static RichText Highlight(string source, ColorPalette p)
    {
        var text = new RichText(source);
        var start = 0;
        while (start < source.Length)
        {
            var (end, style) = Token(source, start, p);
            if (style is { } value)
            {
                text.Style(start, end - start, value);
            }

            start = end;
        }

        return text;
    }

    static (int End, TextSpanStyle? Style) Token(string source, int start, ColorPalette p)
    {
        var c = source[start];
        if (source.AsSpan(start).StartsWith("//"))
        {
            return (Until(source, '\n', start, 0), new TextSpanStyle { Color = p.TextDim, Italic = true });
        }

        if (c == '"')
        {
            return (Until(source, '"', start + 1, 1), TextSpanStyle.Colored(p.Positive));
        }

        if (char.IsDigit(c))
        {
            return (While(source, start, char.IsDigit), TextSpanStyle.Colored(p.Warning));
        }

        if (!char.IsLetter(c))
        {
            return (start + 1, null);
        }

        var end = While(source, start, char.IsLetterOrDigit);
        var word = source[start..end];
        return (end, Keywords.Contains(word) ? new TextSpanStyle { Color = p.Accent, Weight = 700 }
            : char.IsUpper(word[0]) ? TextSpanStyle.Colored(p.Pick(4))
            : null);
    }

    static int Until(string source, char stop, int from, int include) =>
        source.IndexOf(stop, from) is var end and >= 0 ? end + include : source.Length;

    static int While(string source, int from, Func<char, bool> accept)
    {
        while (from < source.Length && accept(source[from]))
        {
            from++;
        }

        return from;
    }

    static SKImage Badge()
    {
        using var surface = SKSurface.Create(new SKImageInfo(36, 36));
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xff3ecf8e) };
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawCircle(18, 18, 17, paint);
        paint.Color = SKColors.White;
        surface.Canvas.DrawCircle(18, 18, 7, paint);
        return surface.Snapshot();
    }
}
