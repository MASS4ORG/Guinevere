# Guinevere.Excalibur.TextEditor

Persistent text buffers, document history and text navigation for Guinevere.

The package uses `com.BrunoMassa.SumTree` 2.0.0 for immutable buffers with shared storage. Core Guinevere defines `ITextBuffer` and provides `StringTextBuffer` for small texts; the SumTree dependency belongs to this separate package.

```csharp
using Guinevere;

var document = new TextDocument(new SumTreeTextBuffer("hello"));
document.Apply([new TextEdit(5, 0, " world")], [TextSelection.At(11)]);
document.Undo();
document.Redo();
```

Offsets and columns count UTF-16 code units, and line numbers are zero-based. Buffers normalize line endings to `\n`; `TextDocument.Load` remembers the original line-ending style for `ToFileText`. `TextNavigation` moves by grapheme boundaries, words and visual tab columns.
