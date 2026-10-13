# Example-74-PanGui-Heart

The original PanGui demonstration of signed distance field based shapes. It combines circles and a triangle with movement, union, intersection, subtraction, onion, scale and mix operations to form a pulsing heart.

Stages morph over 0.65 seconds and hold for the rest of each 3.5-second interval. The final stage blends the heart with an orbiting circle, changing its contour without accumulating rotations. Hover smoothly enlarges the shape; captions remain outside its clipping area.

Guinevere reconstructs Skia paths from sampled signed distances for `Mix` and smooth `Union`. This preserves the demo's expressions and supports ordinary path rendering and shadows, but it is an approximation: details smaller than a sampling cell can disappear, and large shapes use a coarser grid. Stable stage geometry is cached, and animated geometry is calculated once for both GUI passes.

## Original Code

https://pangui.io/ provides this demonstration as a video: https://pangui.io/assets/output2.mp4. The operations displayed in the video are reproduced in `Program.cs`.
