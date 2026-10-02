# Example-76 — Autoformers: Attributes

A tour of **Autoformers** (`Autoformers` + `Autoformers.Excalibur`) in the style of a well-known
inspector's attribute gallery. Instead of screenshots, every page shows the real thing: the live, editable form and
the C# class that produces it.

The demo classes live in `Demos/`. Each file is compiled *and* embedded, so the code on screen is exactly the code
that builds the form.

## Pages

- **Types**: plain classes with no attributes: primitives, vectors and colors, lists and arrays, dictionaries,
  nested classes (compartments), nested structs (written back) and nullables.
- **Attributes**: `[Show]`, `[Hide]`, `[SetOrder]`, `[ReadOnly]`, `[Required]`, `[Title]`, `[Tooltip]`,
  `[HideLabel]`, `[GUIColor]`, `[Button]`, `[Range]`, `[TextArea]`, `[ListDrawerSettings]`, `[EnumLabel]`.

## Layout

Wide windows show the code to the right of the result; narrower ones move it below. Code uses the first installed
monospaced font (JetBrains Mono, Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, Liberation Mono).

## Controls

- **Click** a sidebar entry to open its page; every page keeps its own object while the app runs.
- **Edit** any value in the result; drag a numeric label or a vector axis letter to scrub it.
- **Lists**: `+` / `-` add and remove, drag the grip to reorder, `<` `>` turn pages.
- **Click** a heading to fold a list or a nested object.
