using Guinevere;

namespace Example_01;

public partial class Shared
{
    static readonly float[] AlignStops = [0f, 0.5f, 1f];
    static float alignX = 0.5f;
    static float alignY = 0.5f;
    static Axis alignDirection = Axis.Horizontal;
    static int alignGap = 8;
    static int alignCount = 4;

    static float containerWidth = 560f;

    /// <summary>Pick an alignment cell, then reshape the live container with direction, gap and child count.</summary>
    static void Alignment(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            using (gui.Node().Direction(Axis.Horizontal).Gap(24).Enter())
            {
                using (gui.Node().Direction(Axis.Vertical).Gap(6).Enter())
                    for (var row = 0; row < AlignStops.Length; row++)
                        using (gui.Node().Direction(Axis.Horizontal).Gap(6).Enter())
                            foreach (var column in AlignStops)
                                AlignCell(gui, p, column, AlignStops[row]);

                using (gui.Node().Direction(Axis.Vertical).Gap(8).Enter())
                {
                    Label(gui, p, "AlignContent: click a cell. Direction:");
                    Choice(gui, p, "align-direction", ["Horizontal", "Vertical"],
                        alignDirection == Axis.Horizontal ? 0 : 1,
                        index => alignDirection = index == 0 ? Axis.Horizontal : Axis.Vertical);
                    Stepper(gui, p, "align-gap", "Gap", alignGap, 0, 40, value => alignGap = value);
                    Stepper(gui, p, "align-count", "Children", alignCount, 1, 8, value => alignCount = value);
                }
            }

            Label(gui, p, $".Direction(Axis.{alignDirection}).Gap({alignGap}).AlignContent({alignX}, {alignY})");
            AlignPreview(gui, p);
        }
    }

    static void AlignCell(Gui gui, ColorPalette p, float cellX, float cellY)
    {
        using var key = gui.ItemKey($"cell/{cellX}/{cellY}");
        var selected = cellX == alignX && cellY == alignY;
        ref var hovered = ref gui.GetData(false);
        var fill = gui.AnimateColor(selected ? p.Accent : hovered ? p.SurfaceHover : p.SurfaceRaised, 0.12f);
        var node = gui.Node(84, 54).Padding(6).Cursor(PointerCursor.Hand);
        using (node.Enter())
        {
            gui.DrawBackgroundRect(fill, 8);
            using (gui.Node().Expand().Direction(alignDirection).Gap(3).AlignContent(cellX, cellY).Enter())
                for (var i = 0; i < 3; i++)
                    using (gui.Node(14, 10).Enter())
                        gui.DrawBackgroundRect(selected ? p.AccentText : p.Pick(i), 2);

            if (gui.Pass != Pass.Pass2Render) return;
            hovered = node.OnHover();
            if (node.OnClick())
                Later(() =>
                {
                    alignX = cellX;
                    alignY = cellY;
                });
        }
    }

    static void AlignPreview(Gui gui, ColorPalette p)
    {
        var x = gui.AnimateFloat(alignX, 0.3f);
        var y = gui.AnimateFloat(alignY, 0.3f);
        using (gui.Node().Expand().Padding(12).Enter())
        {
            gui.DrawBackgroundRect(p.Track, 12);
            gui.DrawRectBorder(gui.CurrentNode.Rect, p.Border, 1f);
            using (gui.Node().Expand().Direction(alignDirection).Gap(alignGap).AlignContent(x, y).Enter())
            {
                gui.DrawBackgroundRect(new Color(p.Accent, 0.12f), 8);
                for (var i = 0; i < alignCount; i++)
                    using (gui.Node(44 + i % 3 * 16, 26 + i % 2 * 12).AlignContent(0.5f, 0.5f).Enter())
                    {
                        gui.DrawBackgroundRect(p.Pick(i), 8);
                        gui.DrawText($"{i + 1}", 15, p.AccentText);
                    }
            }
        }
    }

    /// <summary>Drag the container's right edge to see percentages, Expand, Min/Max and Wrap respond.</summary>
    static void Sizing(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            Label(gui, p, $"Container width {containerWidth:0}px: drag the handle");
            using (gui.Node().Expand().Direction(Axis.Horizontal).Enter())
            {
                SizingContainer(gui, p);
                ResizeHandle(gui, p, "sizing/handle", () => containerWidth, value => containerWidth = value, 160f,
                    1100f);
            }
        }
    }

    static void SizingContainer(Gui gui, ColorPalette p)
    {
        using (gui.Node().Width(containerWidth).ExpandHeight().Direction(Axis.Vertical).Padding(16).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(p.Track, 12);
            gui.ScrollY(p.Track, p.Border);

            Label(gui, p, "WidthPercent: 25% / 50% / 75%");
            foreach (var fraction in new[] { 0.25f, 0.5f, 0.75f })
                using (gui.Node(height: 24).WidthPercent(fraction).AlignContent(0.5f, 0.5f).Enter())
                {
                    gui.DrawBackgroundRect(p.Accent, 6);
                    gui.DrawText($"{fraction:P0}", 12, p.AccentText);
                }

            Label(gui, p, "ExpandWidth().MaxWidth(320): grows, then stops");
            using (gui.Node(height: 28).ExpandWidth().MaxWidth(320f).AlignContent(0.5f, 0.5f).Enter())
            {
                gui.DrawBackgroundRect(p.Series[1], 6);
                gui.DrawText("max 320px", 12, p.AccentText);
            }

            Label(gui, p, "Fixed 70 | Expand | Fixed 70");
            using (gui.Node(height: 28).ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
            {
                using (gui.Node(70, 28).AlignContent(0.5f, 0.5f).Enter())
                {
                    gui.DrawBackgroundRect(p.Series[2], 6);
                    gui.DrawText("70", 12, p.AccentText);
                }

                using (gui.Node(height: 28).ExpandWidth().AlignContent(0.5f, 0.5f).Enter())
                {
                    gui.DrawBackgroundRect(p.Series[4], 6);
                    gui.DrawText("Expand", 12, p.AccentText);
                }

                using (gui.Node(70, 28).AlignContent(0.5f, 0.5f).Enter())
                {
                    gui.DrawBackgroundRect(p.Series[2], 6);
                    gui.DrawText("70", 12, p.AccentText);
                }
            }

            Label(gui, p, "Wrap: chips flow onto new rows as the width shrinks");
            using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Gap(8).Enter())
                for (var i = 0; i < 9; i++)
                    using (gui.Node(84, 28).AlignContent(0.5f, 0.5f).Enter())
                    {
                        gui.DrawBackgroundRect(p.Pick(i), 14);
                        gui.DrawText($"chip {i + 1}", 12, p.AccentText);
                    }
        }
    }
}
