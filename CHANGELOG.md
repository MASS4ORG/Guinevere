# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased][]

- Added: Composable min/max layout expressions and Font Awesome widget icons alongside Noto Emoji fallback.
- Changed: Wrapped text layout now lives in core and is shared with ordinary text drawing.

## v[4.1.0][] 2026-09-23

- Fixed: Text area caret was never drawn (its node was built only in the render pass)
- Fixed: Scrollbar thumbs show the default cursor instead of their container's
- Added: DOM-style event propagation, node cursor shapes, and hidden/relative/wrapped pointer modes
- Changed: `gui.Input` filters edges handled with `PreventDefault`; use `gui.PlatformInput` for the raw handler
- Removed: `PointerCursor.Hidden`; request `PointerMode.Hidden` instead
- Added: Capability-based platform services and integration conformance checks
- Added: Headless press, toggle, selection, drag, and repeat behaviors with optional control semantics
- Fixed: Tab and dropdown state changes now happen between frames, preventing new trees from blinking before layout
- Added: File browser widget
- Changed: Major rework on some internal type representations, specially Color
- Changed: Enhanced the Drag-and-drop API
- Added: Better styling foundations and showcase
- Changed: Standardized Excalibur control palettes and sizes
- Changed: Excalibur defaults now use independently inheritable style values
- Removed: Obsolete untyped drop targets and single-term unit accessors

## v[4.0.0][] 2026-09-20

### Added

- **Composable layouts:** Mix and interpolate pixels, percentages, ratios, expand, fit-content, and the new `FitLargest` sizing via `UnitValue`.
- **Layout caching:** Added explicit dirty tracking and invalidation for retained layout trees.
- **Docs & Benchmarks:** Added layout performance/migration docs and `Example-90-LayoutBenchmarks`.

### Changed

- **Performance overhaul:** Intrinsic layout measurement is now a bottom-up, linear pass. It allocates zero memory in steady state.
- **Optimizations:** Replaced LINQ with direct loops/reusable buffers, added single-child fast paths, and streamlined scroll offset propagation.

### Breaking Changes

- **Value types:** `Rect` is now a `record struct`. `UnitValue` and `LayoutStyle` have new binary layouts.
- **API removals:** Dropped the `UnitValue(UnitType, float)` constructor and its implicit numeric conversions.
- **Manual invalidation:** Directly writing to a retained `LayoutNode.Style` now requires an explicit `InvalidateLayout()` call (fluent APIs handle this automatically).

#### Upgrade Guide

- Replace null `Rect` references with `Rect?`.
- Build `UnitValue`s using factory methods (e.g., `UnitValue.Pixels`, `UnitValue.FitLargest`) and read final numeric results directly from the calculated node rectangle.
- Call `node.InvalidateLayout()` after mutating style fields directly.
- Recompile any projects referencing `Rect`, `UnitValue`, or `LayoutStyle` to account for structural changes.

## v[3.1.0][] 2026-09-19

- Added: Dialogs
- Fixed: HandleScrollInput allowing scrolling behind
- Fixed: ApplyAncestorClips' content overflow

## v[3.0.0][] 2026-09-18

- Breaking CHANGE: integrations namespaces now use only `Guinevere`
- Fix: avoid embedding duplicate fonts (icons.ttf is a duplicate of NotoColorEmoji-Regular.ttf)
- Change: removal of `DrawTick`, `DrawSubmenuArrow` and `DrawTriangleFilled` in favor of icons/emojis

## v[2.1.0][] 2026-09-17

- Added: Cascaded Menu
- Fix: z-Index for context menus/flyouts/tooltips
- Fix: Treeviewer starting collapsed

## v[2.0.1][] 2026-09-14

- Changed: README

## v[2.0.0][] 2026-09-14

- BREAKING: changing the package name to `MASS4.Guinevere`

## v[1.8.0][] 2026-09-13

- Added: Excalibur controls library
- Changed: Major reorganization of Sample projects

## v[1.7.0][] 2026-09-11

- Added: docking system (`Guinevere/Docking/`, sample `Sample-60-Docking`) — `DockLayout` with tab
  groups, nested splits and floating windows, rendered by `gui.DockSpace(...)`, with drag-to-dock,
  tab reorder, tear-off and close
- Added: draggable borders — `gui.Splitter(ref fraction, axis)` resizes the two flow siblings it
  separates
- Added: out-of-flow positioning (`LayoutNode.Absolute` / `AbsoluteScreen`) and `BlockInput()` for
  overlays
- Added: input simulator

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

[4.1.0]: https://github.com/brmassa/guinevere/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/brmassa/guinevere/compare/v3.1.0...v4.0.0
[3.1.0]: https://github.com/brmassa/guinevere/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/brmassa/guinevere/compare/v2.1.0...v3.0.0
[2.1.0]: https://github.com/brmassa/guinevere/compare/v2.0.1...v2.1.0
[2.0.1]: https://github.com/brmassa/guinevere/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/brmassa/guinevere/compare/v1.8.0...v2.0.0
[1.8.0]: https://github.com/brmassa/guinevere/compare/v1.7.0...v1.8.0
[1.7.0]: https://github.com/brmassa/guinevere/compare/v1.6.2...v1.7.0
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
[Unreleased]: https://github.com/MASS4ORG/Guinevere/compare/v3.1.0...main
