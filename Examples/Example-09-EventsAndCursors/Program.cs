using System.Numerics;
using Guinevere;

namespace Example_09_EventsAndCursors;

/// <summary>
/// DOM-style events, cursor shapes and pointer modes: a list handles its rows' clicks at the ancestor, Escape is an
/// app-wide shortcut on the root, the scrub box wraps the pointer at the window edges, and holding the right button
/// on the orbit pad locks the pointer for relative motion.
/// </summary>
public static class Program
{
    static readonly string[] Fruits = ["Apple", "Banana", "Cherry", "Durian"];
    static readonly List<string> Log = [];
    static string? _selected;
    static float _scrubValue = 50f;
    static float _scrubStart;
    static bool _scrubbing;
    static bool _orbiting;
    static Vector2 _orbit;

    public static void Main()
    {
        var gui = new Gui();
        using var window = new GuiWindow(gui, 720, 480, "Events and cursors");
        window.RunGui(() => Draw(gui));
    }

    static void Draw(Gui gui)
    {
        // Nothing has focus, so keys target the root: app-wide shortcuts listen there.
        gui.On<KeyDownEvent>(e =>
        {
            if (e.Key != KeyboardKey.Escape) return;
            Log.Clear();
            e.PreventDefault();
        });

        if (_scrubbing) gui.RequestPointerMode(PointerMode.Wrapped);
        if (_orbiting) gui.RequestPointerMode(PointerMode.Relative);

        using (gui.Node().Expand().Padding(24).Gap(16).Direction(Axis.Horizontal).Enter())
        {
            FruitList(gui);
            using (gui.Node().Expand().Gap(16).Enter())
            {
                ScrubBox(gui);
                OrbitPad(gui);
                EventLog(gui);
            }
        }
    }

    /// <summary>One listener on the list serves every row: the click bubbles up with the row as its target.</summary>
    static void FruitList(Gui gui)
    {
        using (gui.Node(200).ExpandHeight().Gap(6).Enter())
        {
            gui.On<ClickEvent>(e =>
            {
                var fruit = e.TargetId.StartsWith("fruit/", StringComparison.Ordinal) ? e.TargetId[6..] : null;
                if (fruit is null) return;
                _selected = fruit;
                Record($"list handled click on {fruit} (x{e.ClickCount})");
            });

            foreach (var fruit in Fruits)
                using (gui.Node(-1, 36, $"fruit/{fruit}").ExpandWidth().Cursor(PointerCursor.Hand).Enter())
                {
                    if (gui.Pass != Pass.Pass2Render) continue;
                    var hovered = gui.GetInteractable().OnHover();
                    gui.DrawBackgroundRect(_selected == fruit ? Color.FromArgb(255, 70, 110, 190)
                        : hovered ? Color.FromArgb(255, 55, 60, 75) : Color.FromArgb(255, 40, 44, 55), 8);
                    gui.DrawText(fruit, 16, Color.White, centerInRect: true);
                }
        }
    }

    /// <summary>Drag sideways to change the value; the pointer wraps at the window edges.</summary>
    static void ScrubBox(Gui gui)
    {
        using (gui.Node(-1, 56, "scrub").ExpandWidth().Cursor(PointerCursor.ResizeHorizontal).Enter())
        {
            gui.On<DragStartEvent>(_ =>
            {
                _scrubbing = true;
                _scrubStart = _scrubValue;
            });
            gui.On<DragMoveEvent>(e => _scrubValue = _scrubStart + e.TotalDelta.X * 0.1f);
            gui.On<DragEndEvent>(_ => _scrubbing = false);

            if (gui.Pass != Pass.Pass2Render) return;
            gui.DrawBackgroundRect(Color.FromArgb(255, 40, 44, 55), 8);
            gui.DrawText($"Scrub: {_scrubValue:0.0}", 18, Color.White, centerInRect: true);
        }
    }

    /// <summary>Hold the right button to orbit: the pointer hides and locks, and only its motion counts.</summary>
    static void OrbitPad(Gui gui)
    {
        using (gui.Node(-1, 120, "orbit").ExpandWidth().Cursor(PointerCursor.Crosshair).Enter())
        {
            gui.On<PointerDownEvent>(e =>
            {
                if (e.Button != MouseButton.Right) return;
                _orbiting = true;
                e.PreventDefault();
            });
            gui.On<PointerMoveEvent>(e =>
            {
                if (_orbiting) _orbit += e.Delta;
            });
            gui.On<PointerUpEvent>(e =>
            {
                if (e.Button == MouseButton.Right) _orbiting = false;
            });

            if (gui.Pass != Pass.Pass2Render) return;
            gui.DrawBackgroundRect(Color.FromArgb(255, _orbiting ? 70 : 40, 44, 55), 8);
            gui.DrawText($"Right-drag to orbit  yaw {_orbit.X:0}  pitch {_orbit.Y:0}", 16, Color.White,
                centerInRect: true);
        }
    }

    static void EventLog(Gui gui)
    {
        using (gui.Node().Expand().Gap(4).Cursor(PointerCursor.Text).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;
            gui.DrawText("Events (Escape clears)", 14, Color.Gray);
            foreach (var line in Log.TakeLast(8)) gui.DrawText(line, 14, Color.White);
        }
    }

    static void Record(string line) => Log.Add(line);
}
