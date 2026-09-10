# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased][]

- Added: docking system (`Guinevere/Docking/`) — `DockLayout` (tab groups, nested splits, floating
  windows, versioned JSON) rendered by `gui.DockSpace(...)`, with drag-to-dock, tab reorder,
  tear-off and close. Sample: `Sample-60-Docking`
- Added: drag-and-drop primitives — `gui.DragSource`, `gui.DropTarget`, `gui.DragGhost`,
  `gui.CurrentDragPayload`, `gui.CancelDrag`, and `InteractableElement.OnDrag(out DragArgs)` which
  reports the real press origin
- Added: `gui.Splitter(ref fraction, axis)` — a draggable divider between two flow siblings
- Added: out-of-flow positioning — `LayoutNode.Absolute(x, y)` (parent content box) and
  `AbsoluteScreen(x, y)` (screen space)
- Added: `LayoutNode.BlockInput()` — an overlay swallows hover for everything drawn beneath it,
  resolved by z-index
- Fixed: `Popup`, `ModalPopup`, `Tooltip`, `ContextMenu`, `Flyout` and `Dropdown` drew at their flow
  position instead of where they asked to be — `Left()`/`Top()` wrote the node rect and
  `CalculateLayout` then overwrote it
- Fixed: an explicitly sized child is no longer widened to the 10px minimum meant for unsized nodes
- Changed: `Left()`/`Top()` now position a node out of its parent's flow, relative to the parent's
  content box

## v[1.6.2][] 2026-09-09

## v[1.6.1][] 2026-09-09

## v[1.6.0][] 2026-09-09

## v[1.5.1][] 2026-05-04

## v[1.5.0][] 2026-05-03

- Changed: update to Dotnet 10
- Changed: update dependencies up to 2026-05-02

## v[1.4.3][] 2025-09-28

## v[1.4.2][] 2025-07-30


- Changed: slight enhancements in the README of all libraries

## v[1.4.1][] 2025-07-28

## v[1.4.0][] 2025-07-27

## v[1.3.0][] 2025-07-27

- Added: Control focus
- Changed: release on code change

## v[1.2.0][] 2025-07-18

- Added: Changelog updater
- Changed: code organization

## v[1.1.0][] 2025-06-26

- Initial release

## v[1.0.0][] 2025-06-25

- First Commit

[1.6.2]: https://github.com/brmassa/guinevere/compare/v1.6.1...v1.6.2
[1.6.1]: https://github.com/brmassa/guinevere/compare/v1.6.0...v1.6.1
[1.6.0]: https://github.com/brmassa/guinevere/compare/v1.5.1...v1.6.0
[1.5.1]: https://github.com/brmassa/guinevere/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/brmassa/guinevere/compare/v1.4.3...v1.5.0
[1.4.3]: https://github.com/brmassa/guinevere/compare/v1.4.2...v1.4.3
[1.4.2]: https://github.com/brmassa/guinevere/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/brmassa/guinevere/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/brmassa/guinevere/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/brmassa/guinevere/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/MASS4ORG/Guinevere/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/MASS4ORG/Guinevere/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/MASS4ORG/Guinevere/compare/main...1.0.0
[Unreleased]: https://github.com/MASS4ORG/Guinevere/compare/v1.2.0...main
