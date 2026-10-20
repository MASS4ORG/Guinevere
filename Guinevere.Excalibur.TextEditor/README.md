# Guinevere.Excalibur.TextEditor

A virtualized text editor with optional language services for [Guinevere](https://github.com/MASS4ORG/Guinevere).

Install `MASS4.Guinevere.Excalibur.TextEditor` to use the control and its SumTree v2 persistent buffer. Public types and GUI extensions use the `Guinevere` namespace.

```csharp
var editor = new TextEditorState(source, LanguageServices.ForLanguage("glsl"));

// every frame
gui.TextEditor(editor);
```

- The viewport lays out and draws visible lines over `SumTreeTextBuffer`, a persistent rope with O(log n) edits and line lookups. A rectangular gesture also hit-tests the rows in its selected range. Any `ITextBuffer` works, such as core's `StringTextBuffer` for small texts.
- Gutter with line numbers and fold markers, current-line highlight, bracket matching, find and replace (plain, whole word, regex), multiple carets (`Alt`+click, `Ctrl`+`Alt`+arrows, `Ctrl`+`D`), indentation, line comments, line moves, auto-closing pairs and undo/redo with typing groups.
- Hold `Alt`+`Shift` and drag to select a rectangle across visible lines. Its edges follow rendered glyph positions and tab stops; short lines clamp to their ends without adding spaces, and a zero-width rectangle creates multiple carets. Type, delete, copy, cut and paste use the normal multi-caret commands, and undo restores the selections and primary caret. The gesture scrolls at viewport edges and stops on mouse release, Escape or a document edit; folded lines stay unselected.
- Editing actions are named commands (`state.Execute("deleteLeft")`) bound through an `EditorKeymap`, so menus, scripts and tests run exactly what keys run.
- Language logic comes from `LanguageServices`: an `ISyntaxHighlighter` (shared with Markdown code blocks) and asynchronous `IDiagnosticsProvider`, `IFoldingProvider`, `ICompletionProvider` and `IHoverProvider` that read immutable snapshots, so they can run on any thread.
- Caret steps and deletions never split a grapheme cluster, and measuring, drawing and hit testing use the shared text layout and shaper.
- Input-method composition is drawn at the caret when the integration provides `ITextCompositionCapability`. Bidirectional text is not supported yet.
