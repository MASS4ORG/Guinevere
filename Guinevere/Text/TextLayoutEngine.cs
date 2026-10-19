namespace Guinevere;

/// <summary>Fonts and constraints a text block is laid out with; compares by value so it can key the cache.</summary>
/// <param name="Content">The styled source.</param>
/// <param name="Font">Inherited font at its display-scaled size.</param>
/// <param name="Emoji">Last-resort fallback typeface.</param>
/// <param name="Widget">Icon font consulted during fallback.</param>
/// <param name="Scale">Display scale applied to logical sizes in span styles.</param>
/// <param name="MaxWidth">Wrap width, or zero or less for none.</param>
/// <param name="Options">Wrapping and truncation options.</param>
/// <param name="Alignment">Horizontal placement of each line.</param>
/// <param name="FontsVersion">Font registry version, so registering faces relayouts styled spans.</param>
/// <param name="Shaper">Turns font runs into glyphs.</param>
readonly record struct TextLayoutRequest(RichTextContent Content, FontKey Font, SKTypeface Emoji, Font Widget,
    float Scale, float MaxWidth, TextLayoutOptions Options, TextAlignment Alignment, int FontsVersion,
    ITextShaper Shaper);

/// <summary>Value identity of a face at a size; fonts are otherwise recreated per call.</summary>
/// <param name="Font">The face, at any size.</param>
/// <param name="Size">The display-scaled size it is laid out at.</param>
readonly record struct FontKey(Font Font, float Size)
{
    /// <summary>The face at <see cref="Size"/>.</summary>
    internal Font Resolve() => Font.Size == Size ? Font : Font.Resized(Size);

    /// <inheritdoc />
    public bool Equals(FontKey other)
    {
        var a = Font.SkFont;
        var b = other.Font.SkFont;
        return Size == other.Size && (ReferenceEquals(Font, other.Font) || (a.Typeface == b.Typeface
            && a.Embolden == b.Embolden && a.SkewX == b.SkewX && Font.Fallbacks == other.Font.Fallbacks));
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Font.SkFont.Typeface, Size);
}

/// <summary>
/// Builds a <see cref="TextLayout"/> from styled content: font selection, shaping, spacing, wrapping, tabs,
/// indents, justification and runs.
/// </summary>
static partial class TextLayoutEngine
{
    enum PieceKind { Text, Hidden, Image, Missing }

    /// <summary>A source range shaped with one font; <see cref="Shaped"/> is null for images and hidden ranges.</summary>
    readonly record struct Piece(int Start, int End, Font Font, int Range, PieceKind Kind, ShapedText? Shaped);

    /// <summary>Per-range spacing in pixels and kerning, after inheriting the block's options.</summary>
    readonly record struct RangeSpacing(float Letter, float Word, bool Kerning);

    sealed partial class Build(TextLayoutRequest request, FontRegistry? fonts)
    {
        readonly Font _base = request.Font.Resolve();
        readonly string _text = request.Content.Text;
        readonly StyledRange[] _ranges = request.Content.Ranges;
        readonly TextLayoutOptions _options = request.Options;
        readonly Dictionary<float, Font> _emoji = [];
        readonly List<Piece> _pieces = [];
        readonly List<int> _tabs = [];
        Font[] _rangeFonts = [];
        string _display = "";
        float[] _advance = [];
        float[] _prefix = [];

        internal TextLayout Run()
        {
            _rangeFonts = new Font[_ranges.Length];
            for (var i = 0; i < _ranges.Length; i++)
            {
                _rangeFonts[i] = ResolveFont(_ranges[i].Style);
            }

            _display = DisplayText();
            _advance = new float[_text.Length];
            for (var i = 0; i < _ranges.Length; i++)
            {
                AppendPieces(i);
            }

            _prefix = Prefix();
            var ranges = WrappedTextLayout.WrapRanges(_text, request.MaxWidth, Measure, _options);
            return Lay(ranges);
        }

