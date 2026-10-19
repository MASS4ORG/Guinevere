using System.Collections.Frozen;

namespace Guinevere;

/// <summary>
/// Accumulates the text-layout declarations of one resolved style onto the inherited options, building a new options
/// record at most once and only when a declaration changed a value.
/// </summary>
struct StyleTextLayout(TextLayoutOptions options)
{
    delegate void Parser(ref Draft draft, string value);

    static readonly FrozenDictionary<string, TextAlignment> Alignments = new Dictionary<string, TextAlignment>
    {
        ["start"] = TextAlignment.Start,
        ["left"] = TextAlignment.Start,
        ["center"] = TextAlignment.Center,
        ["end"] = TextAlignment.End,
        ["right"] = TextAlignment.End,
        ["justify"] = TextAlignment.Justify,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<string, TextCase> Cases = new Dictionary<string, TextCase>
    {
        ["none"] = TextCase.None,
        ["uppercase"] = TextCase.Upper,
        ["lowercase"] = TextCase.Lower,
        ["capitalize"] = TextCase.Capitalize,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Each property's parser; an invalid value leaves the draft unchanged.</summary>
    static readonly FrozenDictionary<string, Parser> Properties = new Dictionary<string, Parser>
    {
        ["text-wrap"] = static (ref d, v) =>
        {
            if (Enum.TryParse<TextWrapMode>(v.Replace("-", "", StringComparison.Ordinal), true, out var mode))
            {
                Set(ref d.WrapMode, mode, ref d.Changed);
            }
        },
        ["text-ellipsis"] = static (ref d, v) => Set(ref d.Ellipsis, Unquote(v), ref d.Changed),
        ["line-height"] = static (ref d, v) => SetIf(ref d.LineHeight, Positive(v), ref d.Changed),
        ["max-lines"] = static (ref d, v) =>
            SetIf(ref d.MaxLines, int.TryParse(v, out var lines) && lines >= 0 ? lines : null, ref d.Changed),
        ["letter-spacing"] = static (ref d, v) => SetIf(ref d.LetterSpacing, Em(v), ref d.Changed),
        ["word-spacing"] = static (ref d, v) => SetIf(ref d.WordSpacing, Em(v), ref d.Changed),
        ["tab-size"] = static (ref d, v) => SetIf(ref d.TabSize, NonNegative(v), ref d.Changed),
        ["tab-stops"] = static (ref d, v) => SetStops(ref d, Lengths(v)),
        ["text-indent"] = static (ref d, v) => SetIf(ref d.FirstLineIndent, Pixels(v), ref d.Changed),
        ["hanging-indent"] = static (ref d, v) => SetIf(ref d.HangingIndent, Pixels(v), ref d.Changed),
        ["text-align"] = static (ref d, v) =>
        {
            if (Alignments.TryGetValue(v, out var align))
            {
                Set(ref d.Alignment, (TextAlignment?)align, ref d.Changed);
            }
        },
        ["text-align-last"] = static (ref d, v) =>
        {
            if (Alignments.TryGetValue(v, out var last))
            {
                Set(ref d.LastLineAlignment, last, ref d.Changed);
            }
        },
        ["text-transform"] = static (ref d, v) =>
        {
            if (Cases.TryGetValue(v, out var textCase))
            {
                Set(ref d.Case, textCase, ref d.Changed);
            }
        },
        ["font-kerning"] = static (ref d, v) => Set(ref d.Kerning, v != "none", ref d.Changed),
        ["missing-glyph"] = static (ref d, v) => Set(ref d.MissingGlyph, v == "none" ? null : Unquote(v),
            ref d.Changed),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    Draft _draft = new(options);

    /// <summary>Whether any declaration changed the inherited options.</summary>
    public readonly bool Changed => _draft.Changed;

    /// <summary>The options with every applied declaration; builds a new record only when something changed.</summary>
    public readonly TextLayoutOptions Options => _draft.Changed ? _draft.Build() : options;

    /// <summary>Applies one declaration; returns false when the property is not a text-layout property.</summary>
    public bool Apply(string prop, string value)
    {
        if (!Properties.TryGetValue(prop, out var parse))
        {
            return false;
        }

        parse(ref _draft, value);
        return true;
    }

    static void Set<T>(ref T field, T value, ref bool changed)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        changed = true;
    }

    static void SetIf<T>(ref T field, T? value, ref bool changed) where T : struct
    {
        if (value is { } parsed)
        {
            Set(ref field, parsed, ref changed);
        }
    }

    static void SetStops(ref Draft draft, float[] stops)
    {
        if (!draft.TabStops.SequenceEqual(stops))
        {
            draft.TabStops = stops;
            draft.Changed = true;
        }
    }

    static string Unquote(string value) => value.Trim('"', '\'');

    static float? Positive(string value) => StyleValue.TryFloat(value, out var number) && number > 0f ? number : null;

    static float? NonNegative(string value) =>
        StyleValue.TryFloat(value, out var number) && number >= 0f ? number : null;

    /// <summary>A length in ems: <c>0.1em</c> or a bare number.</summary>
    static float? Em(string value) =>
        StyleValue.TryFloat(value.EndsWith("em", StringComparison.Ordinal) ? value[..^2] : value, out var ems)
            ? ems
            : null;

    static float? Pixels(string value) => StyleValue.TryLength(value, out var length, out var percent) && !percent
        ? length
        : null;

    static float[] Lengths(string value)
    {
        var stops = new List<float>();
        foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Pixels(part) is { } stop)
            {
                stops.Add(stop);
            }
        }

        return [.. stops.Order()];
    }

    /// <summary>A mutable copy of the options' values, so declarations do not each allocate a record.</summary>
    struct Draft(TextLayoutOptions options)
    {
        internal bool Changed;
        internal TextWrapMode WrapMode = options.WrapMode;
        internal float LineHeight = options.LineHeight;
        internal int MaxLines = options.MaxLines;
        internal string Ellipsis = options.Ellipsis;
        internal float LetterSpacing = options.LetterSpacing;
        internal float WordSpacing = options.WordSpacing;
        internal float TabSize = options.TabSize;
        internal IReadOnlyList<float> TabStops = options.TabStops;
        internal float FirstLineIndent = options.FirstLineIndent;
        internal float HangingIndent = options.HangingIndent;
        internal TextAlignment? Alignment = options.Alignment;
        internal TextAlignment LastLineAlignment = options.LastLineAlignment;
        internal TextCase Case = options.Case;
        internal bool Kerning = options.Kerning;
        internal string? MissingGlyph = options.MissingGlyph;

        internal readonly TextLayoutOptions Build() => new()
        {
            WrapMode = WrapMode,
            LineHeight = LineHeight,
            MaxLines = MaxLines,
            Ellipsis = Ellipsis,
            LetterSpacing = LetterSpacing,
            WordSpacing = WordSpacing,
            TabSize = TabSize,
            TabStops = TabStops,
            FirstLineIndent = FirstLineIndent,
            HangingIndent = HangingIndent,
            Alignment = Alignment,
            LastLineAlignment = LastLineAlignment,
            Case = Case,
            Kerning = Kerning,
            MissingGlyph = MissingGlyph,
        };
    }
}
