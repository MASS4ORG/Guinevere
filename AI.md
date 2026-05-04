# AI.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Guinevere** is a GPU-accelerated immediate-mode GUI (imgui) library built on SkiaSharp, targeting .NET 10.0. It uses a two-pass rendering model (layout → render) with a fluent/builder API.

## Build Commands

Uses **NUKE** build automation (`./build.sh` on Linux/macOS, `build.cmd` on Windows):

```bash
dotnet build               # Quick recompilation
./build.sh restore compile # Complete clean+restore+compile
dotnet test                # Default for tests
./build.sh TestReport      # Run tests + generate HTML coverage report
./build.sh CI              # Full pipeline: Clean → Restore → Compile → Test
./build.sh PackNuGet       # Create NuGet packages
```

Standard dotnet commands also work for individual projects:
```bash
dotnet build Guinevere/Guinevere.csproj
dotnet run --project Samples/Sample-01-BasicWindow/
```

## Architecture

### Core Design
- **Immediate Mode**: UI is redefined every frame via `BeginFrame()` / `EndFrame()` — no retained widget state
- **Two-Pass Layout**: Pass 1 measures and calculates sizes; Pass 2 renders
- **Fluent Builder API**: Chainable calls like `gui.Node().Width(100).Height(50).Enter()`
- **`IDisposable` Scope Pattern**: `using var _ = gui.Node().Enter()` — exiting scope ends the node

### Key Classes
- `Gui` — Main entry point, a partial class split across 19 files in `Guinevere/`
- `LayoutNode` — Core layout element with parent-child relationships (`Guinevere/Layout/LayoutNode.cs`)
- `LayoutNodeScope` — Per-node styling and state (attached to each `LayoutNode`)
- `GuiWindow` — Integration bridge between windowing libraries and core GUI (lives in each integration)
- `DrawList` — Collects rendering commands per frame with Z-ordering
- `Shape` — Complex geometric operations (`Guinevere/Shape.cs`)

### Project Structure
```
Guinevere/           # Core library (main public API)
Guinevere.Tests/     # xUnit 3 tests
Integrations/
  Guinevere.OpenGL.SilkNET/    # Recommended integration
  Guinevere.OpenGL.OpenTK/
  Guinevere.OpenGL.Raylib/
  Guinevere.Vulkan.SilkNET/
  Shared/                      # Common integration code
Samples/             # 20+ example applications demonstrating features
.nuke/               # NUKE build targets (partial classes)
```

### Graphics Abstraction
The `ICanvasRenderer` interface decouples core GUI from the graphics backend. Integrations implement this interface and provide a `GuiWindow` subclass. The recommended integration is `Guinevere.OpenGL.SilkNET`.

## Testing

- **Framework**: xUnit v3 with NSubstitute for mocking
- **Coverage**: Coverlet (80% threshold), outputs to `coverage/`
- Test files map directly to source files: `LayoutNode.cs` → `LayoutNodeCalculationTests.cs`, etc.

## Code Conventions

Enforced via `.editorconfig`:
- File-scoped namespaces
- Expression-bodied members for properties/accessors (enforced as error)
- `var` when type is apparent
- Max line length: 120 characters
- Nullable reference types enabled everywhere
- Prefer functional/fluent style, `Span<T>`, `record` types, and `async`/`await`. Use latest .NET features for performance.
- Do **not** introduce new compilation warnings. Do not fix pre-existing warnings unless asked.

## Versioning & Releases

- **Semantic versioning** via GitVersion (`GitVersion.yml`, ContinuousDelivery mode)
- Conventional commit prefixes drive version bumps: `feat:` → minor, `fix:` → patch, `breaking:` → major
- Daily automated release via GitHub Actions cron (18:00 UTC)
- NuGet package ID: `org.mass4.Guinevere`
- After significant changes, suggest a commit message in **Conventional Commits** style. Do NOT commit unless asked. NEVER push to remote.
- In case the scope of the task is too big, suggest the creating of multiple commits to document progress.
- When creating new branches, point the remote as `origin` repositoty.

### Keep AI.md Current

After any change that affects architecture, project structure, conventions, dev commands, or workflows, review this file and update it if outdated. Treat it as living documentation.

## Samples

The `Samples/` directory contains 20+ numbered examples demonstrating specific features. When adding new functionality, follow existing sample patterns. Key samples for architecture understanding:
- `Sample-54-FocusManagement/` — Keyboard navigation with Tab/Shift+Tab
- `Sample-72-PanGui-AirbnbSlider/` — Advanced shape composition
- `Sample-75-PaperUI-Dashboard/` — Complex dashboard layout

## GitHub MCP Integration

This repository uses GitHub MCP (Model Context Protocol) for streamlined issue and project management. The official repository is `MASS4ORG/Guinevere` at `https://github.com/MASS4ORG/Guinevere`.

### Available Operations
- **Issues**: Create, update, list, search, get, add comments
- **Pull Requests**: Create, update, list, merge, review, get diffs/files
- **Labels**: Create, update, list, delete (see label set in repo)
- **Projects**: Manage issues within GitHub Projects
- **Releases**: Create, list, get by tag

### Label Groups
The repository uses structured labels organized in groups (colors defined on GitHub):

- **Component**: `layout`, `controls`, `core`, `samples`, `integration`, `build`, `docs`, `vulkan`, `opengl`, `skia`
- **Priority**: `priority:low`, `priority:medium`, `priority:high`, `priority:critical`
- **Size**: `size:XS`, `size:S`, `size:M`, `size:L`, `size:XL`
- **Type**: `type:bug`, `type:feature`, `type:docs`, `type:refactor`, `type:task`, `type:enhancement`, `type:performance`, `type:breaking`
- **Status**: `status:blocked`, `status:help-wanted`, `status:wontfix`, `status:duplicate`, `status:invalid`

**Note**: Issue #12 and #17 are duplicates (both "Enhance DragAndDrop API"). Please close one. Issue #16 is CSS-like styling, #24 is the MCP server.

## Issue Template

When creating issues, follow this template to maintain consistency:

```markdown
### Current State
<description of the current state of the feature/bug>

### Tasks:
- [ ] <task 1>
- [ ] <task 2>
- [ ] <task 3>

### Acceptance Criteria:
- <criterion 1>
- <criterion 2>
- <criterion 3>

### Related:
- #<number> (<description>)
- #<number> (<description>)
```
