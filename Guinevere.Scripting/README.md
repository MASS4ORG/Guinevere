# Guinevere.Scripting

Drives a Guinevere UI without a window, so pointer-driven behavior — drags, docking, splitters,
menus — can be exercised in tests, in CI, or while debugging.

Not referenced by the core library; add it only where you need it.

## Injecting input directly

```csharp
var input = new ScriptedInputHandler();
var gui = new Gui { Input = input };

input.MoveTo(50, 50);
input.PressButton();
// ... render a frame ...
input.NewFrame();   // once per frame, to roll the press/release edges
```

## Replaying a JSON script

```json
{
  "width": 1400,
  "height": 900,
  "steps": [
    { "action": "move", "x": 40, "y": 59 },
    { "action": "press", "button": "left" },
    { "action": "move", "x": 1220, "y": 300 },
    { "action": "dump", "path": "mid-drag.png" },
    { "action": "release", "button": "left" },
    { "action": "expectCapture", "value": "none" }
  ]
}
```

```csharp
var script = InputScript.FromJson(File.ReadAllText(path));
var player = new InputScriptPlayer(input, outputDirectory: "artifacts");
var passed = player.Play(script, gui, DrawMyUi);
```

`Play` returns false if any `expect…` step failed, so a script doubles as a CI check. See
`InputAction` for the full list of steps.
