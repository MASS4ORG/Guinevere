# Charts

## Cartesian plots

- Create a `CartesianPlot` with screen bounds and visible X and Y ranges. Use `Pan` and `Zoom` to change the ranges.
- Use `CartesianTransforms.GenerateTicks` for numeric ticks and `CartesianChart.DrawGrid` for grid lines. Use `CartesianChart.DrawScale` and `ChartLabels.DrawTicks` with `ChartAxisSide` to place a scale, outward tick marks, and labels on any edge.
- Place titles and other text with `ChartLabels.Draw`, a bounding rectangle, and horizontal and vertical alignment.
- Draw series in display order and hit-test in reverse order to find the topmost item. Hit tests return a source index or `-1`.

### Line

- Use `CartesianChart.DrawLine` for straight segments or `DrawSmoothLine` for a curve that passes through each sample. Use `HitLineSample` to find a nearby visible sample.
- Missing or nonfinite samples break the line. For large finite series with nondecreasing X, use `CartesianTransforms.DecimateVisible` with the visible X range. Skip the series if the result is empty. Otherwise, pass the resulting indices to drawing and hit testing.

### Bar

- Use `CartesianChart.DrawBars` and `HitBars` with the same bar width in pixels and baseline in data units. The default baseline is zero.
- For multiple series, use `BarChart.Project`, `Draw`, and `HitTest`. Supply category positions, values ordered by category then series, and one stack index per series. Use indices such as `[0, 1, 2]` for grouped series, `[0, 0, 0]` for one stack, or `[0, 0, 1]` for grouped stacks.
- Each stack starts at the same baseline. Positive and negative values extend independently on opposite sides. Use `horizontal: true` with categories on Y and values on X for horizontal bars.
- Size the rectangle buffer for all values and draw the returned slice. Hit tests return an index into that slice, whose rectangle identifies its category and series.

### Scatter

- Use `CartesianChart.DrawPoints` and `HitPoints` with the same marker radius in pixels.

### Bubble

- Use `CartesianChart.DrawPoints` and `HitPoints` with a radius in pixels for each source point, in the same order.

### Histogram

- Use `CartesianTransforms.Histogram` with a value range and a bin buffer. The buffer length sets the number of equal-width bins.
- Use each bin center as X and its count as Y. Draw and hit-test the resulting series with `CartesianChart.DrawBars` and `HitBars`.

## Pie

- Use `PieChart.CreateSegments` with positive weights, a start angle, and an optional gap. Angles are clockwise radians from the positive X axis.
- Size the segment buffer for the returned count and pass the populated span to `PieChart.Draw` and `HitTest`. Use `PieSelection` to highlight or offset a slice. `PieChart.DrawLabels` fits source labels inside the slices and skips tight spaces. Use `ChartLegend.Draw` to show every label.
- An empty set of usable weights returns zero segments. Use the empty result to show a placeholder if needed.

## Donut

- Use the pie APIs with the same fractional `holeRadius` between zero and one for drawing and hit testing.
- Draw concentric rings by preparing one span per ring and specifying its fractional `holeRadius` and `outerRadius` within the same rectangle. Hit-test the rings in reverse draw order with the same radii and selection.

## Arc

- Pass `angularSweep` in radians to `PieChart.CreateSegments` to lay out weighted slices within a partial circle. Use the same sweep with the weight-based `HitTest` overload.
- Use `holeRadius` and `outerRadius` to turn a partial pie into an arc band.

## Radar

- Use `RadarProjection.CreateAxes` for the axes and `Project` to map each series with one range per dimension. Missing values leave gaps.
- Use `RadarChart.DrawGrid` and `DrawSeries` to display the chart. Use `RadarChart.HitSeries` or `RadarProjection.HitTestAxis` to identify a dimension.
- Pass axis names and a rectangle buffer to `RadarChart.DrawAxisLabels`. The method skips labels that would overlap or leave the supplied bounds. Use a legend or tooltip to surface skipped labels.

## Timeline

- Create a `TimelinePlot` with screen bounds, visible time range, and row height. Supply a `TimelineSpan` with a start, end, and row for each item.
- Use `TimelineChart.Draw` and `HitTest` with matching row and interval spacing. Use `DrawInterval` if you also need the bounds for a label or selection outline.

## Flame

- Use `FlameLayout.TryCalculateDepths` to derive rows from parent indices. A parent index of `-1` denotes a root.
- Supply sorted, nonoverlapping intervals per row to `FlameChart.Draw` and `HitTest`. Use `FlameRowIndex.IsValid` to check each row. Set the first visible row and time range to navigate the plot.
- Use `FlameLayout.Search` to find labels and `WritePath` to build breadcrumbs from the selected source index.

## Styling and animation

- Enter a `gui.StyledNode("chart")` and use the colors from `ChartStyle.Resolve(gui)`.
- Style `chart-axis` and `chart-grid` with `border-color`, `chart-label` with `color`, `chart-selection` with `background-color`, and `chart-tooltip` with `color` and `background-color`.
- Use `ChartLabels.DrawTooltip` and `ChartLegend.Draw` within the available screen bounds.
- Use `ChartAnimation.Interpolate` to animate values or `InterpolatePoints` to animate line and scatter coordinates. Supply a destination buffer and a progress value each frame. Keep matching samples in source order.
