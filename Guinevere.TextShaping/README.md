# Guinevere.TextShaping

Full OpenType text shaping for [Guinevere](https://github.com/MASS4ORG/Guinevere): ligatures, contextual alternates, mark positioning, kerning and complex scripts.

Without this package Guinevere uses its built-in `BasicTextShaper`, which skips shaping: one glyph per character from the font's character map, plus GPOS/kern pair kerning. Plug this package in to shape every text layout (labels, rich text, Markdown, editors):

```csharp
gui.TextShaper = ComplexTextShaper.Instance;
```

Layouts stay cached across frames. Kerning still follows `TextLayoutOptions.Kerning` and `TextSpanStyle.Kerning`.

The shaping engine is currently HarfBuzz, through `SkiaSharp.HarfBuzz`; it brings the HarfBuzzSharp native library for each platform.
