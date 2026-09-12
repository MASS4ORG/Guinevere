# Controls-01 — Buttons, Inputs, Tabs & Scrolling

Merges `Sample-50-Controls`, `Sample-51-Buttons`, `Sample-52-TextInput-MultiPlatform`, `Sample-07-Scroll`
and `Sample-11-Styling` into one sample organised around eight tabs.

## Features

- **Buttons**: every button style — filled, outline, text, rounded, icon, custom-size, with
  hover/press visual feedback.
- **Selection**: checkbox, toggle and dropdown.
- **Text Inputs**: text fields, password fields, text areas and clicking-to-focus; shows both the
  ref-based and returning APIs for each.
- **Navigation**: a breadcrumb trail (hoverable links that navigate back up the path, current
  page locked), tab bar, pill tabs, vertical tabs, and a **tree view** — a virtualised
  file-explorer tree with folder/file icons, mouse and arrow-key navigation, and click reporting
  for every button.
- **Feedback**: determinate progress bars (animated sweeps, hue-shifted fills) and indeterminate
  bars, plus custom track/fill colours and heights.
- **Scrolling**: turning a node into a scroll container, programmatic scroll
  (`ScrollToTop` / `ScrollToBottom` / scroll-percentage sliders) and inner scroll regions.
- **Focus**: Tab / Shift+Tab navigation and cascaded focus indicators.
- **Styling**: two named-node styles applied through a `StyleSheet` added to the `Gui`
  (`gui.StyledNode` + `gui.StyleSheets.Add`).

Use the tab bar at the top to switch between the demonstrations.

## Controls

- **Click** a tab to switch demo.
- **Type** in any input once it has keyboard focus; **click** an input to focus it.
- **Tree view**: click a row to select it, click the arrow (or double-click the row) to fold,
  use **arrow keys** to move the selection and **Enter** to activate.
- **Breadcrumb**: click an intermediate crumb to navigate back; the current page is not a link
  (it also answers **Tab**/Space when focused).
- **Click** the scroll arrows (or scroll the wheel) over the list boxes.
- The styling tab shows the same widget twice — once with each style.