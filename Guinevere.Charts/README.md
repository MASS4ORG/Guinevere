# Guinevere Charts

- Package `MASS4.Guinevere.Charts` for .NET 10.
- See [Example 04](../Examples/Example-04-Charts/) for a complete Excalibur gallery.

## Cartesian plots

- Create a `CartesianPlot` with screen bounds and visible X and Y ranges. Use `Pan` and `Zoom` to change the ranges.
- Use `CartesianTransforms.GenerateTicks` for numeric ticks, `CartesianChart.DrawGrid` for grid lines, and `ChartLabels.DrawTicks` for axis labels.
- Draw series in display order and hit-test in reverse order to find the topmost item. Hit tests return a source index or `-1`.

### Line

- Use `CartesianChart.DrawLine` to connect samples and `HitLineSample` to find a nearby visible sample.
- Missing or nonfinite samples break the line. For large finite series with nondecreasing X, pass the indices from `CartesianTransforms.Decimate` to both drawing and hit testing.

### Bar

- Use `CartesianChart.DrawBars` and `HitBars` with the same bar width in pixels and baseline in data units. The default baseline is zero.

### Scatter

- Use `CartesianChart.DrawPoints` and `HitPoints` with the same marker radius in pixels.

### Bubble

- Use `CartesianChart.DrawPoints` and `HitPoints` with a radius in pixels for each source point, in the same order.

### Histogram

- Use `CartesianTransforms.Histogram` with a value range and a bin buffer. The buffer length sets the number of equal-width bins.
- Use each bin center as X and its count as Y. Draw and hit-test the resulting series with `CartesianChart.DrawBars` and `HitBars`.

## Pie

- Use `PieChart.CreateSegments` with positive weights, a start angle, and an optional gap. Angles are clockwise radians from the positive X axis.
- Size the segment buffer for the returned count and pass the populated span to `PieChart.Draw` and `HitTest`. Use `PieSelection` to highlight or offset a slice, and `ChartLegend.Draw` when labels need more space.

## Donut

- Use the pie APIs with the same fractional `holeRadius` between zero and one for drawing and hit testing.

## Radar

- Use `RadarProjection.CreateAxes` for the axes and `Project` to map each series with one range per dimension. Missing values leave gaps.
- Use `RadarChart.DrawGrid` and `DrawSeries` to display the chart. Use `RadarChart.HitSeries` or `RadarProjection.HitTestAxis` to identify a dimension.

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
- Use `ChartAnimation.Interpolate` to update a value buffer between frames. Keep values in source order.
