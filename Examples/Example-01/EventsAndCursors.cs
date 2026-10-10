using System.Numerics;
using Guinevere;

namespace Example_01;

public partial class Shared
{
    static readonly string[] Fruits = ["Apple", "Banana", "Cherry", "Durian"];
    static readonly (string Name, PointerCursor Cursor)[] CursorShelf =
    [
        ("Hand", PointerCursor.Hand), ("Text", PointerCursor.Text), ("Crosshair", PointerCursor.Crosshair),
        ("Resize", PointerCursor.ResizeHorizontal),
    ];

    static readonly List<string> Log = [];
    static string? _selected;
    static float _scrubValue = 50f;
    static float _scrubStart;
    static bool _scrubbing;
    static bool _orbiting;
    static Vector2 _orbit;

    /// <summary>One listener serves a whole list, a scrub box wraps the pointer, and an orbit pad locks it.</summary>
    static void EventsAndCursors(Gui gui, ColorPalette p)
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

        using (gui.Node().Expand().Gap(16).Direction(Axis.Horizontal).Enter())
        {
            FruitList(gui, p);
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
            {
                ScrubBox(gui, p);
                OrbitPad(gui, p);
                CursorGallery(gui, p);
                EventLog(gui, p);
            }
        }
    }

    /// <summary>One listener on the list serves every row: the click bubbles up with the row as its target.</summary>
    static void FruitList(Gui gui, ColorPalette p)
    {
        using (gui.Node(190).ExpandHeight().Direction(Axis.Vertical).Gap(6).Enter())
        {
            Label(gui, p, "Click bubbles to the list");
            gui.On<ClickEvent>(e =>
            {
                var fruit = e.TargetId.StartsWith("fruit/", StringComparison.Ordinal) ? e.TargetId[6..] : null;
                if (fruit is null) return;
                _selected = fruit;
                Record($"list handled click on {fruit} (x{e.ClickCount})");
            });

            for (var i = 0; i < Fruits.Length; i++)
            {
                var fruit = Fruits[i];
                var node = gui.Node(-1, 40, $"fruit/{fruit}").ExpandWidth().Padding(12, 0).Direction(Axis.Horizontal)
                    .Gap(10).ContentAlignY(0.5f).Cursor(PointerCursor.Hand);
                using (node.Enter())
                {
                    var hovered = gui.Pass == Pass.Pass2Render && node.OnHover();
                    gui.DrawBackgroundRect(_selected == fruit ? p.Accent : hovered ? p.SurfaceHover : p.SurfaceRaised, 8);
                    using (gui.Node(12, 12).Enter())
                        gui.DrawBackgroundRect(_selected == fruit ? p.AccentText : p.Pick(i), 6);
                    gui.DrawText(fruit, 15, _selected == fruit ? p.AccentText : p.Text);
                }
            }
        }
    }

    /// <summary>Drag sideways to change the value; the pointer wraps at the window edges.</summary>
    static void ScrubBox(Gui gui, ColorPalette p)
    {
        using (gui.Node(-1, 56, "scrub").ExpandWidth().AlignContent(0.5f, 0.5f).Cursor(PointerCursor.ResizeHorizontal)
                   .Enter())
        {
            gui.On<DragStartEvent>(_ =>
            {
                _scrubbing = true;
                _scrubStart = _scrubValue;
                Record("scrub: pointer wraps at the window edges");
            });
            gui.On<DragMoveEvent>(e => _scrubValue = _scrubStart + e.TotalDelta.X * 0.1f);
            gui.On<DragEndEvent>(_ =>
            {
                _scrubbing = false;
                Record($"scrub: released at {_scrubValue:0.0}");
            });

            gui.DrawBackgroundRect(_scrubbing ? p.SurfaceHover : p.SurfaceRaised, 8);
            gui.DrawText($"Drag to scrub: {_scrubValue:0.0}", 18, p.Text);
        }
    }

    /// <summary>Hold the right button to orbit: the pointer hides and locks, and only its motion counts.</summary>
    static void OrbitPad(Gui gui, ColorPalette p)
    {
        using (gui.Node(-1, 96, "orbit").ExpandWidth().AlignContent(0.5f, 0.5f).Cursor(PointerCursor.Crosshair).Enter())
        {
            gui.On<PointerDownEvent>(e =>
            {
                if (e.Button != MouseButton.Right) return;
                _orbiting = true;
                Record("orbit: pointer locked");
                e.PreventDefault();
            });
            gui.On<PointerMoveEvent>(e =>
            {
                if (_orbiting) _orbit += e.Delta;
            });
            gui.On<PointerUpEvent>(e =>
            {
                if (e.Button != MouseButton.Right || !_orbiting) return;
                _orbiting = false;
                Record("orbit: pointer released");
            });

            gui.DrawBackgroundRect(_orbiting ? p.SurfaceHover : p.SurfaceRaised, 8);
            gui.DrawText($"Right-drag to orbit    yaw {_orbit.X:0}    pitch {_orbit.Y:0}", 16, p.Text);
        }
    }

    static void CursorGallery(Gui gui, ColorPalette p)
    {
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
            foreach (var (name, cursor) in CursorShelf)
                using (gui.Node(-1, 40, $"cursor/{name}").ExpandWidth().AlignContent(0.5f, 0.5f).Cursor(cursor).Enter())
                {
                    gui.DrawBackgroundRect(p.SurfaceRaised, 8);
                    gui.DrawText(name, 13, p.TextDim);
                }
    }

    static void EventLog(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(12).Gap(3).Cursor(PointerCursor.Text).Enter())
        {
            gui.DrawBackgroundRect(p.Track, 8);
            gui.DrawText("Events (Escape clears)", 12, p.TextDim);
            foreach (var line in Log.TakeLast(8)) gui.DrawText(line, 13, p.Text);
        }
    }

    static void Record(string line) => Log.Add(line);
}