        Font ResolveFont(TextSpanStyle style)
        {
            var font = style.Font ?? _base;
            var size = style.Size is { } logical ? logical * request.Scale : _base.Size;
            if (style.Weight is null && style.Italic is null)
            {
                return font.Size == size ? font : font.Resized(size);
            }

            return Restyle(font, size, Math.Clamp(style.Weight ?? font.Weight, 1, 1000), style.Italic ?? font.Italic);
        }

        /// <summary>A registered face for the weight and slant, or the face synthesized when none is registered.</summary>
        Font Restyle(Font font, float size, int weight, bool italic) =>
            fonts?.Resolve(font.Families ?? $"\"{font.FamilyName}\"", size, weight, italic)
            ?? font.Resized(size).Restyled(weight, italic);

        Font EmojiAt(float size)
        {
            if (!_emoji.TryGetValue(size, out var font))
            {
                _emoji[size] = font = new Font(new SKFont(request.Emoji, size));
            }

            return font;
        }

        /// <summary>The source with each range's letter case applied character by character, keeping offsets.</summary>
        string DisplayText()
        {
            char[]? chars = null;
            foreach (var range in _ranges)
            {
                var textCase = range.Style.Case ?? _options.Case;
                if (textCase == TextCase.None)
                {
                    continue;
                }

                chars ??= _text.ToCharArray();
                for (var i = range.Start; i < range.End; i++)
                {
                    chars[i] = Cased(chars[i], textCase, i);
                }
            }

            return chars is null ? _text : new string(chars);
        }

        char Cased(char value, TextCase textCase, int offset) => textCase switch
        {
            TextCase.Upper => char.ToUpperInvariant(value),
            TextCase.Lower => char.ToLowerInvariant(value),
            _ => offset == 0 || !char.IsLetterOrDigit(_text[offset - 1]) ? char.ToUpperInvariant(value) : value
        };

        RangeSpacing Spacing(int index)
        {
            var style = _ranges[index].Style;
            var size = _rangeFonts[index].Size;
            return new RangeSpacing((style.LetterSpacing ?? _options.LetterSpacing) * size,
                (style.WordSpacing ?? _options.WordSpacing) * size, style.Kerning ?? _options.Kerning);
        }

        void AppendPieces(int index)
        {
            var range = _ranges[index];
            var font = _rangeFonts[index];
            if (range.Image is { } image)
            {
                _pieces.Add(new Piece(range.Start, range.End, font, index, PieceKind.Image, null));
                if (range.End > range.Start)
                {
                    _advance[range.Start] = image.Width * request.Scale;
                }

                return;
            }

            var fontRanges = new List<FontRange>();
            FontTextLayout.AppendRanges(fontRanges, _display, range.Start, range.End, font, request.Widget,
                EmojiAt(font.Size));
            var spacing = Spacing(index);
            foreach (var fontRange in fontRanges)
            {
                _pieces.Add(Shape(fontRange, index, spacing));
            }
        }

        Piece Shape(FontRange range, int index, RangeSpacing spacing)
        {
            if (range.Hidden)
            {
                return new Piece(range.Start, range.End, range.Font, index, PieceKind.Hidden, null);
            }

            var missing = range.Missing && _options.MissingGlyph is { Length: > 0 };
            var shaped = missing
                ? Placeholders(range)
                : request.Shaper.Shape(range.Font, _display, range.Start, range.End - range.Start, spacing.Kerning);
            AddAdvances(range.Start, shaped, spacing);
            return new Piece(range.Start, range.End, range.Font, index, missing ? PieceKind.Missing : PieceKind.Text,
                shaped);
        }

        /// <summary>The missing-glyph placeholder shaped once per unrenderable character.</summary>
        ShapedText Placeholders(FontRange range)
        {
            var placeholder = _options.MissingGlyph!;
            var font = range.Font.SkFont.GetGlyph(char.ConvertToUtf32(placeholder, 0)) != 0
                ? range.Font
                : EmojiAt(range.Font.Size);
            var one = request.Shaper.Shape(font, placeholder, 0, placeholder.Length, false);
            var glyphs = new List<ushort>();
            var advances = new List<float>();
            var clusters = new List<int>();
            var offset = 0;
            foreach (var rune in _display.AsSpan(range.Start, range.End - range.Start).EnumerateRunes())
            {
                glyphs.AddRange(one.Glyphs);
                advances.AddRange(one.Advances);
                clusters.AddRange(Enumerable.Repeat(offset, one.Glyphs.Length));
                offset += rune.Utf16SequenceLength;
            }

            return new ShapedText([.. glyphs], [.. advances], [.. clusters]);
        }

