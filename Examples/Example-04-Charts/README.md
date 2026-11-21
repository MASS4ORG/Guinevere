# Example 04: Charts

Interactive Excalibur showcase for Guinevere Charts. Run from the repository root with `dotnet run --project Examples/Example-04-Charts/`. The desktop host uses OpenGL.SilkNET. Add `-- --headless` to render every view on a software surface without opening a window.

- The Cartesian tabs show a 100,000-sample decimated line with a reference series, bars, scatter, bubbles, and a histogram. Middle-drag to pan and use the zoom buttons to change the visible range.
- The part-to-whole tab toggles pie and donut modes. Click a slice to highlight and separate it. Change values to animate the chart. The radar tab compares two series on independently scaled axes and animates the observed values.
- The timeline tab shows labeled intervals across lanes. The flame tab culls a nested trace to the visible rows and time range. Search by scope name, select a parent, or scroll through the lanes with the lane buttons.

The [stylesheet](Styles/charts.pss) themes the Excalibur toolbar and the chart's axis, grid, text, selection, and tooltip parts.
