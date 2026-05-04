# Sample-54-FocusManagement

This sample demonstrates Guinevere's robust focus management system, including keyboard navigation, cascaded focus, and focus-aware styling.

## Features
- **Tab Navigation**: Move forward with `Tab` and backward with `Shift+Tab`.
- **Focus Indicators**: Controls visually change when they receive focus.
- **Cascaded Focus**: Parental nodes can detect if any of their children have focus (`HasFocusWithin`).
- **Keyboard Interaction**: Use the `Space` bar to interact with focused buttons, toggles, and checkboxes.
- **Focus Status Panel**: Real-time display of the currently focused element ID and focus changes.
- **Interactive Form**: A complex user information form with various focusable controls.

## Interaction
- Click any control to focus it.
- Use `Tab` / `Shift+Tab` to cycle through all focusable elements.
- Watch the panel titles; they turn blue and display "(FOCUSED)" when the section contains the focused element.
