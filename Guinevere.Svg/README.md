# Guinevere.Svg

SVG icons for Guinevere. Core stays dependency-free; this optional package decodes SVG with [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) into a vector picture that `gui.Icon` scales to any size without re-parsing.

```csharp
var folder = SvgIcon.FromFile("icons/folder.svg", tintable: true); // load once, reuse every frame
gui.Icon(folder, 16, tint: Color.Gold);
```

With `MASS4.Guinevere.Styling`, register the decoder so `.pss` icon themes can use SVG sources:

```csharp
gui.IconDecoders.Add(SvgIconDecoder.Instance);
gui.StyledIcon("asset.folder", 16);
```

```css
icon#asset\.folder { src = url("svg/folder.svg"); tint = true; color = $folder; }
```

Dependency cost: Svg.Skia brings Svg.Model, Svg.Custom, ExCSS and HarfBuzzSharp with its native libraries per platform.
