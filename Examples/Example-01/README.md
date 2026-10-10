# Example-01 (Merged Showcase)

A consolidated example showcasing multiple Guinevere features in a single tabbed interface. This replaces the previous separate small examples for simplicity.

## Features

### 1. Basic Layout
- Absolute positioning of shapes (`gui.DrawRect`, `gui.DrawCircle`)
- Container nodes with `Axis.Horizontal` and `Axis.Vertical` directions
- Margins and padding
- Content alignment (`AlignContent`)
- Conditional rendering based on keyboard input
- Hover and click interactions
- Z-Index control

### 2. Children Layout
Advanced child alignment and distribution within nodes using `AlignContent` with separate horizontal and vertical controls:
- `AlignContent(float alignHorizontal, float alignVertical)` for independent axis control
- Support for both `Axis.Horizontal` and `Axis.Vertical` layouts
- Nested layout structures with visualized alignment results

### 3. Text Rendering
Organised with tabs showing:
- **Basic Text**: Single line, multi-line, colored and sized text, special characters, Unicode, symbols and font fallback
- **Wrapping**: Unwrapped vs. wrapped text at different `wrapWidth`s
- **Color Demo**: Rainbow and colored text variations
- **Effects**: `TextEffects` (flat, outline, drop shadow, inner shadow, linear/radial gradients, combined)
- **Long Content**: Long text to demonstrate scrolling with `ScrollY`

### 4. Animation
- Composable layout blending with `UnitValue.Lerp`
- Time-based animated elements with trigonometric movement
- HSV color cycling
- Easing function comparisons (Linear, EaseIn, EaseOut, SmoothStep, BackOut, ElasticOut)

### 5. Drag and Drop
- Typed and tag-identified payloads
- Non-consuming target previews with accepted/rejected indicators
- Settled drop results with state tracking
- Keyboard accessible (Space/Enter to drag/drop, Escape to cancel)
- Custom drag ghosts

### 6. Responsive Layout
Adapts to different screen sizes:
- **Mobile** (< 600px): Single column with bottom navigation
- **Tablet** (600-900px): Two-column layout
- **Desktop** (> 900px): Three-column layout with header navigation and sidebars

### 7. Styling (PSS)
- PanGui-compatible stylesheet syntax with constants and variables
- Nested and direct-child selectors
- `#inherit` inheritance
- Built-in `:hover` and custom `:selected` modifiers
- Immediate-mode variables
- Theme tokens and atomic provider reloads
- Light/dark theme switching

### 8. Events and Cursors
- DOM-style event bubbling (ancestor handles child clicks via `e.TargetId`)
- App-wide shortcuts (Escape on root)
- Pointer modes: `Wrapped` for scrubbing at window edges, `Relative` for orbit controls
- Cursor requests (`PointerCursor.Hand`, `ResizeHorizontal`, `Crosshair`, `Text`)
- Drag events with `e.TotalDelta` and `e.Delta`
- Pointer lock for relative mouse movement

### 9. Headless Controls
- Custom-styled controls using `gui.Pressable()` and `gui.Toggleable()`
- Headless control state tracking with `ControlBehaviorOptions`
- Custom visual rendering in render pass
- Game-style UI without Excalibur control styles

## Controls
- **Shift (Left)**: Toggle conditional rendering in basic layout
- **Mouse**: Hover/click interactions throughout
- **Escape**: Clear event log / cancel drag operations
- **Space/Enter**: Drag and drop (when focused)
- **Arrow keys / Tab**: Navigate controls

## Running
```bash
dotnet run --project Examples/Example-01-OpenGL-SilkNet/
```

This example uses Silk.NET OpenGL integration and runs as an executable.