        /// <summary>Adds glyph advances to their source characters, with letter and word spacing.</summary>
        void AddAdvances(int start, ShapedText shaped, RangeSpacing spacing)
        {
            var previousCluster = -1;
            for (var g = 0; g < shaped.Glyphs.Length; g++)
            {
                var offset = start + shaped.Clusters[g];
                if (char.IsControl(_display[offset]))
                {
                    continue;
                }

                _advance[offset] += shaped.Advances[g];
                if (shaped.Clusters[g] == previousCluster)
                {
                    continue;
                }

                previousCluster = shaped.Clusters[g];
                _advance[offset] += spacing.Letter + (_display[offset] is ' ' or ' ' ? spacing.Word : 0f);
            }
        }

        /// <summary>Running advance sums, so any tab-free range measures in constant time.</summary>
        float[] Prefix()
        {
            var prefix = new float[_text.Length + 1];
            for (var i = 0; i < _text.Length; i++)
            {
                prefix[i + 1] = prefix[i] + _advance[i];
                if (_text[i] == '\t')
                {
                    _tabs.Add(i);
                }
            }

            return prefix;
        }

        float Measure(int start, int end, string suffix)
        {
            var width = LineX(start, end);
            if (suffix.Length == 0)
            {
                return width;
            }

            foreach (var marker in MarkerRuns(suffix, end))
            {
                width += Sum(marker.Shaped.Advances);
            }

            return width;
        }

        static float Sum(float[] values)
        {
            var total = 0f;
            foreach (var value in values)
            {
                total += value;
            }

            return total;
        }

        int RangeAt(int offset)
        {
            for (var i = _ranges.Length - 1; i >= 0; i--)
            {
                if (_ranges[i].Start < offset || (_ranges[i].Start == offset && offset == 0))
                {
                    return i;
                }
            }

            return _ranges.Length > 0 ? 0 : -1;
        }

        /// <summary>A truncation marker split by fallback font and shaped in the style of the text before it.</summary>
        List<(string Text, Font Font, ShapedText Shaped)> MarkerRuns(string marker, int end)
        {
            var range = RangeAt(end);
            var font = range >= 0 ? _rangeFonts[range] : _base;
            var runs = new List<(string, Font, ShapedText)>();
            foreach (var run in FontTextLayout.Create(marker, font, request.Widget, EmojiAt(font.Size)))
            {
                runs.Add((run.Text, run.Font, request.Shaper.Shape(run.Font, run.Text, 0, run.Text.Length, false)));
            }

            return runs;
        }
    }

    /// <summary>The line box: the tallest ascent and descent of its runs, each with half-leading.</summary>
    struct LineMetrics
    {
        internal float Above;
        internal float Below;
        internal bool Any;

        internal void Add(Font font, RichTextImage? image, float shift, TextLayoutRequest request)
        {
            float above;
            float below;
            if (image is not null)
            {
                above = image.Height * request.Scale + shift;
                below = -shift;
            }
            else
            {
                var metrics = font.SkFont.Metrics;
                var ascent = -metrics.Ascent;
                var descent = metrics.Descent;
                var leading = (font.Size * request.Options.LineHeight - (ascent + descent)) / 2f;
                above = ascent + leading + shift;
                below = descent + leading - shift;
            }

            Above = Any ? Math.Max(Above, above) : above;
            Below = Any ? Math.Max(Below, below) : below;
            Any = true;
        }
    }

    /// <summary>Lays out <paramref name="request"/>; <paramref name="fonts"/> supplies real bold and italic faces.</summary>
    internal static TextLayout Create(TextLayoutRequest request, FontRegistry? fonts) =>
        new Build(request, fonts).Run();
}
