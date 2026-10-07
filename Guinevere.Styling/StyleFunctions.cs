namespace Guinevere;

/// <summary>
/// The functions and arithmetic available in <c>.pss</c> values. Percentages mean fractions where a 0..1 amount is
/// expected; mixing a percentage with a pixel length cannot be resolved and leaves the value as written.
/// </summary>
static class StyleFunctions
{
    /// <summary><c>shade()</c> contrast when the sheet defines no <c>$contrast</c> token.</summary>
    public const double DefaultContrast = 0.3;

    delegate StyleExprValue Function(List<StyleExprValue> args, Func<double> contrast, int position);

    static readonly Dictionary<string, (int Min, int Max, Function Body)> Functions = new(StringComparer.Ordinal)
    {
        ["calc"] = (1, 1, static (a, _, _) => a[0]),
        ["min"] = (1, int.MaxValue, static (a, _, p) => Extreme(a, p, max: false)),
        ["max"] = (1, int.MaxValue, static (a, _, p) => Extreme(a, p, max: true)),
        ["clamp"] = (3, 3, static (a, _, p) => Clamp(a, p)),
        ["rgb"] = (3, 4, static (a, _, p) => Rgb(a, p, 255d)),
        ["rgba"] = (3, 4, static (a, _, p) => Rgb(a, p, 255d)),
        ["rgb1"] = (3, 4, static (a, _, p) => Rgb(a, p, 1d)),
        ["rgba1"] = (3, 4, static (a, _, p) => Rgb(a, p, 1d)),
        ["hsl"] = (3, 4, static (a, _, p) => Hsl(a, p)),
        ["hsla"] = (3, 4, static (a, _, p) => Hsl(a, p)),
        ["mix"] = (3, 3, static (a, _, p) =>
            StyleExprValue.Of(ColorArg(a[0], p).Lerp(ColorArg(a[1], p), Amount(a[2], p)))),
        ["alpha"] = (2, 2, static (a, _, p) => StyleExprValue.Of(ColorArg(a[0], p).WithAlpha(Amount(a[1], p)))),
        ["lighten"] = (2, 2, static (a, _, p) => Toward(a, p, 1d)),
        ["darken"] = (2, 2, static (a, _, p) => Toward(a, p, 0d)),
        ["contrast-ink"] = (1, 3, static (a, _, p) => ContrastInk(a, p)),
        ["shade"] = (2, 4, static (a, c, p) => Shade(a, c, p)),
    };

    public static bool IsKnown(string name) => Functions.ContainsKey(name);

    /// <summary>Checks arity, then evaluates; any unknown argument makes the result unknown.</summary>
    public static StyleExprValue Invoke(string name, List<StyleExprValue> args, Func<double> contrast, int position)
    {
        var (min, max, body) = Functions[name];
        if (args.Count < min || args.Count > max)
            throw new StyleExpressionException($"{name}() expects {Arity(min, max)} arguments", position);
        foreach (var arg in args)
            if (arg.Kind == StyleValueKind.Unknown) return StyleExprValue.Unknown;
        return body(args, contrast, position);
    }

    /// <summary>
    /// <c>+ - * /</c> on numbers; a percentage scales with a plain factor. Other operands stay unevaluated, since
    /// PanGui also applies these operators to shapes.
    /// </summary>
    public static StyleExprValue Arithmetic(char op, StyleExprValue left, StyleExprValue right, int position)
    {
        if (left.Kind != StyleValueKind.Number || right.Kind != StyleValueKind.Number) return StyleExprValue.Unknown;
        return op switch
        {
            '+' or '-' => Sum(op, left, right),
            '*' => Product(left, right),
            _ => Quotient(left, right, position),
        };
    }

    static StyleExprValue Sum(char op, StyleExprValue left, StyleExprValue right) =>
        left.Unit != right.Unit
            ? StyleExprValue.Unknown
            : StyleExprValue.Of(op == '+' ? left.Number + right.Number : left.Number - right.Number, left.Unit);

    static StyleExprValue Product(StyleExprValue left, StyleExprValue right) =>
        left.Unit == StyleNumberUnit.Percent && right.Unit == StyleNumberUnit.Percent
            ? StyleExprValue.Unknown
            : StyleExprValue.Of(left.Number * right.Number,
                left.Unit == StyleNumberUnit.Percent ? left.Unit : right.Unit);

    static StyleExprValue Quotient(StyleExprValue left, StyleExprValue right, int position)
    {
        if (right.Unit == StyleNumberUnit.Percent) return StyleExprValue.Unknown;
        if (right.Number == 0d) throw new StyleExpressionException("Division by zero", position);
        return StyleExprValue.Of(left.Number / right.Number, left.Unit);
    }

    static StyleExprValue Extreme(List<StyleExprValue> args, int position, bool max)
    {
        var best = NumberArg(args[0], position);
        for (var i = 1; i < args.Count; i++)
        {
            var next = NumberArg(args[i], position);
            if (next.Unit != best.Unit) return StyleExprValue.Unknown;
            if (max ? next.Number > best.Number : next.Number < best.Number) best = next;
        }
        return StyleExprValue.Of(best.Number, best.Unit);
    }

    static StyleExprValue Clamp(List<StyleExprValue> args, int position)
    {
        var low = Extreme([args[0], args[1]], position, max: true);
        return low.Kind == StyleValueKind.Unknown ? low : Extreme([low, args[2]], position, max: false);
    }

