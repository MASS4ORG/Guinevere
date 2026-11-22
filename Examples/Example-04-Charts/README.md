# Example 04: Charts

Interactive Excalibur showcase for Guinevere Charts. Run from the repository root with `dotnet run --project Examples/Example-04-Charts/`. The desktop host uses OpenGL.SilkNET. Add `-- --headless` to render every view and its chart variants on a software surface.

- The line tab decimates a 100,000-sample series to the visible range. Switch the reference between straight and smooth, move the plot scales, or animate the reference values. Middle-drag to pan and use the zoom buttons to change the visible range.
- The bar tab switches among grouped, stacked, and grouped-stacked series with positive and negative values. Every layout can be drawn horizontally or vertically. The remaining Cartesian tabs show scatter, bubbles, and a histogram.
- The part-to-whole tab shows pie, donut, arc, and two-ring modes. Click a slice to highlight and separate it. Change values to animate the chart or toggle the empty state. The radar tab compares two series on independently scaled axes and animates the observed values.
- The timeline tab shows labeled intervals across lanes. The flame tab culls a nested trace to the visible rows and time range. Search by scope name, select a parent, or scroll through the lanes with the lane buttons.

The [stylesheet](Styles/charts.pss) themes the Excalibur toolbar and the chart's axis, grid, text, selection, and tooltip parts.
