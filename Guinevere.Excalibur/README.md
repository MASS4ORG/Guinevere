# Guinevere.Excalibur

A ready-to-use set of default controls for the [Guinevere](https://github.com/MASS4ORG/Guinevere)
immediate-mode GUI library. Everything here is an extension method on `Gui` living in the `Guinevere`
namespace, so it drops into an existing Guinevere app with a single package reference and no setup.

![Guinevere Excalibur controls](../docs/excalibur-controls-overview.png)

## Install

```
dotnet add package MASS4.Guinevere.Excalibur
```

## Quick Start

```csharp
using Guinevere;

public partial class App
{
    private bool _isEnabled = true;
    private string _name = "";
    private float _volume = 0.5f;
    private int _activeTab;

    public void Draw(Gui gui)
    {
        if (gui.Button("Run")) { /* clicked */ }
        gui.Checkbox(ref _isEnabled, "Enable feature");
        gui.TextInput(ref _name, "Your name");
        gui.Slider(ref _volume, 0f, 1f);

        gui.Tabs(ref _activeTab, tabs =>
        {
            tabs.Tab("Home", () => gui.DrawText("Home content", 12, Color.Gray));
            tabs.Tab("Settings", () => gui.DrawText("Settings content", 12, Color.Gray));
        });
    }
}
```

## Controls

| Group | APIs | Purpose |
|-------|------|---------|
| Actions | `Button`, `IconButton`, `ImageButton` | Pointer and keyboard-activated buttons with focus feedback. |
| Selection | `Checkbox`, `Toggle`, `RadioButton`, `RadioGroup`, `Dropdown`, `MultiDropdown`, `EnumDropdown` | Boolean, exclusive, list and flag selection, with searchable multi-selection and enum presentations. |
| Text and values | `TextInput`, `PasswordInput`, `TextArea`, `NumberField`, `Slider`, `ObjectField` | Text editing, scrub editing, numeric ranges, and compact object fields. |
| Display | `Image`, `ProgressBar`, `WrappedText`, `Toast`, `Toasts`, `ClearToasts` | Images, progress, wrapping, and transient notifications. |
| Navigation | `Tabs`, `TabBar`, `TabStrip`, `PillTabs`, `VerticalTabs`, `Breadcrumb`, `TreeView`, `FileBrowser` | Tabs, trails, virtualised trees, and an embeddable filesystem picker. Tabs are closable only with `closable: true`. |
| Menus and overlays | `AppBar`, `MenuBar`, `Flyout`, `CascadeMenu`, `ContextMenu`, `Popup`, `ModalPopup`, `Dialog`, `Tooltip` | Application chrome, command menus, popovers, modal windows, and delayed help. |
| Layout tools | `Splitter`, `DockSpace`, `DockLayout` | Resizable panes and persistent split/tab/float docking. |

## Styling

Controls take no colors or radii; their look comes from `.pss` stylesheets. Excalibur ships a default sheet (`ExcaliburStyles.DefaultSheet`) that is kept under every application sheet, so a theme overrides any token or rule in it. Restyle with classes, sheets, or tokens for a subtree:

```csharp
gui.StyleSheets.Add(StyleSheet.Parse("""
    $accent = #88c0d0;
    button.danger { background-color = #dc3545; color = #ffffff; :hover { background-color = #c82131; } }
    """));

if (gui.Button("Delete", classes: ["danger"])) Delete();

using (gui.Node().Enter())
{
    gui.SetStyleToken("surface", Color.FromArgb(255, 40, 44, 52)); // this subtree only
    gui.Button("Dark");
}
```

`ExcaliburStyles.DefaultSheetText` is a starting point for a full theme.

### Themes

A color theme is a token-only sheet layered right above the default sheet. Excalibur ships `ExcaliburStyles.Dark` (the default sheet itself), `Light`, `MonoLight` and `MonoDark`; any sheet of `$token = value;` declarations works the same way. Application sheets added with `gui.StyleSheets.Add` still override the theme.

```csharp
ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light); // replaces the previous theme; Dark or null restores the default
var ink = ExcaliburStyles.TokenColor(gui, "text");    // theme colors for an application's own drawing
gui.DrawDropIndicator(drop.State, style: ExcaliburStyles.DroppableArea(gui));
```

`ControlPalette` and `gui.ControlPalette` are gone, and so are the color tokens of `gui.ControlStyle` (dimensions stay). To migrate:

| Before | After |
|---|---|
| the default light palette | `ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light)`; the default is now dark |
| `new Gui { ControlPalette = ControlPalette.Dark }` | nothing: dark is the default |
| a custom `new ControlPalette { Surface = … }` | a sheet of `$surface = …;` tokens passed to `SetTheme`, or `gui.StyleSheets.SetToken("surface", …)` |
| `gui.ControlPalette.Text`, `gui.ControlStyle.Text` | `ExcaliburStyles.TokenColor(gui, "text")` |
| `ControlStyles.Value<ControlAccent, Color>(c)` on a subtree | `gui.SetStyleToken("accent", c)` |
| `gui.ApplyControlPalette()` with a `control-palette { … }` rule | top-level `$token = value;` declarations in the sheet |

Token names are the palette's property names in kebab case (`SurfaceHover` → `surface-hover`).

### Contracts

| Control | Element | Classes | States and modifiers | Properties read |
|---|---|---|---|---|
| `Button` | `button` | caller classes; `primary` in the default sheet | `:hover`, `:active`, `:focus`, `:disabled` | `background-color`/`background`, `border-*`, `border-radius`, `padding` (fit size), `color`, `font-*`, `outline`, `box-shadow`, `opacity`, `cursor` |
| `IconButton` | `button` | `icon` + caller classes | as `Button`, plus `:checked` (pass `checked` as a class) | as `Button` |
| `ImageButton` | `button` | `image` + caller classes | as `Button`; the images swap by state | as `Button`; the image is drawn under the border and outline |
| `Checkbox` | `checkbox`, drawn child `indicator` | caller classes and `id` | `:hover`, `:active`, `:focus`, `:disabled`, `:checked`, `:mixed` | row `color`/`cursor`; indicator background, border, radius and color; focus `outline`; root box/text properties |
| `RadioButton` | `radio`, drawn children `indicator`, `dot` | caller classes and `id` | `:hover`, `:active`, `:focus`, `:disabled`, `:checked` | as `Checkbox`; `dot` background/border/radius |
| `Toggle` | `toggle`, drawn children `track`, `thumb` | caller classes and `id` | `:hover`, `:focus`, `:disabled`, `:checked` | as `Checkbox`; track background/radius; thumb background/border/radius |
| `TextInput`/`PasswordInput`/`TextArea`/`NumberField` | `input` | `password`, `area`, `number` respectively, plus caller classes and `id` | `:hover`, `:active`, `:focus`, `:disabled` | root box/text/font properties and `cursor`; child `placeholder` color (`:disabled`), child `selection` background |
| `Dropdown` | `dropdown`, popup children `listbox`, `option` | caller classes and `id` | button `:open`, `:disabled`; option `:hover`, `:highlighted`, `:selected` | root box/text properties and `cursor`; listbox background/border/radius; option background/radius and `cursor` |
| `MultiDropdown` | `dropdown`, children `chip`, `listbox`, `option`, `indicator` | caller classes and `id` | button `:open`, `:disabled`; chip `:hover`; option `:selected`, `:highlighted`; indicator `:checked`, `:mixed` | as `Dropdown`, plus chip background/radius/color and indicator border/radius |
| `EnumDropdown` | `dropdown`; `button.segment` with `EnumPresentation.ToggleButtons` | `segment` (toggle buttons) plus caller classes | segment `:checked` | as `Dropdown`/`Button` |
| `Slider` | `slider`, drawn children `track`, `fill`, `thumb` | caller classes and `id` on `slider` | `:hover`, `:active`, `:focus`, `:disabled`; `thumb:focus` | root box/text properties, `gap`, `flex-direction`, `align-items`; track `height`; thumb `width`, `height`, `max-width`, `max-height`; each part's background, border, radius, outline, shadow and opacity |
| `ProgressBar` | `progress`, drawn child `fill` | caller classes and `id` on `progress` | `:indeterminate` when the fraction is null | root `width`, `height`, `padding` and box properties; fill background, border, radius, outline, shadow and opacity |
| `Splitter` | `splitter` | caller classes and `id` | `:horizontal`, `:vertical`, `:hover`, `:active` while captured | axis size, background, border, radius, outline, shadow, opacity and cursor |
| `Tabs`/`PillTabs`/`VerticalTabs` | `tabs`, `tabbar`, `tab`, `tabpanel`, `tab-close`, `tab-nav` | `plain`, `pill`, `vertical` plus caller classes | tab `:hover`, `:selected`, `:focus`, `:disabled`; `tab-nav` `:hover`, `:disabled`; `tab-close` `:hover` | tabbar background/border/radius; tab color/background/radius/outline; tabpanel background/border/radius; close radius/color; nav color |
| `TabStrip` | `tabstrip`, `tab`, `marker`, `tab-close` | caller classes and `id` | tab `:hover`, `:selected`; `tab-close` `:hover` | tabstrip background; tab background/color; marker background; close radius |
| `DockSpace`/`DockLayout` | `dockspace`, `dock-panel`, `dock-window`, `dock-grip`, `dock-empty`, `dock-ghost`, `drop-indicator`, `drop-preview` | `dock` on floating windows | — | background, border, radius, color and cursor per element |
| `TreeView` | `treeview`, `tree-row`, `tree-expander`, `tree-ghost` | caller classes and `id` | row `:hover`, `:selected`; expander `:hover` | treeview color; row background/radius; expander color; ghost background/radius/color |
| scrollbar | `scrollbar`, child `thumb` (drawn by the styling package) | — | thumb `:hover`, `:active` | scrollbar background; thumb background/radius |
| `Popup` | `popup`, `popup-content`, `popup-title`, `overlay` | caller classes and `id` | `:closed` while hidden | popup background/border/radius; title background/color; overlay background |
| `Dialog` | `dialog`, `dialog-title`, `dialog-content`, `dialog-footer`, `dialog-close` | caller classes and `id` | `:closed`; close `:hover` | dialog background/border/radius; title/color; footer border; close radius/color |
| `Tooltip` | `tooltip` | caller classes and `id` | `:closed` | background/border/radius/color |
| `Toast` | `toast`, drawn child `accent` | `info`, `positive`, `warning`, `negative` plus caller classes | — | toast background/border/radius/color; accent width/background |
| `MenuBar`/`Flyout`/`ContextMenu` | `menubar`, `menu`, `menu-title`, `menu-item`, `menu-shortcut`, `menu-separator`, `menu-expander`, `menu-check` | caller classes | `:hover`, `:focus`, `:open`, `:highlighted`, `:disabled` | background/border/radius/color; item background/radius/outline; shortcut and separator color |
| `AppBar` | `appbar`, `window-button`, `glyph-fill` | — | `window-button` `:hover` | appbar background/color; window-button background; glyph-fill background |
| `Breadcrumb` | `breadcrumb`, `crumb`, `underline`, `crumb-separator` | crumb `link`/`current` classes | `:link:hover`, `:link:active`; separator `:hover`, `:open` | crumb color and underline; separator color |
| `Label`/`WrappedText` | `label`, `wrapped-label` | caller classes and `id` | — | `color`, `font-*` |
| `ObjectField` | `object-field`, `drop-outline`, `object-pick`, `object-clear` | caller classes and `id` | `:hover`, `:empty`, `:accepting`, `:rejecting`; drop-outline `:accepted` | background/border/radius/color; pick and clear color |
| `FileDialog` | `file-browser`, `file-sidebar`, `file-list`, `file-header`, `file-column`, `file-row`, `file-place`, `file-label`, `file-status`, `file-detail` | `primary` on the accept button | row/place/column `:hover`, `:selected` | background, border, radius and color per element |
| Autoformers form | `form` | — | — | `color`, `dim-color`, `background-color`, `border-color`, `accent-color`, `divider-color`, `negative-color`, `base-background`, `border-radius` |

The embedded default sheet (`Styles/guinevere.default.pss`, exposed as `ExcaliburStyles.DefaultSheetText`) is the normative list of selectors and of the properties each one reads; the table above summarises it. Drawn parts support direct-child selectors such as `slider#volume > thumb` and inherit tokens from their control. Slider track height and thumb dimensions accept pixels or percentages of the track viewport's height. `Slider` value labels use the stylesheet font size unless `fontSize` is supplied. `ProgressBar` defaults to the sheet's height and fills its parent; `Splitter` defaults to the sheet's axis size. Explicit dimensions override sheet dimensions.

```pss
slider.volume > track { height = 8; border-radius = 4; }
slider.volume > thumb { width = 18; height = 18; border-radius = 50%; }
slider.volume:focus > thumb { outline = 2px solid $accent; outline-offset = 3; }
progress.download > fill { background = linear-gradient(to right, #5081d9, #88c0d0); }
splitter:horizontal { width = 8; cursor = col-resize; }
```

When upgrading, move slider/progress colors and splitter colors into the matching rules, and pass `classes` or `id` to select them. `Slider.fontSize`, `ProgressBar.height` and `Splitter.thickness` are nullable so omitted values follow the sheet; recompile callers against the updated API. Applications with positional compiler call-site arguments should use named `filePath` and `lineNumber` arguments.

Default tokens: `$base-background`, `$surface`, `$surface-hover`, `$surface-active`, `$popup`, `$border`, `$border-active`, `$divider`, `$accent`, `$accent-hover`, `$accent-subtle`, `$text`, `$text-dim`, `$text-disabled`, `$text-on-accent`, `$selected`, `$positive`, `$negative`, `$warning`, `$info`, `$focus-ring`, `$shadow`, `$overlay`, `$text-selection`, `$radius`. Every Excalibur control now draws from sheets, including the Autoformers `form` contract.

## Buttons and selection

```csharp
if (gui.Button("Build")) Build();
gui.Checkbox(ref _enabled, "Enable feature");
gui.Toggle(ref _darkMode, "Dark mode");
gui.RadioGroup(ref _quality, [(0, "Low"), (1, "Medium"), (2, "High")]);
gui.Dropdown(["Draft", "Review", "Published"], ref _status);
```

Buttons and dropdowns receive mouse focus; use Tab to move through controls and Enter or Space to
activate buttons. Open popovers and dialogs constrain Tab to their visible controls.

## Searchable selection

Keep selection in your application and call the widget from the same site in both GUI passes:

```csharp
// Fields kept between frames:
private readonly string[] _tags = ["Gameplay", "UI", "Audio"];
private IReadOnlyList<string> _selectedTags = [];

// Inside Draw(Gui gui):
var result = gui.MultiDropdown(_tags, _selectedTags, chips: true,
    comparer: StringComparer.Ordinal, width: 240);
if (result.Changed) _selectedTags = result.Selected;
```

Search uses case-insensitive substring matching. Select all and Clear affect only filtered options; other selected values stay intact. Equality defaults to `EqualityComparer<T>.Default`, duplicate options show once, and returned values follow option order before unknown values in their original order. The summary shows up to two labels or a count; `chips: true` shows up to two removable chips and a remaining count. Rows are virtualized; `maxVisibleItems` defaults to six. Filtering still scans the options.

Use Up/Down to move through results and Enter to select; Space toggles while the list is focused and types a space in search. Home/End and PageUp/PageDown navigate the focused list. Tab stays within the popup, and Escape or an outside click closes it. Set `enabled: false` to disable interaction. Give repeated widgets distinct node scopes or `filePath` identities.

```csharp
gui.EnumDropdown(ref _permissions); // [Flags] automatically uses checkboxes.
gui.EnumDropdown(ref _mode, EnumPresentation.ToggleButtons);
gui.EnumDropdown(ref _mode, EnumPresentation.Paging);
```

Ordinary enums select one value. Flag edits add/remove only the chosen bits; selecting zero (None) clears the whole value. A None option is supplied when a flags enum has no declared zero. Paging wraps through distinct declared values in `Enum.GetValues` order, including sparse values; for flags it replaces the entire mask. Button groups occupy one horizontal row: use the dropdown for many or long labels. Pass `display` to provide custom labels.

`EnumDropdown(ref value)` returns true when an edit is delivered. `MultiDropdownResult<T>.Changed` and enum edits are delivered once in the build pass, so persistence/undo can run inside that condition. For custom multi-object drawers, apply `result.Changes` to each owner and use `mixed`/`isMixed` for feedback. The runtime `EnumDropdown(Enum, out changes, ...)` overload and `EnumSelection.Apply` support enum drawers. Autoformers.Excalibur handles this automatically; see the [Turian guide](../docs/turian-selection-guide.md).

## Text and numeric input

```csharp
_name = gui.TextInput(_name, placeholder: "Project name");
_notes = gui.TextArea(_notes, width: 420, height: 120);
gui.NumberField(ref _opacity, min: 0, max: 1, step: 0.05f);
gui.Slider(ref _opacity, min: 0, max: 1, step: 0.05f, showValue: true);
```

`NumberField` supports click-to-edit and drag scrubbing. `Slider` supports clicking, dragging beyond
its track, and arrow-key adjustments while focused.

## Overlays and dialogs

```csharp
gui.Popup(ref _showInspector, () => DrawInspector(gui), title: "Inspector");
gui.Dialog(ref _showDelete, "Delete project", () => gui.DrawText("This cannot be undone."),
    footer: () => { if (gui.Button("Delete")) DeleteProject(); });
gui.Tooltip(gui.CurrentNode, "More information");
```

`Popup` and `Dialog` preserve focus context while open and restore the opener after they close.
`Dialog` blocks the content behind it; `Popup` can opt into modal behavior through `ModalPopup`.

## File dialog

Keep a `FileDialogState`, open it with a request, and draw it every frame. Enumeration runs
asynchronously and listing rows are virtualised, so large or remote directories do not stall the
frame loop.

```csharp
var files = new FileDialogState();

files.Open(new FileDialogRequest
{
    Mode = FileDialogMode.OpenFile,
    Title = "Open scene",
    Filters = [FileDialogFilter.Of("Scenes", ".scene")],
    OnComplete = path => SelectedScene = path,
});

gui.FileDialog(files);
```

Use `gui.FileBrowser(files)` for the same picker embedded in a panel. Pass an
`IFileDialogFileSystem` to `FileDialogState` to browse engine assets, archives, remote storage, or a
test fixture. The built-in `PhysicalFileDialogFileSystem` supplies local quick-access folders and
drives.

## Menu Bar

Call `menus.Collapsible()` in the builder to show a compact menu toggle. Opening it reveals the titles;
choosing an action, clicking outside, or pressing Escape collapses it again. Menu bars, flyouts, cascade menus,
and context menus share a 300 ms submenu grace period and support nested arrow-key navigation.

```csharp
gui.MenuBar(menus =>
{
    menus.Menu("File", file =>
    {
        file.Item("New", () => NewFile(), "Ctrl+N");
        file.Separator();
        file.CheckItem("Autosave", () => _autosave, v => _autosave = v);
        file.Submenu("Export", export =>
        {
            export.Item("PNG", () => Export("png"));
            export.Item("SVG", () => Export("svg"));
        });
    });
    menus.Menu("Help", help => help.Item("About", () => ShowAbout()));
});
```

## Application Bar

```csharp
using (gui.AppBar(nativeTitlebar: useNativeTitlebar, resizable: true,
    minimumWindowSize: new System.Numerics.Vector2(480, 320)))
{
    gui.MenuBar(menus => menus.Collapsible()
        .Menu("File", file => file.Item("Open", OpenProject)));
    gui.Image(badge, width: 15, height: 15);
    gui.DrawText("Studio");
    gui.Node().ExpandWidth();
    if (gui.Button("◐", 36, 36)) ToggleTheme();
}
```

The scope lays out arbitrary widgets in a horizontal row, with normal gaps, padding and flexible nodes.
Repeated calls append content in order. It creates no content delegates, action lists or application title
model. The operating system's title is supplied separately when creating `GuiWindow`.

Desktop integrations register `IWindowChromeCapability` automatically. Where movement is supported, the bar
replaces native decorations. With `windowControls: true`, it appends minimize, maximize/restore and close
buttons after the content, independently of native decorations or movement support.
Close requests go through `GuiWindow.CloseRequested`, preserving the application's unsaved-work guard.
Passive content such as images and labels, and empty space, drag the window and double-click to maximize.
Buttons, editors, custom interactions and event handlers retain their own gestures.

Set `resizable: true` to enable four border and four corner handles while native decorations are hidden.
The desktop integrations register `IWindowResizeCapability` automatically. Handles show resize cursors,
retain pointer capture outside the window and keep the opposite edges fixed. `minimumWindowSize` defaults
to 160 by 100 logical desktop units; custom handles are disabled with native decorations or maximization.
Native sizing requests made during a GUI frame are applied after rendering finishes using the canvas.

Set `nativeTitlebar: true` to show the operating system's decorations at runtime; set it back to `false`
to use application chrome. The application buttons remain available while native decorations are showing. Keep the
scope at the same call site in both passes, and apply mode changes on the next frame. `windowControls: false`
embeds the bar and releases any decoration management it previously owned.

The current GLFW integrations cannot move native Wayland windows. The bar keeps native decorations when
`CanMove` is false so the window stays movable. Native Wayland custom-titlebar dragging requires a backend
with compositor move requests. Native snap gestures require support from the window integration.
Call `DrawWindowTitlebar(true)` when completely unmounting an application bar that replaced native chrome.

Let `AppBar` manage decorations while it is mounted; calling `DrawWindowTitlebar` independently every frame
competes with its remembered mode. Switching OpenGL/Vulkan wrappers does not add native Wayland dragging:
the GLFW integrations need compositor move support or an X11/XWayland startup choice.

### Extending a host application

Hosts can draw their own shell and registered plugin widgets inside the scope. `AppBarScope` is a value type;
extend the composed content through host methods or extension methods. Keep plugin factories and contribution
registries in the host, cache widget instances, and call their render methods in both GUI passes.

Main-menu actions can continue to use a command registry feeding `MenuBar`; arbitrary widgets such as play
controls or recent projects belong in the AppBar row. Apply contribution additions/removals between frames
so the same widgets appear in both passes, and remove cached instances when their plugin unloads.

### Migrating builder callers

Replace `gui.AppBar(bar => ...)` with a `using (gui.AppBar())` scope. Move `Leading` and `Content` widgets
directly into its body, render visible titles with `gui.DrawText`, and replace `Action` entries with ordinary
buttons. Use normal layout nodes and menus to arrange content and handle overflow.

## Tree View

```csharp
var state = new TreeViewState();
var items = new List<TreeItem>
{
    new("src", "src", 0, HasChildren: true),
    new("src/main", "main.cs", 1),
    new("assets", "assets", 0, HasChildren: true),
};

gui.TreeView(state, items, onClick: e => SelectedPath = e.Item.Label);
```

## Docking

```csharp
var layout = new DockLayout
{
    Root = new DockSplit(Axis.Horizontal, new DockLeaf("files"), new DockLeaf("editor"), 0.4f)
};

gui.DockSpace(layout,
    panelInfo: id => new DockPanelInfo("Files"),
    renderPanel: (id, g) => g.DrawText(id));
```

## License

This project is licensed under the MIT License. See the [LICENSE](../LICENSE) file for details.
