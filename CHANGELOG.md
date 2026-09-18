# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased][]

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
[Unreleased]: https://github.com/MASS4ORG/Guinevere/compare/v1.2.0...main
