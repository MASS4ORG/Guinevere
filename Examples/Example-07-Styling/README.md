# Example-76-Styling

A compact, interactive tour of Guinevere's stylesheet integration. It demonstrates PanGui-compatible
assignment syntax, constants and variables, nested and direct-child selectors, `#inherit`, built-in
`:hover` and custom `:selected` modifiers, immediate-mode variables, theme tokens, and atomic provider
reloads.

Run it with:

```bash
dotnet run --project Examples/Example-76-Styling/
```

Transition annotations such as `:hover(0.15 ease-out)` interpolate colors, shadows and the `scale`/`translate` transforms: buttons grow on hover and shrink while pressed, and the feature card lifts. Styled shape/effect stacks remain planned features.
