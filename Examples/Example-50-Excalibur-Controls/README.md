# Controls-01 — Excalibur control gallery on the docking system

Merges `Example-50-Controls`, `Example-51-Buttons`, `Example-52-TextInput-MultiPlatform`, `Example-07-Scroll`
and `Example-11-Styling`, plus the `Example-51-Docking` layout demo, into one docking workspace: each
widget group is a dock panel you can split, tab, reorder, tear off or close.

## Features

### Docking

Every group below is a panel of the dock space. Drag a tab onto another group's edge to split, onto
its center to join its tabs, along a tab strip to reorder, or onto empty space to tear it into a
floating window. Drag a splitter to resize, and use the **✕** on a tab to close a panel. The
**overflow button** on a tab strip opens that panel's actions (close).

### Panels

- **Buttons**: every button style — filled, outline, text, rounded, icon, custom-size, with
  hover/press visual feedback.
- **Selection**: checkbox, toggle, dropdown, searchable multi-selection with removable chips, automatic enum flag checkboxes, enum buttons and previous/next enum paging, and two embedded file browsers.
- **Text Inputs**: text fields, password fields, text areas, a right-aligned numeric field and
  clicking-to-focus; shows both the ref-based and returning APIs for each.
- **Navigation**: a breadcrumb trail, a closable tab bar (middle-click a tab or use its "×"), pill
  tabs, vertical tabs, a **menu bar** (dropdown menus with submenus, separators, disabled items,
  shortcut labels and checkable items), and a virtualised **tree view** with a selection-following
  breadcrumb.
- **Focus**: Tab / Shift+Tab navigation and cascaded focus indicators.
- **Feedback**: determinate and indeterminate progress bars, toasts (corner-pinned, re-triggered
  rather than stacked), and delayed tooltips.
- **Scrolling**: turning a node into a scroll container, programmatic scroll
  (`ScrollToTop` / `ScrollToBottom` / scroll-percentage) and nested scroll regions.
- **Styling**: PSS-styled widgets via `gui.StyledNode`, plus a source view whose hovered element
  highlights the rules it uses.
- **Colors**: color fields, an inline picker with swappable shapes, a picker built from parts, and
  the current system appearance.

### Layout toolbar

**Save layout**, **Load layout** and **Reset** persist or restore the dock arrangement; **Reopen …**
brings a closed panel back.

## Controls

- **Click** a tab to switch a panel; **drag** it to rearrange the dock space.
- **Type** in any input once it has keyboard focus; **click** an input to focus it.
- **Multi-selection**: search, check several rows without closing, use Select all/Clear on the filtered results, or remove a chip.
- **Tree view**: click a row to select it, click the arrow (or double-click the row) to fold,
  use **arrow keys** to move the selection and **Enter** to activate.
- **Menu bar**: click a title to drop its menu; **hover** another title to switch menus; **Escape**
  or clicking outside dismisses.
- **File browsers**: navigate with the toolbar, filter or search the listing, then choose a file or folder.