    /// <summary>
    /// <c>rgb()</c>/<c>rgba()</c> take every channel, alpha included, in 0..<paramref name="full"/> (255) and
    /// <c>rgb1()</c>/<c>rgba1()</c> in 0..1, so <c>rgba(1, 1, 1, 1)</c> is never ambiguous; percentages work in both.
    /// </summary>
    static StyleExprValue Rgb(List<StyleExprValue> args, int position, double full) => StyleExprValue.Of(new StyleColor(
        Channel(args[0], position, full), Channel(args[1], position, full), Channel(args[2], position, full),
        args.Count == 4 ? Channel(args[3], position, full) : 1d));

    /// <summary>CSS <c>hsl()</c>: hue in degrees; saturation and lightness as percentages or 0..1.</summary>
    static StyleExprValue Hsl(List<StyleExprValue> args, int position) => StyleExprValue.Of(StyleColor.FromHsl(
        NumberArg(args[0], position).Number, Portion(args[1], position), Portion(args[2], position),
        args.Count == 4 ? Alpha(args[3], position) : 1d));

    /// <summary>Godot-style <c>lightened</c>/<c>darkened</c>: moves toward white or black, keeping alpha.</summary>
    static StyleExprValue Toward(List<StyleExprValue> args, int position, double target)
    {
        var color = ColorArg(args[0], position);
        return StyleExprValue.Of(color.Lerp(new StyleColor(target, target, target, color.A), Amount(args[1], position)));
    }

    /// <summary>The ink (default black or white) with the higher WCAG contrast against the background.</summary>
    static StyleExprValue ContrastInk(List<StyleExprValue> args, int position)
    {
        if (args.Count == 2) throw new StyleExpressionException("contrast-ink() expects 1 or 3 arguments", position);
        var background = ColorArg(args[0], position).Luminance();
        var dark = args.Count == 3 ? ColorArg(args[1], position) : new StyleColor(0d, 0d, 0d, 1d);
        var light = args.Count == 3 ? ColorArg(args[2], position) : new StyleColor(1d, 1d, 1d, 1d);
        return StyleExprValue.Of(Ratio(background, dark.Luminance()) >= Ratio(background, light.Luminance())
            ? dark
            : light);
    }

    /// <summary>
    /// Godot's editor elevation ladder: lerps the HSV value toward 0 by <c>contrast × offset</c> (negative offsets
    /// brighten, with contrast clamped to -0.1..0.5) and scales saturation.
    /// </summary>
    static StyleExprValue Shade(List<StyleExprValue> args, Func<double> contrastToken, int position)
    {
        var color = ColorArg(args[0], position);
        var offset = NumberArg(args[1], position).Number;
        var saturation = args.Count > 2 ? NumberArg(args[2], position).Number : 1d;
        var contrast = args.Count > 3 ? NumberArg(args[3], position).Number : contrastToken();
        if (offset < 0d) contrast = Math.Clamp(contrast, -0.1, 0.5);
        var (h, s, v) = color.ToHsv();
        var value = Math.Clamp(v + (0d - v) * contrast * offset, 0d, 1d);
        return StyleExprValue.Of(StyleColor.FromHsv(h, Math.Clamp(s * saturation, 0d, 1d), value, color.A));
    }

    static StyleColor ColorArg(StyleExprValue arg, int position)
    {
        if (arg.Kind == StyleValueKind.Color) return arg.Color;
        if (arg.Kind == StyleValueKind.Text && StyleValue.TryColor(arg.Text, out var color)) return StyleColor.From(color);
        throw new StyleExpressionException($"Expected a color, got '{arg.Canonical()}'", position);
    }

    static StyleExprValue NumberArg(StyleExprValue arg, int position) => arg.Kind == StyleValueKind.Number
        ? arg
        : throw new StyleExpressionException($"Expected a number, got '{arg.Canonical()}'", position);

    /// <summary>A 0..1 amount: a fraction or a percentage, clamped.</summary>
    static double Amount(StyleExprValue arg, int position) => Math.Clamp(Fraction(arg, position), 0d, 1d);

    static double Fraction(StyleExprValue arg, int position)
    {
        var number = NumberArg(arg, position);
        return number.Unit == StyleNumberUnit.Percent ? number.Number / 100d : number.Number;
    }

    static double Portion(StyleExprValue arg, int position)
    {
        var number = NumberArg(arg, position);
        var fraction = number.Unit == StyleNumberUnit.Percent || number.Number > 1d ? number.Number / 100d : number.Number;
        return Math.Clamp(fraction, 0d, 1d);
    }

    static double Channel(StyleExprValue arg, int position, double full)
    {
        var number = NumberArg(arg, position);
        return Math.Clamp(number.Number / (number.Unit == StyleNumberUnit.Percent ? 100d : full), 0d, 1d);
    }

    static double Alpha(StyleExprValue arg, int position)
    {
        var number = NumberArg(arg, position);
        var alpha = number.Unit == StyleNumberUnit.Percent ? number.Number / 100d
            : number.Number <= 1d ? number.Number : number.Number / 255d;
        return Math.Clamp(alpha, 0d, 1d);
    }

    static double Ratio(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    static string Arity(int min, int max) =>
        min == max ? $"{min}" : max == int.MaxValue ? $"at least {min}" : $"{min} to {max}";
}
