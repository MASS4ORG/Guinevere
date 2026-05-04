using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.OpenTK;
using Math = System.Math;

namespace Sample_73_PanGui_MusicApp;

public partial class Program
{
    // Style constants
    private readonly float _gap = 8;
    private readonly float _padding = 10;
    private readonly float _borderRadius = 5;
    private readonly Font _arialNormal = Font.LoadFont("arial");
    private readonly Font _arialBold = Font.LoadFont("arialbd");
    private readonly Font fonts = Font.LoadFont("fa-regular-400");
    private Gui _gui = null!;
    private GuiWindow _window = null!;

    public static void Main()
    {
        var program = new Program();
        program.Run();
    }

    private void Run()
    {
        _gui = new Gui();
        _window = new GuiWindow(_gui, 1600, 900, "PanGui Music Production App");

        _window.RunGui(Draw);
    }

    private void Draw()
    {
        // Set default font, text color, and size for the windowHandler
        _gui.SetTextFont(_arialNormal);
        _gui.SetTextColor(Color.Blue);
        _gui.SetTextSize(18);

        // Draw the windowHandler background
        _gui.DrawBackgroundRect().RadialGradientColor(0x292423FF, 0x322D29FF, 0, _gui.ScreenRect.MaxDimension);

        DrawTopToolbar();

        // The root layout node that helps us layout all the main sections of the application
        using (_gui.Node().Expand().Gap(_gap).Margin(_gap).Direction(Axis.Horizontal).Enter())
        {
            // Section for instruments and snapshots on the left
            using (_gui.Node().Expand().Gap(_gap).Enter())
            {
                // Draw the instrument controls
                DrawInstruments();
            }

            // Section for track list and pads on the right
            using (_gui.Node(400, UnitValue.Expand()).Gap(_gap).Enter())
            {
                // Draw the library
                using (_gui.Node().Expand().Enter())
                {
                    DrawLibrary();
                }

                // Draw the pad player
                using (_gui.Node().ExpandWidth().Enter())
                {
                    DrawPadPlayer(padPlayer);
                }
            }
        }

        // Section for the piano, mod wheel, and additional controls at the bottom
        using (_gui.Node().ExpandWidth().Enter())
        {
            // PanGui lets you pass LayoutNodes around as arguments to functions, which enables a sort of
            // semi-retained pattern within the immediate mode pattern  - which is pretty cool!
            // In this case, we give the DrawPianoSection function a LayoutNode to put the piano buttons into.
            LayoutNode pianoButtonsLayoutNode;
            float radius = 90;
            ShapePos s1 = ShapePos.Rectangle(_gui.CurrentNode.LastChild.OuterRect);
            ShapePos s2 =
                ShapePos.RectangleRounded(
                    _gui.CurrentNode.FirstChild.ChildNodes[3].Rect.Expand(30, 0).AddHeight(80),
                    radius);
            ShapePos pianoBgShape = s1.SmoothUnion(s2, radius);

            // Draw the snapshot controls and prepare layout for piano buttons
            using (_gui.Node().Direction(Axis.Horizontal).Gap(_gap).Margin(_gap).MarginTop(0).ExpandWidth().Enter())
            {
                // The mask to cut off some of the snapshot buttons with
                ShapePos mask = pianoBgShape.Expand(10);

                DrawSnapshotButton(snapshots[0], default);
                DrawSnapshotButton(snapshots[1], default);
                DrawSnapshotButton(snapshots[2], mask);
                pianoButtonsLayoutNode = _gui.Node().ExpandHeight();
                DrawSnapshotButton(snapshots[3], mask);
                DrawSnapshotButton(snapshots[4], default);
                DrawSnapshotButton(snapshots[5], default);
            }
            ...

                using (_gui.Node().ExpandWidth().Enter())
            {
                DrawPianoSection(pianoButtonsLayoutNode, pianoBgShape);
            }
        }

        return;
    });
    }

    private LayoutNode DrawSnapshotButton(Snapshot snapshot, ShapePos mask)
    {
        // Draws the horizontal list of buttons below the instruments and volume sliders
        using (_gui.Node(UnitValue.Expand(), 60).Gap(10).Enter())
        {
            bool isSelected = snapshot == selectedSnapshot;
            ShapePos bgShape = ShapePos.RectangleRounded(_gui.CurrentNode.Rect.Expand(-3), _borderRadius);

            if (mask != default)
                bgShape = bgShape - mask;

            StyleButton(isSelected, bgShape);

            _gui.DrawText(snapshot.Name);

            if (_gui.CurrentNode.OnClick())
                selectedSnapshot = snapshot;

            return _gui.CurrentNode;
        }
    }

    private void DrawInstruments()
    {
        // You may have noticed that in most places, we enter nodes to add content to them.
        // However, that is not the only way.
        //
        // This:
        //
        // using (gui.Node().Enter())
        // {
        //     LayoutNode a = gui.Node();
        //     LayoutNode b = gui.Node();
        // }
        //
        // Is the same as this:
        //
        // LayoutNode container = gui.Node().Enter();
        // LayoutNode a = gui.Node();
        // LayoutNode b = gui.Node();
        // container.Exit();
        //
        // And it's also the same as this (less the state management aspects of entering and exiting scopes):
        //
        // LayoutNode container = gui.Node();
        // LayoutNode a = container.AppendNode();
        // LayoutNode b = container.AppendNode();
        //
        // This has many use cases, but in this case we simply use it to reduce the amount of nesting,
        // and to make the code more readable.

        LayoutNode container = _gui.Node().Expand().Direction(Axis.Horizontal).Gap(_gap);

        // Draws the horizontal list of instruments, effects and volume sliders
        foreach (ref Instrument instrument in selectedSnapshot.Instruments.AsSpan())
        {
            using (container.AppendNode().Expand().Enter())
            {
                StyleBox();

                float offsetY = container.Rect.Height * 0.5f;
                _gui.DrawBackgroundRect(_borderRadius)
                    .RadialGradientColor(new Color(instrument.Color, 0.2f), new Color(instrument.Color, 0), 0, 400,
                        0,
                        -offsetY)
                    .RadialGradientColor(new Color(instrument.Color, 0.2f), new Color(instrument.Color, 0), 0, 200,
                        0,
                        -offsetY);

                Popup adsrPopup = GetPopup();

                // Draw header
                using (_gui.Node().Enter())
                {
                    StyleHeader();

                    Rect graphRect = _gui.CurrentNode.Rect.AlignRight(100).Padding(8);
                    InteractableElement graphInteractable = _gui.GetInteractable(graphRect);
                    float graphHover = _gui.AnimateBool01(graphInteractable.OnHover() || adsrPopup.IsVisible(), 0.2f,
                        Easing.SmoothStep);
                    _gui.DrawText(instrument.Name);
                    _gui.DrawRect(graphRect.Expand(1), 4, 0x00000088);

                    DrawAdsrGraph(graphRect, instrument.adsr,
                        new Color(instrument.Color, 0.4f + graphHover * 0.4f));

                    if (graphInteractable.OnClick())
                    {
                        adsrPopup.Show();
                    }

                    if (adsrPopup.IsVisible())
                    {
                        DrawDefaultPopupTitleAndFooter(adsrPopup, "Attack, Decay, Sustain, Release");

                        using (adsrPopup.BodyContainer.Enter())
                        {
                            DrawAdsrSettings(instrument.adsr);
                        }
                    }
                }

                // Fancy audio wave:
                using (_gui.Node().ExpandWidth().MarginBottom(_padding).Enter())
                {
                    _gui.DrawBackgroundRect(0x00000044);

                    Color color = new Color(instrument.Color, 0.7f);

                    for (int n = 0; n < 2; n++)
                    {
                        var rect = _gui.Node(UnitValue.Expand(), 40).Rect;
                        var count = (int)((rect.Width - 20) / 2);

                        // Draw some random audio wave
                        if (count > 2)
                        {
                            unsafe
                            {
                                // Currently, we create the graph by manually modifying vertex data.
                                // Eventually, there will be a slightly higher level and simpler API
                                // for creating graphs from data.
                                ImVertexQuad* quads = _gui.DrawList.AddTriangulatedQuads(count);
                                var r = rect.AlignLeft(2).AlignCenterY(2);
                                var center = rect.Center.Y;
                                var t = MathF.PI - _gui.Time.Elapsed * 2 + n * 0.2f;

                                for (int i = 0; i < count; i++)
                                {
                                    float f = i / (count - 1f);
                                    f = MathF.Cos(t + f * MathF.PI * 6) * 0.5f + 0.5f;
                                    f *= 0.2f + Math.Abs(ImMath.Noise(i * 3.04f, t * 0.4f));
                                    r.Height = Math.Max(f * rect.Height * 0.5f, 2);
                                    r.Y = center - r.Height * 0.5f;
                                    quads[i].Set(r, color);
                                    r.X += 2;
                                }
                            }
                        }
                    }
                }

                // Draw effect buttons
                using (_gui.Node().ExpandWidth().Padding(20).Gap(20).Enter())
                {
                    foreach (ref var effect in instrument.Effects.AsSpan())
                    {
                        DrawEffectButton(ref effect, instrument.Color);
                    }
                }

                // Draw Pan
                using (_gui.Node().ExpandWidth().AlignContent(0.5f).MarginBottom(20).Gap(10).Enter())
                {
                    DrawKnobSlider(0x74B8DBFF, 60, ref instrument.Pan, true);
                    _gui.DrawText("Pan");
                }

                // Draw sliders
                using (_gui.Node().Expand().MarginBottom(40).Gap(5).Direction(Axis.Horizontal).AlignContent(0.5f)
                           .Enter())
                {
                    int rulerMargin = 8;
                    LayoutNode leftRulerNode = _gui.Node(8, UnitValue.Expand()).Margin(0, rulerMargin);
                    LayoutNode sliderNode = DrawVolumeSlider(instrument.Color, ref instrument.Volume).Width(26);
                    LayoutNode rulerNumbersNode = _gui.Node(10, UnitValue.Expand()).ContentAlignX(0.5f)
                        .Spacing(Spacing.SpaceBetween).Margin(8, rulerMargin);
                    LayoutNode volumnBarNode = _gui.Node(26, UnitValue.Expand());
                    LayoutNode rightRulerNode = _gui.Node(8, UnitValue.Expand()).Margin(0, rulerMargin);

                    int rulerLineCount = 15;
                    using (rulerNumbersNode.Enter())
                    {
                        _gui.SetTextSize(13);
                        _gui.SetTextColor(0xffffff55);
                        for (int i = 0; i < rulerLineCount; i++)
                            _gui.DrawText((i + 1) + "");
                    }

                    _gui.DrawRect(volumnBarNode, 6).SolidColor(0x00000088).InnerShadow(0x00000099, 4);
                    (float left, float right) volume = GetSimulatedOutputVolume();
                    DrawVolumeBar(volumnBarNode.Rect.Padding(6).AlignLeft(5), volume.left * instrument.Volume);
                    DrawVolumeBar(volumnBarNode.Rect.Padding(6).AlignRight(5), volume.right * instrument.Volume);
                    _gui.DrawLinesY(leftRulerNode, rulerLineCount, 0xffffff20, 2);
                    _gui.DrawLinesY(rightRulerNode, rulerLineCount, 0xffffff20, 2);
                }
            }
        }
    }

    private unsafe void DrawAdsrGraph(Rect graphRect, ADSR adsr, Color color)
    {
        Color colGradientTop = new Color(color, color.A * 0.4f);
        Color colGradientBottom = new Color(color, 0);

        graphRect = graphRect.Padding(5).AddY(5);

        float totalWidth = graphRect.Width;
        float totalHeight = graphRect.Height;

        float smoothAttack = _gui.Smoothdamp(adsr.Attack);
        float smoothDecay = _gui.Smoothdamp(adsr.Decay);
        float smoothSustain = _gui.Smoothdamp(adsr.Sustain);
        float smoothRelease = _gui.Smoothdamp(adsr.Release);

        Vector2 pStart = graphRect.BL;
        Vector2 pAttack = pStart + new Vector2(smoothAttack * totalWidth, -totalHeight);
        Vector2 pDecay = pAttack + new Vector2(smoothDecay * totalWidth, (1 - smoothSustain) * totalHeight);
        Vector2 pSustain = pDecay + new Vector2(0.15f * totalWidth, 0);
        Vector2 pRelease = new Vector2(pSustain.X + smoothRelease * totalWidth, pStart.Y);

        Span<Vector2> points = [pStart, pAttack, pDecay, pSustain, pRelease];
        Span<float> curvePowers = [0.7f, 0.5f, 1, 0.5f];
        Span<int> curveResolutions = [10, 10, 2, 10];

        for (int i = 0; i < points.Length - 1; i++)
        {
            float pow = curvePowers[i];
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];
            int nResolution = curveResolutions[i];
            Vector2 pFrom = p1;
            for (int j = 1; j < nResolution; j++)
            {
                float t = j / (nResolution - 1f);
                Vector2 pTo = new(p1.X + (p2.X - p1.X) * (1 - MathF.Pow(1 - t, pow)),
                    p1.Y + (p2.Y - p1.Y) * MathF.Pow(t, pow));

                _gui.DrawLine(pFrom, pTo, color, 2);

                // Currently, we create the graph by manually modifying vertex data.
                // Eventually, there will be a slightly higher level and simpler API
                // for creating graphs from data.
                ImVertexQuad* quad = _gui.DrawList.AddTriangulatedQuad();
                quad->SetColor(color);
                quad->TL.Pos = pFrom;
                quad->BL.Pos = new Vector2(pFrom.X, pStart.Y);
                quad->TR.Pos = pTo;
                quad->BR.Pos = new Vector2(pTo.X, pStart.Y);
                quad->TL.Col = Color.Lerp(colGradientTop, colGradientBottom,
                    1 - (pStart.Y - pFrom.Y) / totalHeight);
                quad->TR.Col = Color.Lerp(colGradientTop, colGradientBottom, 1 - (pStart.Y - pTo.Y) / totalHeight);
                quad->BL.Col = colGradientBottom;
                quad->BR.Col = colGradientBottom;

                pFrom = pTo;
            }
        }
    }

    private unsafe void DrawAdsrSettings(ADSR adsr)
    {
        // Draws the Attack, Decay, Sustain, Release settings for the selected instrument.
        Rect graphRect = _gui.Node(700, 300).MarginBottom(_gap).Rect;

        _gui.DrawLinesX(graphRect.HPadding(20), graphRect.Width / 20, 0xffffff11);
        _gui.DrawLinesY(graphRect.VPadding(20), graphRect.Height / 20, 0xffffff11);
        _gui.DrawRect(graphRect, _borderRadius)
            .OuterShadow(0xffffff44, new Vector2(0, 1), 1)
            .InnerShadow(0x000000ff, new Vector2(0, 10), 40, -10)
            .SolidColor(0x00000099);

        float totalWidth = graphRect.Width - 90;
        float totalHeight = graphRect.Height - 90;

        Vector2 pStart = graphRect.Padding(20).BL;
        Vector2 pAttack = pStart + new Vector2(adsr.Attack * totalWidth, -totalHeight);
        Vector2 pDecay = pAttack + new Vector2(adsr.Decay * totalWidth, (1 - adsr.Sustain) * totalHeight);
        Vector2 pSustain = pDecay + new Vector2(0.15f * totalWidth, 0);
        Vector2 pRelease = new Vector2(pSustain.X + adsr.Release * totalWidth, pStart.Y);

        Span<Vector2> points = [pStart, pAttack, pDecay, pSustain, pRelease];
        Span<Color> colors = [0xF04848FF, 0xF0A948FF, 0x3BBE69FF, 0x5074ecff];
        Span<float> curvePowers = [0.7f, 0.5f, 1, 0.5f];
        Span<int> curveResolutions = [30, 30, 2, 30];
        Shape circle = Shape.Circle(6);

        for (int i = 0; i < points.Length - 1; i++)
        {
            Color color = colors[i];
            Color colGradientTop = new Color(color, 0.4f);
            Color colGradientBottom = new Color(color, 0);

            _gui.PushZIndex(_gui.State.ZIndex + 1);
            _gui.DrawShape(points[i + 1], circle).SolidColor(color);
            _gui.PopZIndex();

            float pow = curvePowers[i];
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];
            int nResolution = curveResolutions[i];

            Vector2 pFrom = p1;
            for (int j = 1; j < nResolution; j++)
            {
                float t = j / (nResolution - 1f);
                Vector2 pTo = new(p1.X + (p2.X - p1.X) * (1 - MathF.Pow(1 - t, pow)),
                    p1.Y + (p2.Y - p1.Y) * MathF.Pow(t, pow));
                _gui.DrawLine(pFrom, pTo, color, 2);
                ImVertexQuad* quad = _gui.DrawList.AddTriangulatedQuad();
                quad->SetColor(color);
                quad->TL.Pos = pFrom;
                quad->BL.Pos = new Vector2(pFrom.X, pStart.Y);
                quad->TR.Pos = pTo;
                quad->BR.Pos = new Vector2(pTo.X, pStart.Y);
                quad->TL.Col = Color.Lerp(colGradientTop, colGradientBottom,
                    1 - (pStart.Y - pFrom.Y) / totalHeight);
                quad->TR.Col = Color.Lerp(colGradientTop, colGradientBottom, 1 - (pStart.Y - pTo.Y) / totalHeight);
                quad->BL.Col = colGradientBottom;
                quad->BR.Col = colGradientBottom;
                pFrom = pTo;
            }
        }

        var sustainDragRect = new Rect(pDecay.X - 10, pDecay.Y - 10, (pSustain.X - pDecay.X) + 20, 20);
        InteractableElement sustainHandle = _gui.GetInteractable(sustainDragRect);
        var attackHandle = _gui.GetInteractable(points[1], Shape.Circle(20));
        var decayHandle = _gui.GetInteractable(points[2], Shape.Circle(20));
        var releaseHandle = _gui.GetInteractable(points[4], Shape.Circle(20));

        if (attackHandle.OnHover() || attackHandle.OnHold()) _gui.DrawCircle(points[1], 15, 0xffffff22);
        if (decayHandle.OnHover() || decayHandle.OnHold()) _gui.DrawCircle(points[2], 15, 0xffffff22);
        if (sustainHandle.OnHover() || sustainHandle.OnHold()) _gui.DrawRect(sustainDragRect, 15, 0xffffff22);
        if (releaseHandle.OnHover() || releaseHandle.OnHold()) _gui.DrawCircle(points[4], 15, 0xffffff22);

        var mouseDelta = _gui.Input.MouseDelta / graphRect.Size;

        if (attackHandle.OnHold())
            adsr.Attack = Math.Clamp(adsr.Attack + mouseDelta.X, 0, 1);

        if (decayHandle.OnHold())
            adsr.Decay = Math.Clamp(adsr.Decay + mouseDelta.X, 0, 1);

        if (sustainHandle.OnHold())
            adsr.Sustain = 1 - Math.Clamp((1 - adsr.Sustain) + mouseDelta.Y, 0, 1);

        if (releaseHandle.OnHold())
            adsr.Release = Math.Clamp(adsr.Release + mouseDelta.X, 0, 1);

        using (_gui.Node().ExpandWidth().Spacing(Spacing.SpaceEvenly).Direction(Axis.Horizontal).Enter())
        {
            DrawKnobSliderWithLabel(colors[0], ref adsr.Attack, "Attack", $"{adsr.Attack * 1000:0} ms");
            DrawKnobSliderWithLabel(colors[1], ref adsr.Decay, "Decay", $"{adsr.Decay * 1000:0} ms");
            DrawKnobSliderWithLabel(colors[2], ref adsr.Sustain, "Sustain", $"{adsr.Sustain * 100:0} %");
            DrawKnobSliderWithLabel(colors[3], ref adsr.Release, "Release", $"{adsr.Release * 1000:0} ms");
        }
    }

    private void DrawEffectSettings(Popup popup, ref Effect effect)
    {
        _gui.CurrentNode
            .Margin(_gap)
            .Gap(_gap)
            .Direction(Axis.Horizontal);

        using (_gui.Node().Padding(_gap).AlignContent(0.5f).Enter())
        {
            StyleInnerPopupBox("Settings");

            using (_gui.Node().Direction(Axis.Horizontal).Wrap(3).Enter())
            {
                foreach (ref var knob in effect.KnobValues.AsSpan())
                {
                    using (_gui.Node().Padding(_gap * 2).Gap(_gap * 2).AlignContent(0.5f).Enter())
                    {
                        _gui.DrawText("Low cut");
                        DrawKnobSlider(0xE6B455FF, 70, ref knob);
                        _gui.DrawText(knob.ToString("0.0"));
                    }
                }
            }
        }

        using (_gui.Node().Padding(_padding).ExpandHeight().AlignContent(0.5f).Enter())
        {
            StyleInnerPopupBox("Output");

            using (_gui.Node().ExpandHeight().Padding(_gap * 2, _gap).Direction(Axis.Horizontal).Gap(40).Enter())
            {
                using (_gui.Node().Padding(_padding).ExpandHeight().Gap(_gap).AlignContent(0.5f).Enter())
                {
                    _gui.DrawText("Dry");
                    DrawVolumeSlider(0xE6B556FF, ref effect.Dry).Width(30);
                    _gui.DrawText(effect.Dry.ToString("0.0"));
                }

                using (_gui.Node().Padding(_padding).ExpandHeight().Gap(_gap).AlignContent(0.5f).Enter())
                {
                    _gui.DrawText("Wet");
                    DrawVolumeSlider(0xE6B556FF, ref effect.Wet).Width(30);
                    _gui.DrawText(effect.Wet.ToString("0.0"));
                }
            }
        }

        void StyleInnerPopupBox(string title)
        {
            _gui.DrawBackgroundRect(_borderRadius).SolidColor(0xffffff11);

            using (_gui.Node().ExpandWidth().AlignContent(0.5f).Margin(_gap).MarginBottom(_gap).Enter())
            {
                _gui.DrawText(title);
            }

            var line = _gui.Node(UnitValue.Expand(), 2).MarginBottom(_gap);
            _gui.DrawRect(line.Rect.Expand(_gap, 0), 0x00000044);
        }
    }

    private void DrawPianoSection(LayoutNode pianoButtonsContainer, ShapePos pianoBgShape)
    {
        float popupVisibility = GetPopupVisibility();

        // Draw the piano buttons always on top
        _gui.SetZIndex(1000);
        _gui.SetTransform(Matrix3x2.CreateScale(1, 1 + popupVisibility * 0.05f, _gui.CurrentNode.Rect.BottomCenter));

        _gui.DrawShape(pianoBgShape.Pos, pianoBgShape.Shape)
            .SolidColor(Color.Lerp(0x151313FF, 0x292423FF, popupVisibility))
            .InnerShadow(Color.FromArgb((int)((1 - popupVisibility) * 255), 0x00000088), new Vector2(0, 40),
                80, -40)
            .InnerShadow(Color.FromArgb((int)((1 - popupVisibility) * 255), 0x00000088), new Vector2(0, 10),
                20, -10)
            .SolidColor(Color.FromArgb((int)(popupVisibility * 255), 0x00000044))
            .InnerShadow(Color.FromArgb((int)(popupVisibility * 255), 0xffffff11), new Vector2(0, 40), 80,
                -40)
            .OuterShadow(Color.FromArgb((int)(popupVisibility * 255), 0x000000ff), 20)
            .Stroke(0x000000ff, 1, 0);

        // Here we jump into the layout node we were passed, which was created separately.
        using (pianoButtonsContainer.Direction(Axis.Horizontal).Enter())
        {
            float t = 0.5f;
            DrawKnobSlider(0x74B8DBFF, 60, ref t).Margin(15);
            DrawKnobSlider(0xE5B657FF, 60, ref t).Margin(15);
            DrawKnobSlider(0x3DBF6BFF, 60, ref t).Margin(15);
        }

        using (_gui.Node().ExpandWidth().Direction(Axis.Horizontal).Height(260).MarginTop(20).Padding(_padding)
                   .Gap(_gap).Enter())
        {
            // Draw mod wheel
            {
                var leftRuler = _gui.Node(10, UnitValue.Expand()).Spacing(Spacing.SpaceBetween);
                var wheel = _gui.Node(60, UnitValue.Expand()).Rect;
                var rightRuler = _gui.Node(10, UnitValue.Expand()).Spacing(Spacing.SpaceBetween);
                var handle = wheel.Padding(0, 5).AlignTop(30).AddY(modWheel * (wheel.Height - 36));
                var shadowShape = Shape.Circle(300).MoveX(-220) * Shape.Rectangle(400, wheel.Height).MoveX(180);
                var handleShape = Shape.RectangleRounded(wheel.Width, 20, 5);

                // shadow
                _gui.DrawShape(wheel.Center, shadowShape)
                    .DiamondGradientColor(0x000000cc, 0x00000000, 50);

                // wheel
                _gui.DrawRect(wheel, 5)
                    .OuterShadow(0xffffff22, new Vector2(0, 3), 3)
                    .SolidColor(0xffffff11)
                    .Stroke(0x000000ff, 3);

                // handle
                _gui.DrawRect(handle)
                    .LinearGradientColor(0x00000088, 0xffffff11, Angle.Turns(-0.25f), 0.54f)
                    .InnerShadow(0x000000ff, new Vector2(0, 1), 1, 1)
                    .InnerShadow(0xffffff22, new Vector2(0, -1), 1, 1);

                // lighting
                _gui.DrawRect(wheel, 5)
                    .InnerShadow(0xffffff11, new Vector2(8, 0), 5f, -5) // left light
                    .InnerShadow(0x000000cc, new Vector2(-8, 0), 5f, -5) // right shadow
                    .InnerShadow(0x000000cc, new Vector2(0, 10), 10f, -10) // top shadow
                    .LinearGradientColor(0x000000ee, 0x00000000, MathF.PI * 0.5f, 0.6f,
                        offsetY: -0.5f) // 3d effect
                    .LinearGradientColor(0x00000000, 0x00000044, MathF.PI * 0.5f, 0.8f,
                        offsetY: 0.8f); // 3d effect

                // Draw left and right rulers:
                {
                    var numLines = 20;
                    for (int i = 0; i < numLines; i++)
                    {
                        float f = i / (float)numLines;
                        var left = leftRuler.AppendNode(UnitValue.Expand(), 2);
                        var right = rightRuler.AppendNode(UnitValue.Expand(), 2);
                        float brightness = _gui.Smoothdamp(f >= modWheel ? 1 : 0f, 10);
                        Color color = Color.Lerp(0x000000ff, 0xE6B455FF, brightness);
                        float outerShadow = brightness * 8;
                        _gui.DrawRect(left.Rect, 1).SolidColor(color)
                            .OuterShadow(new Color(color, 0.2f), outerShadow, 2);
                        _gui.DrawRect(right.Rect, 1).SolidColor(color)
                            .OuterShadow(new Color(color, 0.2f), outerShadow, 2);
                    }
                }

                InteractableElement e = _gui.GetInteractable(wheel);
                if (e.OnHold(out var args))
                {
                    var slideArea = wheel.Padding(0, handle.Height * 0.5f);
                    modWheel = Math.Clamp(_gui.Input.MousePosition.Y - slideArea.Top, 0, slideArea.Height) /
                               slideArea.Height;
                }
            }

            _gui.Node(_gap);

            using (_gui.Node().Expand().Gap(4).Enter())
            {
                float numKeys = 7 * 12f;

                foreach (var instrument in selectedSnapshot.Instruments)
                {
                    Color color = instrument.Color;
                    float keyStart = _gui.Smoothdamp((1f / numKeys) * instrument.KeyStart);
                    float keyLength = _gui.Smoothdamp((1f / numKeys) * instrument.KeyLength);

                    Rect rangeRect = _gui.Node(UnitValue.Expand(), 14).Rect;
                    rangeRect.X += keyStart * rangeRect.Width;
                    rangeRect.Width *= keyLength;

                    // bg
                    _gui.DrawRect(rangeRect, 7)
                        .SolidColor(0x00000088)
                        .OuterShadow(0xffffff22, new Vector2(0, 1), 2);

                    // glow
                    _gui.DrawRect(rangeRect.Padding(2), 5)
                        .SolidColor(color)
                        .InnerShadow(0x000000aa, new Vector2(0, 0), 8.2f, -2);
                }

                // Draw Piano
                Rect piano = _gui.Node().MarginTop(10).Expand().Rect;
                DrawKeyboard(piano, keyPressures);
            }
        }
    }

    private void DrawPadPlayer(PadPlayer padPlayer)
    {
        _gui.CurrentNode.Padding(_padding).Gap(_gap);

        StyleBox();

        using (_gui.Node().Gap(_gap).Direction(Axis.Horizontal).ExpandWidth().Enter())
        {
            using (_gui.Node().ExpandWidth().Gap(_gap).Enter())
            {
                _gui.SetTextSize(22);

                using (_gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(_gap).Wrap(4).Enter())
                {
                    for (int i = 0; i < padPlayer.Pads.Length; i++)
                    {
                        using (_gui.Node().ExpandWidth().Height(UnitValue.Ratio(1)).Enter())
                        {
                            StyleButton(i == padPlayer.SelectedPad);
                            _gui.DrawText(padPlayer.Pads[i]);

                            if (_gui.CurrentNode.OnClick())
                            {
                                padPlayer.SelectedPad = i;
                            }
                        }
                    }
                }
            }

            using (_gui.Node().ExpandHeight().Width(80).MarginBottom(20).AlignContent(0.5f).Gap(20).Enter())
            {
                DrawVolumeSlider(0xC2984BFF, ref padPlayer.Volume).Width(25);
                _gui.SetTextSize(22);
                _gui.SetTextFont(_arialBold);
                _gui.DrawText("-5 dB");
            }
        }

        using (_gui.Node().Direction(Axis.Horizontal).Gap(_gap).AlignContent(0.5f).Enter())
        {
            DrawKnobSliderWithLabel(0xE5B458FF, ref padPlayer.Brightness, "Brightness",
                $"{padPlayer.Brightness * 100:0}%");
            _gui.Node(30);
            DrawKnobSliderWithLabel(0xE5B458FF, ref padPlayer.Shimmer, "Shimmer",
                $"{padPlayer.Shimmer * 100:0}%");
        }
    }

    private void DrawKnobSliderWithLabel(Color color, ref float value, string label, string valueLabel)
    {
        using (_gui.Node().Direction(Axis.Horizontal).AlignContent(0.5f).Gap(_gap).Enter())
        {
            DrawKnobSlider(color, 70, ref value);

            using (_gui.Node().Gap(10).Enter())
            {
                _gui.DrawText(label, color: Color.White);
                _gui.DrawText(valueLabel, color: 0xffffff88);
            }

            Color col = 0xF18B46FF;
        }
    }

    private LayoutNode DrawKnobSlider(Color color, UnitValue unitValue, ref float value, bool isPan = false)
    {
        ref float t = ref _gui.Smoothdamp(value);
        LayoutNode node = _gui.Node(unitValue);
        float angleOffset = 0.1f;
        float arcLength = Angle.Turns(1 - angleOffset * 2);
        float arcStart = Angle.Turns(0.25f + angleOffset);

        if (node.IsVisible())
        {
            Vector2 center = node.Rect.Center;
            float arcThickness = 3;
            float arcRadius = node.Rect.W * 0.5f - arcThickness;
            float knobRadius = node.Rect.W * 0.5f - arcThickness - 8;

            Shape knobShape;
            Shape arcSliceShape;

            if (isPan)
            {
                knobShape = Shape.Circle(knobRadius - 4);
                float panLength = Math.Abs(0.5f - t);
                float len = arcLength * panLength;
                float rot = len * 0.5f - Angle.Turns(0.25f);

                if (t < 0.5f)
                {
                    rot -= len;
                }

                arcSliceShape = Shape.Pie(arcRadius * 2, len)
                    .Rotate(rot);

                arcSliceShape *= (knobShape + 10);
                arcSliceShape -= knobShape;
                arcSliceShape -= 3;
            }
            else
            {
                float rotation = arcLength * t * 0.5f + MathF.PI * 0.5f + Angle.Turns(angleOffset);

                knobShape = Shape
                    .Union(Shape.Circle(knobRadius - 4), Shape.EquilateralTriangle(knobRadius).MoveY(-10),
                        smoothness: 10).Rotate(arcStart + arcLength * value + MathF.PI * 0.5f);
                arcSliceShape = Shape.Pie(arcRadius * 2, arcLength * t).Rotate(rotation);
                arcSliceShape *= (knobShape + 10);
                arcSliceShape -= knobShape;
                arcSliceShape -= 3;
            }

            // Bg
            _gui.DrawShape(center, knobShape + 10)
                .InnerShadow(0x00000022, new Vector2(0, 10), 20, -10)
                .InnerShadow(0x000000ff, new Vector2(0, -10), 20, -10)
                .SolidColor(0x00000088);

            _gui.DrawShape(center, arcSliceShape)
                .SolidColor(color);

            //Handle
            _gui.DrawShape(center, knobShape)
                .LinearGradientColor(0xB9B1AFFF, 0x484443FF, Angle.Turns(-0.25f))
                .OuterShadow(0x000000cc, new Vector2(0, knobRadius * 0.5f), knobRadius * 1.5f,
                    knobRadius * 0.2f)
                .InnerShadow(0xffffff22, new Vector2(0, 1), 1, 1);
        }

        InteractableElement e = _gui.GetInteractable(node.Rect);
        if (e.OnHold())
        {
            value += -_gui.Input.MouseDelta.Y / 1000;
            value += _gui.Input.MouseDelta.X / 1000;
            value = Math.Clamp(value, 0, 1f);

            t = value;
        }

        return node;
    }

    private void DrawLibrary()
    {
        StyleBox();

        using (_gui.Node().Enter())
        {
            StyleHeader();
            _gui.DrawText("Library");
        }

        using (_gui.Node().Expand().Margin(1).Enter())
        {
            _gui.ScrollY(0x00000055, 0xffffff22);

            foreach (string title in trackLibrary)
            {
                bool isSelected = title == selectedTrack;
                using (_gui.Node().ExpandWidth().Padding(_padding, 10).Enter())
                {
                    if (isSelected)
                    {
                        _gui.DrawBackgroundRect(0x00000077);
                        _gui.SetTextColor(0xffffff99);
                    }

                    _gui.DrawText(title);

                    if (_gui.CurrentNode.OnClick())
                    {
                        selectedTrack = title;
                    }
                }
            }
        }
    }

    private void DrawTopToolbar()
    {
        using (_gui.Node()
                   .ExpandWidth()
                   .AlignContent(0.5f)
                   .Padding(_gap)
                   .Gap(20)
                   .Direction(Axis.Horizontal)
                   .Enter())
        {
            _gui.SetZIndex(1000);

            _gui.DrawBackgroundRect()
                .SolidColor(Color.Lerp(0x252323FF, 0x393433FF, 1))
                .SolidColor(0x00000044)
                .InnerShadow(0xffffff11, new Vector2(0, 40), 80, -40)
                .OuterShadow(0x000000ff, 10)
                .Stroke(0x000000ff, 1, 0);

            _gui.DrawText("Some Logo", color: Color.White).MarginLeft(10);

            // Spacer
            _gui.Node().Expand();

            {
                DrawToolbarButtonTemplate(out LayoutNodeScope title, out LayoutNodeScope subTitle,
                    out LayoutNodeScope btn);
                using (title.Enter()) _gui.DrawText("Play in");
                using (subTitle.Enter()) _gui.DrawText("per patch");
                using (btn.Enter()) _gui.DrawText("C");
            }

            {
                DrawToolbarButtonTemplate(out LayoutNodeScope title, out LayoutNodeScope subTitle,
                    out LayoutNodeScope btn);
                using (title.Enter()) _gui.DrawText("Hear in");
                using (subTitle.Enter()) _gui.DrawText("per patch");
                using (btn.Enter()) _gui.DrawText("D");
            }

            {
                DrawToolbarButtonTemplate(out LayoutNodeScope title, out LayoutNodeScope subTitle,
                    out LayoutNodeScope btn);
                using (title.Enter()) _gui.DrawText("Tempo");
                using (btn.Enter()) _gui.DrawText("122.00 BPM");
            }

            // Spacer
            _gui.Node().Expand();

            // Menu button placeholder
            _gui.DrawIcon(Icons.MenuBurger, 30).MarginRight(10);
        }
    }

    private void DrawEffectButton(ref Effect effect, in Color instrumentColor)
    {
        LayoutNode container = _gui.Node().ExpandWidth().Direction(Axis.Horizontal);
        LayoutNode toggleEffectNode =
            container.AppendNode(UnitValue.Ratio(1), UnitValue.Expand())
                .AlignContent(0.5f); // Expand height, and make width as wide as it is tall.
        LayoutNode textNode = container.AppendNode().ExpandWidth()
            .AlignContent(0, 0.5f)
            .Gap(7)
            .PaddingY(10)
            .PaddingLeft(_padding);

        float onOffNodeHover = _gui.AnimateBool01(toggleEffectNode.OnHover(), 0.2f, Easing.EaseInOutSine);
        float textNodeHover = _gui.AnimateBool01(textNode.OnHover(), 0.2f, Easing.EaseInOutSine);

        textNode.ContentOffsetX(textNodeHover * 10);

        Popup popup = GetPopup();

        // We could also use an Icon. This is just to showcase the shapes API a little.
        // Angle is a utility struct that returns a float in radians.
        Shape onIconShape = Shape.Arc(13, 2, Angle.Turns(-0.1f), Angle.Turns(0.7f))
                            + Shape.RectangleRounded(4, 16, 2).MoveY(-10);

        float onBrightness = _gui.AnimateBool01(effect.IsOn, 0.2f, Easing.EaseInOutSine);

        Color col1 = Color.Lerp(0x00000077, instrumentColor, onBrightness);
        Color col2 = Color.Lerp(0x00000044, instrumentColor, onBrightness);

        // bg
        _gui.DrawRect(container.Rect, radius: 10)
            .LinearGradientColor(0xffffff44, 0xffffff11, Angle.Turns(-0.15f))
            .SolidColor(new Color(instrumentColor, textNodeHover * 0.1f))
            .OuterShadow(0x00000055, new Vector2(0, 1), 5);

        // icon bg
        _gui.DrawRect(toggleEffectNode.Rect, 10, 0, 0, 10)
            .LinearGradientColor(col1, col2)
            .InnerShadow(0xffffff22, new Vector2(0, 0), 1, 0);

        // icon
        using (toggleEffectNode.Enter())
        {
            Color iconColor = effect.IsOn ? 0xffffffff : Color.Lerp(0xffffff33, 0xffffffff, onOffNodeHover);
            _gui.SetTextColor(iconColor);
            _gui.DrawIcon(Icons.PowerOff, 35);
        }

        using (textNode.Enter())
        {
            // This will clip the contents of the node to the node's size, so overflow content is cut off.
            _gui.ClipContent();
            _gui.DrawText(effect.Type, 0xffffff55, 16);
            _gui.DrawText(effect.Name, 0xffffff88, 20);
        }

        if (popup.IsVisible())
        {
            DrawDefaultPopupTitleAndFooter(popup, effect.Name);

            using (popup.BodyContainer.Enter())
            {
                DrawEffectSettings(popup, ref effect);
            }
        }

        if (toggleEffectNode.OnClick())
        {
            effect.IsOn = !effect.IsOn;
        }

        if (textNode.OnClick())
        {
            popup.Show();
        }
    }

    private void StyleHeader()
    {
        _gui.CurrentNode
            .Height(50)
            // .Spacing(Spacing.SpaceBetween)
            .AlignContent(0, 0.5f)
            .ExpandWidth()
            .Direction(Axis.Horizontal)
            .Padding(20, 0);

        _gui.SetTextColor(0xDCF2F2FF);

        _gui.DrawBackgroundRect()
            .SolidColor(0xffffff11)
            .OuterShadow(0x000000ff, 2);
    }

    private void StyleBox()
    {
        var bgShape =
            Shape.RectangleRounded(_gui.CurrentNode.Rect.Width, _gui.CurrentNode.Rect.Height, _borderRadius);

        _gui.DrawShape(_gui.CurrentNode.Rect.Center, bgShape)
            .OuterShadow(0x00000088, new Vector2(0, 0), 80, 20);

        _gui.DrawShape(_gui.CurrentNode.Rect.Center, bgShape)
            .RadialGradientColor(0xffffff08, 0xffffff05, 0, _gui.CurrentNode.Rect.MaxDimension,
                offsetY: _gui.CurrentNode.Rect.Height * 0.5f)
            .InnerShadow(0xffffff22, 2);
    }

    private void StyleButton(bool on, ShapePos bgShape)
    {
        Rect rect = _gui.CurrentNode
            .Padding(15)
            .MinWidth(UnitValue.Ratio(1))
            .AlignContent(0.5f)
            .Rect;

        InteractableElement e = _gui.CurrentNode.GetInteractable();
        float tOn = _gui.AnimateBool01(on, 0.2f, Easing.EaseInOutSine);
        float tHover = e.OnHover() ? 1 : 0;
        float tDown = _gui.AnimateBool01(e.OnHold(), 0.1f, Easing.EaseInOutSine);
        float tUp = 1 - tDown;

        _gui.DrawShape(bgShape)
            .OuterShadow((0xffffff22, tUp), new Vector2(0, -4), 6, 3)
            .OuterShadow((0x000000ff, tUp), new Vector2(0, 2), 6)
            .InnerShadow((0, 0, 0, tDown * 0.423f), new Vector2(0, 0), 30, -5)
            .InnerShadow((0, 0, 0, tDown * 0.375f), 20)
            .LinearGradientColor((0x00000088, tUp), 0x00000000, Angle.Turns(0.25f))
            .RadialGradientColor((0xF4A73AFF, tOn * tUp), (0xEF753DFF, tOn), 0, rect.Width * 0.5f)
            .InnerShadow((0xA73317FF, tOn * tUp), 4, 0)
            .InnerShadow((1, 1, 1, 0.2f * tUp), new Vector2(0, +3), 2, -2)
            .InnerShadow((1, 1, 1, 0.2f * tDown), new Vector2(0, -3), 2, -2)
            .SolidColor((1, 1, 1, tHover * tUp * 0.05f))
            .RadialGradientColor((0, 0, 0, tDown * 0.6f), 0x00000000, 0, rect.Width * 0.7f);

        Color textColor = 0xFDF5F2FF;
        textColor = Color.Lerp(textColor, 0x130302FF, tOn);
        textColor = Color.Lerp(textColor, 0xffffff44, tDown * (1 - tOn));
        _gui.SetTextColor(textColor);
    }

    private void StyleButton(bool on)
    {
        var rect = _gui.CurrentNode.Rect;
        ShapePos bgShape = ShapePos.RectangleRounded(rect.Expand(-3), _borderRadius);
        StyleButton(on, bgShape);
    }

    private void DrawVolumeBar(Rect r, float t)
    {
        Color green = 0x39BE69FF;
        Color yellow = 0xF0A948FF;
        Color red = 0xF03E3EFF;

        // Adjust the method of drawing gradients to support vertical gradients
        VertexPlane q = _gui.DrawList.AddTriangulatedPlane(2, 4);
        float left = r.Left;
        float right = r.Right;
        float bottom = r.Bottom;
        float h = r.Height;

        // Adjustments to make sure the colors blend correctly from bottom to top
        q[0, 0].Set(new Vector2(left, bottom), Vector2.One, green);
        q[1, 0].Set(new Vector2(right, bottom), Vector2.One, green);
        q[0, 1].Set(new Vector2(left, bottom - h * Math.Min(t, 0.3f)), Vector2.One, green);
        q[1, 1].Set(new Vector2(right, bottom - h * Math.Min(t, 0.3f)), Vector2.One, green);
        q[0, 2].Set(new Vector2(left, bottom - h * Math.Min(t, 0.5f)), Vector2.One, yellow);
        q[1, 2].Set(new Vector2(right, bottom - h * Math.Min(t, 0.5f)), Vector2.One, yellow);
        q[0, 3].Set(new Vector2(left, bottom - h * t), Vector2.One, red);
        q[1, 3].Set(new Vector2(right, bottom - h * t), Vector2.One, red);

        // Note that this is another example of using a lower-level API to easily do
        // things that we have not yet designed nicer, higher-level APIs for, such
        // as drawing a multi step gradient.
        //
        // Since nothing is hidden away, and everything is fully accessible, very few
        // things are impossible to do.
    }

    private LayoutNode DrawVolumeSlider(Color color, ref float volume, float min = 0, float max = 1)
    {
        ref float animatedVolume = ref _gui.GetFloat(volume);
        animatedVolume = ImMath.Lerp(animatedVolume, volume, _gui.Time.DeltaTime * 10);

        float tHeight = Math.Clamp((animatedVolume - min) / (max - min), 0, 1);
        LayoutNode node = _gui.Node().ExpandWidth().ExpandHeight();
        Shape bgShape = Shape.RectangleRounded(node.Rect.Width, node.Rect.Height, node.Rect.Width * 0.4f);
        Rect laneRect = node.Rect.Padding(0, 10).AlignCenterX(8);
        Rect handleRect = node.Rect.SetHeight(35).SetY(laneRect.Y + laneRect.Height * (1 - tHeight) - 20);
        InteractableElement sliderInteractable = _gui.GetInteractable(laneRect);
        InteractableElement handleInteractable = _gui.GetInteractable(handleRect);

        float tFocus = _gui.AnimateBool01(handleInteractable.OnHover() || handleInteractable.OnHold(),
            duration: 0.2f,
            Easing.EaseInOutSine);
        color = Color.Lerp(color, Color.White, tFocus * 0.2f);

        // Bg
        _gui.DrawShape(node, bgShape)
            .LinearGradientColor(0x00000044, 0x00000044);

        // Lane
        _gui.DrawRect(laneRect.Expand(1), 4)
            .SolidColor(0x00000088)
            .OuterShadow(0xffffff22, new Vector2(0, 1), 2);

        // Lane glow
        _gui.DrawRect(laneRect.AlignBottom(laneRect.Height * tHeight), 4)
            .SolidColor(color)
            .InnerShadow(0x000000aa, new Vector2(0, 0), 8.2f, -2);

        // Handle
        _gui.DrawRect(handleRect, 3)
            .LinearGradientColor(0xB9B1AFFF, 0x484443FF, Angle.Turns(-0.25f))
            .OuterShadow(0x000000ff, new Vector2(0, 10), 30, 10)
            .InnerShadow(0xffffff22, new Vector2(0, 1), 1, 1);

        // Handle whole
        _gui.DrawRect(handleRect.AlignCenterY(8))
            .OuterShadow(0xffffff22, new Vector2(0, 2), 1)
            .LinearGradientColor(0xB9B1AFFF, 0x484443FF, Angle.Turns(+0.25f));

        // Input
        if (handleInteractable.OnHold() || sliderInteractable.OnHold())
        {
            volume -= _gui.Input.MouseDelta.Y / laneRect.Height * (max - min);
            volume = Math.Clamp(volume, min, max);
            animatedVolume = volume;
        }

        if (sliderInteractable.OnPointerDown())
        {
            var deltaY = _gui.Input.MousePosition.Y - node.Rect.TL.Y;
            volume = Math.Clamp(1 - deltaY / laneRect.Height, 0, 1);
            animatedVolume = volume;
        }

        // Tooltip
        if (handleInteractable.On(Interactions.Hover | Interactions.Hold))
        {
            DrawTooltip(handleRect.LeftCenter, volume.ToString("0.00") + " dB");
        }

        return node;
    }

    private LayoutNode DrawToolbarButtonTemplate(out LayoutNodeScope titleText, out LayoutNodeScope subTitleText,
        out LayoutNodeScope buttonContents)
    {
        using (_gui.Node().Direction(Axis.Horizontal).Gap(_gap).AlignContent(0.5f).Enter())
        {
            using (_gui.Node().AlignContent(0, 0.5f).Gap(5).Enter())
            {
                titleText = _gui.Node().ToScope();

                using (subTitleText = _gui.Node().Enter())
                {
                    _gui.SetTextSize(15);
                    _gui.SetTextColor(0xffffff44);
                }
            }

            using (buttonContents = _gui.Node().Enter())
            {
                StyleButton(false);
            }

            return _gui.CurrentNode;
        }
    }

    private void DrawKeyboard(Rect piano, float[] keyPressure)
    {
        int numOctaves = keyPressure.Length / 12;
        int numMajorKeys = numOctaves * 7;
        int numMinorKeys = numOctaves * 5;
        Rect majorKeyRect = piano.AlignLeft(piano.Width / numMajorKeys);

        float minorKeyWidth = majorKeyRect.Width * 0.6f;
        Rect minorKeyRect = piano.AlignLeft(minorKeyWidth);
        minorKeyRect.X += majorKeyRect.Width * 0.5f + (majorKeyRect.Width - minorKeyWidth) * 0.5f;
        minorKeyRect.Height = majorKeyRect.Height * 0.55f;

        bool isPressingPiano = _gui.GetInteractable(piano).IgnorePropagation().OnHold(out var args);

        // Draw major keys
        for (int i = 0; i < numMajorKeys; i++)
        {
            ref float pressure = ref keyPressure[i];

            var darkColor = Color.Lerp(0x00000044, 0x00000088, pressure);

            _gui.DrawRect(majorKeyRect, 5)
                .SolidColor(0xffffffff)
                .InnerShadow(0x000000ff, 0.1f, 1)
                .LinearGradientColor(0x00000000, darkColor, Angle.Turns(-0.25f), 0.5f);

            // Simulate key release
            pressure = ImMath.Lerp(pressure, 0, _gui.Time.DeltaTime * 5);
            if (_gui.GetInteractable(majorKeyRect).OnHover() && isPressingPiano)
                pressure = 1;

            majorKeyRect.X += majorKeyRect.W;
        }

        // Draw minor keys
        for (int i = 0; i < numMinorKeys; i++)
        {
            ref float pressure = ref keyPressure[i + numMajorKeys];

            _gui.DrawRect(minorKeyRect, 0, 0, 3, 3)
                .SolidColor(Color.Lerp(0x2A2A31FF, 0x757984FF, pressure))
                .InnerShadow(0x00000088, 1f, 2)
                .InnerShadow(0xffffff44, new Vector2(2, 0), 1f);

            // Simulate key release
            pressure = ImMath.Lerp(pressure, 0, _gui.Time.DeltaTime * 5);
            if (_gui.GetInteractable(minorKeyRect).OnHover() && isPressingPiano)
                pressure = 1;

            minorKeyRect.X += majorKeyRect.Width;
            if (i % 5 == 1 || i % 5 == 4)
                minorKeyRect.X += majorKeyRect.Width;
        }
    }


    // The user-code for this popup function looks like this:
    //
    // Popup popup = GetPopup();
    //
    // if (gui.Button("Show Popup"))
    // {
    //     popup.Show();
    // }
    //
    // if (popup.IsVisible())
    // {
    //     using (popup.BodyContainer.Enter())
    //     {
    //         gui.DrawText("Hello, world!");
    //     }
    // }
    private Popup GetPopup()
    {
        var popup = new Popup() { Visibility = _gui.GetAnimationFloat() };
        float tVisibility01 = popup.Visibility.GetValue();
        // Calculate popup and background scaling for animation effects
        Vector2 scalePopupIn = Vector2.Lerp(Vector2.One, new Vector2(0.9f, 0.9f), 1 - tVisibility01);
        Vector2 scaleBgOut = Vector2.Lerp(Vector2.One, new Vector2(0.95f, 0.95f), tVisibility01);
        if (popup.IsVisible())
        {
            // If you're familiar with HTMl / CSS you can think of this layout node as being position:fixed;
            // We will make that API nicer in the near future! It has all the bells and whistles to be far more capable than HTML/CSS, it's just about making the API nicer.
            using (_gui.Node()
                       .Expand()
                       // .SetParent(_gui.Systems.LayoutSystem.RootNode)
                       .PositionRelativeTo(_gui.Systems.LayoutSystem.RootNode, 0, 0)
                       .AlignContent(0.5f, 0.3f).Enter())
            {
                // Resetting the state to 0, is sort of like a "reset" for the GUI state. In a way it jumps us back to the main loop of the GUI.
                _gui.SetState(0);
                // Data scopes are explained further down on the website. Go check it out!
                _gui.SetDataScope("popup");
                // This makes the popup appear on top of everything else.
                _gui.SetZIndex(100);
                // Draw semi-transparent background for popup
                _gui.DrawRect(_gui.ScreenRect, new Color(0, 0, 0, 0.8f * tVisibility01));
                // Allow popup to be closed by clicking outside its area
                if (_gui.CurrentNode.OnClick())
                    popup.Hide();
                // The default size for a layout node is "Fit", so the inner popup container will be the size of its content
                using (_gui.Node().Enter())
                {
                    _gui.CurrentNode.PreventAllInputPropagation();
                    // Animate the popup.
                    _gui.SetOpacity(tVisibility01);
                    _gui.SetTransform(Matrix3x2.CreateScale(scalePopupIn, _gui.ScreenRect.Center));
                    _gui.DrawBackgroundRect(radius: borderRadius)
                        .RadialGradientColor(0x292423FF, 0x322D29FF, 0, _gui.CurrentNode.Rect.MaxDimension)
                        .InnerShadow(0xffffff11, new Vector2(0, 40), 80, -40)
                        .OuterShadow(0x000000ff, 20)
                        .Stroke(0x000000ff, 1, 0);
                    using (_gui.Node()
                               .Margin(_gap)
                               .Direction(Axis.Horizontal)
                               .ExpandWidth()
                               .Gap(_gap)
                               .MinWidth(UnitValue.Fit)
                               .Margin(20)
                               .AlignContent(0.5f).Enter())
                    {
                        _gui.SetTextSize(20);
                        _gui.SetTextFont(_arialBold);
                        _gui.SetTextColor(0xFFFFFFcc);
                        // Assigns the header container
                        popup.HeaderContainer = _gui.CurrentNode.ToScope();
                    }

                    // Draw separator line
                    _gui.DrawRect(_gui.Node(UnitValue.Expand(), 1), new Color(1, 1, 1, 0.1f));
                    _gui.SetTextColor(0xffffff88);
                    // Assigns the body container
                    popup.BodyContainer = _gui.Node()
                        .Margin(_gap)
                        .ToScope();
                    // Draw another separator line
                    _gui.DrawRect(_gui.Node(UnitValue.Expand(), 1), new Color(1, 1, 1, 0.1f));
                    // Assigns the footer container
                    popup.FooterContainer = _gui.Node()
                        .Margin(_gap)
                        .Direction(Axis.Horizontal)
                        .ExpandWidth()
                        .ContentAlignX(1)
                        .Gap(_gap)
                        .ToScope();
                }
            }
        }

        // This API is particularly likely to become something different, or be completely removed.
        // It's meant to solve the problem of getting computed values that are not yet known during
        // this pass, but were known or calculated during the previous pass.
        //
        // For now, this is kind of a temporary hack; don't worry about it, we will have a much better
        // solution for this problem before PanGui launches.
        ref float maxPopupVisibility = ref _gui.GetValue<float>("maxPopupVisibility", Pass.CurrentPass, 0f);
        maxPopupVisibility = Math.Max(popup.Visibility, maxPopupVisibility);
        return popup;
    }

    private float GetPopupVisibility()
    {
        // This API is particularly likely to become something different, or be completely removed.
        // It's meant to solve the problem of getting computed values that are not yet known during
        // this pass, but were known or calculated during the previous pass.
        //
        // For now, this is kind of a temporary hack; don't worry about it, we will have a much better
        // solution for this problem before PanGui launches.
        return _gui.GetValue<float>("maxPopupVisibility", Pass.PreviousPass, 0f);
    }

    private LayoutNode DrawTooltip(Vector2 pivot, string text)
    {
        using (_gui.EnterDataScope("tooltip"))
        {
            // We can position the tooltip layout node
            // precisely, and then offset its content based
            // on its own size, so it always floats to the
            // left of the pivot point and is vertically
            // centered.
            var tooltipPositioner = _gui.Node()
                .PositionRelativeToRoot(pivot)
                .ContentOffsetX(Offset.Percentage(-1))
                .ContentOffsetY(Offset.Percentage(-0.5f));
            using (tooltipPositioner.AppendNode().AlignContent(0.5f).Padding(10, 15).MarginRight(20).Enter())
            {
                var rect = _gui.CurrentNode.Rect;
                var tooltipBgShape = Shape.RectangleRounded(_gui.CurrentNode.Rect.Width, rect.Height, 5)
                                     + Shape.EquilateralTriangle(20, Angle.Turns(0.25f))
                                         .Move(rect.Width * 0.5f, 0);
                _gui.SetTextSize(16);
                _gui.SetTextColor(0xffffffcc);
                _gui.SetZIndex(10);
                _gui.DrawShape(_gui.CurrentNode, tooltipBgShape)
                    .SolidColor(0x00000099)
                    .OuterShadow(0x00000055, new Vector2(5, 8), 5)
                    .InnerShadow(0xffffff22, new Vector2(0, 1), 1.2f);
                _gui.DrawText(text);
            }

            return tooltipPositioner;
        }
    }

    private void DrawDefaultPopupTitleAndFooter(Popup popup, string title)
    {
        using (popup.HeaderContainer.Enter())
        {
            _gui.DrawText(title);
        }

        using (popup.FooterContainer.Enter())
        {
            using (_gui.Node().Enter())
            {
                StyleButton(false);
                _gui.DrawText("Cancel");
                if (_gui.CurrentNode.OnClick())
                    popup.Hide();
            }

            using (_gui.Node().Enter())
            {
                StyleButton(false);
                _gui.DrawText("Confirm");
                if (_gui.CurrentNode.OnClick())
                    popup.Hide();
            }
        }
    }
}
