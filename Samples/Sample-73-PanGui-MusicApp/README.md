# Sample-73-PanGui-MusicApp

This is a comprehensive "real-world" style application demonstration. It showcases a complex Music Production interface with multiple sections, custom styling, and advanced layout techniques.

## Features
- **Complex Layout**: Multi-panel interface with toolbars, sidebars, content areas, and bottom controls.
- **Custom Components**:
    - Instrument controls with ADSR envelopes and effect chains.
    - Pad player for rhythm and sample triggering.
    - Piano roll and modulation wheels.
    - Snapshot management.
- **Advanced Visuals**:
    - Custom gradients and smooth unions between shapes.
    - Font management with font icons (FontAwesome).
    - High-fidelity dark theme.
- **State Management**: Handling a large amount of application state (instruments, tracks, pads, snapshots).
- **Semi-Retained Layout Pattern**: Passing `LayoutNode` objects as arguments to functions for modular UI building.

## Interaction
- Interact with instrument knobs, sliders, and buttons.
- Trigger pads and explore the library.
- Navigate snapshots and play the virtual keyboard.

## Original Code

Code from https://pangui.io/

```cs
using PanGui;
using System.Numerics;

// Style constants
float gap = 8;
float padding = 10;
float borderRadius = 5;
ImFont arial_normal = ImFont.LoadFont("arial");
ImFont arial_bold = ImFont.LoadFont("arialbd");
Icons.IconFont = ImFont.LoadFont("fa-regular-400");

// Open a window and run the GUI
Gui gui = new Gui();
GuiWindow window = new GuiWindow(gui);

window.RunGui(() =>
{
    // Set default font, text color, and size for the window
    gui.SetTextFont(arial_normal);
    gui.SetTextColor(0x747273FF);
    gui.SetTextSize(18);

    // Draw the window background
    gui.DrawSdBackgroundRect().RadialGradientColor(0x292423FF, 0x322D29FF, 0, gui.ScreenRect.MaxDimension);

    DrawTopToolbar();

    // The root layout node that helps us layout all the main sections of the application
    using (gui.Node().Expand().Gap(gap).Margin(gap).FlowDir(Axis.X).Enter())
    {
        // Section for instruments and snapshots on the left
        using (gui.Node().Expand().Gap(gap).Enter())
        {
            // Draw the instrument controls
            DrawInstruments();
        }

        // Section for track list and pads on the right
        using (gui.Node(400, Size.Expand()).Gap(gap).Enter())
        {
            // Draw the library
            using (gui.Node().Expand().Enter())
            {
                DrawLibrary();
            }

            // Draw the pad player
            using (gui.Node().ExpandWidth().Enter())
            {
                DrawPadPlayer(padPlayer);
            }
        }
    }

    // Section for the piano, mod wheel, and additional controls at the bottom
    using (gui.Node().ExpandWidth().Enter())
    {
        // PanGui lets you pass LayoutNodes around as arguments to functions, which enables a sort of
        // semi-retained pattern within the immediate mode pattern  - which is pretty cool!
        // In this case, we give the DrawPianoSection function a LayoutNode to put the piano buttons into.
        LayoutNode pianoButtonsLayoutNode;
        float radius = 90;
        SdShapePos s1 = SdShapePos.Rectangle(gui.CurrentNode.LastChild.OuterRect);
        SdShapePos s2 = SdShapePos.RectangleRounded(gui.CurrentNode.FirstChild.ChildNodes[3].Rect.Expand(30, 0).AddHeight(80), radius);
        SdShapePos pianoBgShape = s1.SmoothUnion(s2, radius);

        // Draw the snapshot controls and prepare layout for piano buttons
        using (gui.Node().FlowDir(Axis.X).Gap(gap).Margin(gap).MarginTop(0).ExpandWidth().Enter())
        {
            // The mask to cut off some of the snapshot buttons with
            SdShapePos mask = pianoBgShape.Expand(10);

            DrawSnapshotButton(snapshots[0], default);
            DrawSnapshotButton(snapshots[1], default);
            DrawSnapshotButton(snapshots[2], mask);
            pianoButtonsLayoutNode = gui.Node().ExpandHeight();
            DrawSnapshotButton(snapshots[3], mask);
            DrawSnapshotButton(snapshots[4], default);
            DrawSnapshotButton(snapshots[5], default);
        }

        using (gui.Node().ExpandWidth().Enter())
        {
            DrawPianoSection(pianoButtonsLayoutNode, pianoBgShape);
        }
    }

    return;
});

LayoutNode DrawSnapshotButton(Snapshot snapshot, SdShapePos mask)
{
    // Draws the horizontal list of buttons below the instruments and volume sliders
    using (gui.Node(Size.Expand(), 60).Gap(10).Enter())
    {
        bool isSelected = snapshot == selectedSnapshot;
        SdShapePos bgShape = SdShapePos.RectangleRounded(gui.CurrentNode.Rect.Expand(-3), borderRadius);

        if (mask != default)
            bgShape = bgShape - mask;

        StyleButton(isSelected, bgShape);

        gui.DrawText(snapshot.Name);

        if (gui.CurrentNode.OnClick())
            selectedSnapshot = snapshot;

        return gui.CurrentNode;
    }
}

void DrawInstruments()
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

    LayoutNode container = gui.Node().Expand().FlowDir(Axis.X).Gap(gap);

    // Draws the horizontal list of instruments, effects and volume sliders
    foreach (ref Instrument instrument in selectedSnapshot.Instruments.AsSpan())
    {
        using (container.AppendNode().Expand().Enter())
        {
            StyleBox();

            float offsetY = container.Rect.Height * 0.5f;
            gui.DrawSdBackgroundRect(borderRadius)
                .RadialGradientColor(new Color(instrument.Color, 0.2f), new Color(instrument.Color, 0), 0, 400, 0, -offsetY)
                .RadialGradientColor(new Color(instrument.Color, 0.2f), new Color(instrument.Color, 0), 0, 200, 0, -offsetY);

            Popup adsrPopup = GetPopup();

            // Draw header
            using (gui.Node().Enter())
            {
                StyleHeader();

                ImRect graphRect = gui.CurrentNode.Rect.AlignRight(100).Padding(8);
                InteractableElement graphInteractable = gui.GetInteractable(graphRect);
                float graphHover = gui.AnimateBool01(graphInteractable.OnHover() || adsrPopup.IsVisible(), 0.2f, Easing.SmoothStep);
                gui.DrawText(instrument.Name);
                gui.DrawRect(graphRect.Expand(1), 4, 0x00000088);

                DrawADSRGraph(graphRect, instrument.adsr, new Color(instrument.Color, 0.4f + graphHover * 0.4f));

                if (graphInteractable.OnClick())
                {
                    adsrPopup.Show();
                }

                if (adsrPopup.IsVisible())
                {
                    DrawDefaultPopupTitleAndFooter(adsrPopup, "Attack, Decay, Sustain, Release");

                    using (adsrPopup.BodyContainer.Enter())
                    {
                        DrawADSRSettings(instrument.adsr);
                    }
                }
            }

            // Fancy audio wave:
            using (gui.Node().ExpandWidth().MarginBottom(padding).Enter())
            {
                gui.DrawBackgroundRect(0x00000044);

                Color color = new Color(instrument.Color, 0.7f);

                for (int n = 0; n < 2; n++)
                {
                    var rect = gui.Node(Size.Expand(), 40).Rect;
                    var count = (int)((rect.Width - 20) / 2);

                    // Draw some random audio wave
                    if (count > 2)
                    {
                        unsafe
                        {
                            // Currently, we create the graph by manually modifying vertex data.
                            // Eventually, there will be a slightly higher level and simpler API
                            // for creating graphs from data.
                            ImVertexQuad* quads = gui.DrawList.AddTriangulatedQuads(count);
                            var r = rect.AlignLeft(2).AlignCenterY(2);
                            var center = rect.Center.Y;
                            var t = MathF.PI - gui.Time.Elapsed * 2 + n * 0.2f;

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
            using (gui.Node().ExpandWidth().Padding(20).Gap(20).Enter())
            {
                foreach (ref var effect in instrument.Effects.AsSpan())
                {
                    DrawEffectButton(ref effect, instrument.Color);
                }
            }

            // Draw Pan
            using (gui.Node().ExpandWidth().AlignContent(0.5f).MarginBottom(20).Gap(10).Enter())
            {
                DrawKnobSlider(0x74B8DBFF, 60, ref instrument.Pan, true);
                gui.DrawText("Pan");
            }

            // Draw sliders
            using (gui.Node().Expand().MarginBottom(40).Gap(5).FlowDir(Axis.X).AlignContent(0.5f).Enter())
            {
                int rulerMargin = 8;
                LayoutNode leftRulerNode = gui.Node(8, Size.Expand()).Margin(0, rulerMargin);
                LayoutNode sliderNode = DrawVolumeSlider(instrument.Color, ref instrument.Volume).Width(26);
                LayoutNode rulerNumbersNode = gui.Node(10, Size.Expand()).ContentAlignX(0.5f).Spacing(Spacing.SpaceBetween).Margin(8, rulerMargin);
                LayoutNode volumnBarNode = gui.Node(26, Size.Expand());
                LayoutNode rightRulerNode = gui.Node(8, Size.Expand()).Margin(0, rulerMargin);

                int rulerLineCount = 15;
                using (rulerNumbersNode.Enter())
                {
                    gui.SetTextSize(13);
                    gui.SetTextColor(0xffffff55);
                    for (int i = 0; i < rulerLineCount; i++)
                        gui.DrawText((i + 1) + "");
                }

                gui.DrawSdRect(volumnBarNode, 6).SolidColor(0x00000088).InnerShadow(0x00000099, 4);
                (float left, float right) volume = GetSimulatedOutputVolume();
                DrawVolumeBar(volumnBarNode.Rect.Padding(6).AlignLeft(5), volume.left * instrument.Volume);
                DrawVolumeBar(volumnBarNode.Rect.Padding(6).AlignRight(5), volume.right * instrument.Volume);
                gui.DrawLinesY(leftRulerNode, rulerLineCount, 0xffffff20, 2);
                gui.DrawLinesY(rightRulerNode, rulerLineCount, 0xffffff20, 2);
            }
        }
    }
}

unsafe void DrawADSRGraph(ImRect graphRect, ADSR adsr, Color color)
{
    Color colGradientTop = new Color(color, color.A * 0.4f);
    Color colGradientBottom = new Color(color, 0);

    graphRect = graphRect.Padding(5).AddY(5);

    float totalWidth = graphRect.Width;
    float totalHeight = graphRect.Height;

    float smoothAttack = gui.Smoothdamp(adsr.Attack);
    float smoothDecay = gui.Smoothdamp(adsr.Decay);
    float smoothSustain = gui.Smoothdamp(adsr.Sustain);
    float smoothRelease = gui.Smoothdamp(adsr.Release);

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
            Vector2 pTo = new(p1.X + (p2.X - p1.X) * (1 - MathF.Pow(1 - t, pow)), p1.Y + (p2.Y - p1.Y) * MathF.Pow(t, pow));

            gui.DrawLine(pFrom, pTo, color, 2);

            // Currently, we create the graph by manually modifying vertex data.
            // Eventually, there will be a slightly higher level and simpler API
            // for creating graphs from data.
            ImVertexQuad* quad = gui.DrawList.AddTriangulatedQuad();
            quad->SetColor(color);
            quad->TL.Pos = pFrom;
            quad->BL.Pos = new Vector2(pFrom.X, pStart.Y);
            quad->TR.Pos = pTo;
            quad->BR.Pos = new Vector2(pTo.X, pStart.Y);
            quad->TL.Col = Color.Lerp(colGradientTop, colGradientBottom, 1 - (pStart.Y - pFrom.Y) / totalHeight);
            quad->TR.Col = Color.Lerp(colGradientTop, colGradientBottom, 1 - (pStart.Y - pTo.Y) / totalHeight);
            quad->BL.Col = colGradientBottom;
            quad->BR.Col = colGradientBottom;

            pFrom = pTo;
        }
    }
}

unsafe void DrawADSRSettings(ADSR adsr)
{
    // Draws the Attack, Decay, Sustain, Release settings for the selected instrument.
    ImRect graphRect = gui.Node(700, 300).MarginBottom(gap).Rect;

    gui.DrawLinesX(graphRect.HPadding(20), (int)(graphRect.Width / 20), 0xffffff11);
    gui.DrawLinesY(graphRect.VPadding(20), (int)(graphRect.Height / 20), 0xffffff11);
    gui.DrawSdRect(graphRect, borderRadius)
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
    SdShape circle = SdShape.Circle(6);

    for (int i = 0; i < points.Length - 1; i++)
    {
        Color color = colors[i];
        Color colGradientTop = new Color(color, 0.4f);
        Color colGradientBottom = new Color(color, 0);

        gui.PushZIndex(gui.State.ZIndex + 1);
        gui.DrawSdShape(points[i + 1], circle).SolidColor(color);
        gui.PopZIndex();

        float pow = curvePowers[i];
        Vector2 p1 = points[i];
        Vector2 p2 = points[i + 1];
        int nResolution = curveResolutions[i];

        Vector2 pFrom = p1;
        for (int j = 1; j < nResolution; j++)
        {
            float t = j / (nResolution - 1f);
            Vector2 pTo = new(p1.X + (p2.X - p1.X) * (1 - MathF.Pow(1 - t, pow)), p1.Y + (p2.Y - p1.Y) * MathF.Pow(t, pow));
            gui.DrawLine(pFrom, pTo, color, 2);
            ImVertexQuad* quad = gui.DrawList.AddTriangulatedQuad();
            quad->SetColor(color);
            quad->TL.Pos = pFrom;
            quad->BL.Pos = new Vector2(pFrom.X, pStart.Y);
            quad->TR.Pos = pTo;
            quad->BR.Pos = new Vector2(pTo.X, pStart.Y);
            quad->TL.Col = Color.Lerp(colGradientTop, colGradientBottom, 1 - (pStart.Y - pFrom.Y) / totalHeight);
            quad->TR.Col = Color.Lerp(colGradientTop, colGradientBottom, 1 - (pStart.Y - pTo.Y) / totalHeight);
            quad->BL.Col = colGradientBottom;
            quad->BR.Col = colGradientBottom;
            pFrom = pTo;
        }
    }

    var sustainDragRect = new ImRect(pDecay.X - 10, pDecay.Y - 10, (pSustain.X - pDecay.X) + 20, 20);
    InteractableElement sustainHandle = gui.GetInteractable(sustainDragRect);
    var attackHandle = gui.GetInteractable(points[1], SdShape.Circle(20));
    var decayHandle = gui.GetInteractable(points[2], SdShape.Circle(20));
    var releaseHandle = gui.GetInteractable(points[4], SdShape.Circle(20));

    if (attackHandle.OnHover() || attackHandle.OnHold()) gui.DrawCircle(points[1], 15, 0xffffff22);
    if (decayHandle.OnHover() || decayHandle.OnHold()) gui.DrawCircle(points[2], 15, 0xffffff22);
    if (sustainHandle.OnHover() || sustainHandle.OnHold()) gui.DrawRect(sustainDragRect, 15, 0xffffff22);
    if (releaseHandle.OnHover() || releaseHandle.OnHold()) gui.DrawCircle(points[4], 15, 0xffffff22);

    var mouseDelta = gui.Input.MouseDelta / graphRect.Size;

    if (attackHandle.OnHold())
        adsr.Attack = Math.Clamp(adsr.Attack + mouseDelta.X, 0, 1);

    if (decayHandle.OnHold())
        adsr.Decay = Math.Clamp(adsr.Decay + mouseDelta.X, 0, 1);

    if (sustainHandle.OnHold())
        adsr.Sustain = 1 - Math.Clamp((1 - adsr.Sustain) + mouseDelta.Y, 0, 1);

    if (releaseHandle.OnHold())
        adsr.Release = Math.Clamp(adsr.Release + mouseDelta.X, 0, 1);

    using (gui.Node().ExpandWidth().Spacing(Spacing.SpaceEvenly).FlowDir(Axis.X).Enter())
    {
        DrawKnobSliderWithLabel(colors[0], ref adsr.Attack, "Attack", $"{adsr.Attack * 1000:0} ms");
        DrawKnobSliderWithLabel(colors[1], ref adsr.Decay, "Decay", $"{adsr.Decay * 1000:0} ms");
        DrawKnobSliderWithLabel(colors[2], ref adsr.Sustain, "Sustain", $"{adsr.Sustain * 100:0} %");
        DrawKnobSliderWithLabel(colors[3], ref adsr.Release, "Release", $"{adsr.Release * 1000:0} ms");
    }
}

void DrawEffectSettings(Popup popup, ref Effect effect)
{
    gui.CurrentNode
        .Margin(gap)
        .Gap(gap)
        .FlowDir(Axis.X);

    using (gui.Node().Padding(gap).AlignContent(0.5f).Enter())
    {
        StyleInnerPopupBox("Settings");

        using (gui.Node().FlowDir(Axis.X).Wrap(3).Enter())
        {
            foreach (ref var knob in effect.KnobValues.AsSpan())
            {
                using (gui.Node().Padding(gap * 2).Gap(gap * 2).AlignContent(0.5f).Enter())
                {
                    gui.DrawText("Low cut");
                    DrawKnobSlider(0xE6B455FF, 70, ref knob);
                    gui.DrawText(knob.ToString("0.0"));
                }
            }
        }
    }

    using (gui.Node().Padding(padding).ExpandHeight().AlignContent(0.5f).Enter())
    {
        StyleInnerPopupBox("Output");

        using (gui.Node().ExpandHeight().Padding(gap * 2, gap).FlowDir(Axis.X).Gap(40).Enter())
        {
            using (gui.Node().Padding(padding).ExpandHeight().Gap(gap).AlignContent(0.5f).Enter())
            {
                gui.DrawText("Dry");
                DrawVolumeSlider(0xE6B556FF, ref effect.Dry).Width(30);
                gui.DrawText(effect.Dry.ToString("0.0"));
            }

            using (gui.Node().Padding(padding).ExpandHeight().Gap(gap).AlignContent(0.5f).Enter())
            {
                gui.DrawText("Wet");
                DrawVolumeSlider(0xE6B556FF, ref effect.Wet).Width(30);
                gui.DrawText(effect.Wet.ToString("0.0"));
            }
        }
    }

    void StyleInnerPopupBox(string title)
    {
        gui.DrawSdBackgroundRect(borderRadius).SolidColor(0xffffff11);

        using (gui.Node().ExpandWidth().AlignChildrenCenter().Margin(gap).MarginBottom(gap).Enter())
        {
            gui.DrawText(title);
        }

        var line = gui.Node(Size.Expand(), 2).MarginBottom(gap);
        gui.DrawRect(line.Rect.Expand(gap, 0), 0x00000044);
    }
}

void DrawPianoSection(LayoutNode pianoButtonsContainer, SdShapePos pianoBgShape)
{
    float popupVisibility = GetPopupVisibility();

    // Draw the piano buttons always on top
    gui.SetZIndex(1000);
    gui.SetTransform(Matrix3x2.CreateScale(1, 1 + popupVisibility * 0.05f, gui.CurrentNode.Rect.BottomCenter));

    gui.DrawSdShape(pianoBgShape.Pos, pianoBgShape.Shape)
        .SolidColor(Color.Lerp(0x151313FF, 0x292423FF, popupVisibility))
        .InnerShadow(new Color(0x00000088, 1 - popupVisibility), new Vector2(0, 40), 80, -40)
        .InnerShadow(new Color(0x00000088, 1 - popupVisibility), new Vector2(0, 10), 20, -10)
        .SolidColor(new Color(0x00000044, popupVisibility))
        .InnerShadow(new Color(0xffffff11, popupVisibility), new Vector2(0, 40), 80, -40)
        .OuterShadow(new Color(0x000000ff, popupVisibility), 20)
        .Stroke(0x000000ff, 1, 0);

    // Here we jump into the layout node we were passed, which was created separately.
    using (pianoButtonsContainer.FlowDir(Axis.X).Enter())
    {
        float t = 0.5f;
        DrawKnobSlider(0x74B8DBFF, 60, ref t).Margin(15);
        DrawKnobSlider(0xE5B657FF, 60, ref t).Margin(15);
        DrawKnobSlider(0x3DBF6BFF, 60, ref t).Margin(15);
    }

    using (gui.Node().ExpandWidth().FlowDir(Axis.X).Height(260).MarginTop(20).Padding(padding).Gap(gap).Enter())
    {
        // Draw mod wheel
        {
            var leftRuler = gui.Node(10, Size.Expand()).Spacing(Spacing.SpaceBetween);
            var wheel = gui.Node(60, Size.Expand()).Rect;
            var rightRuler = gui.Node(10, Size.Expand()).Spacing(Spacing.SpaceBetween);
            var handle = wheel.Padding(0, 5).AlignTop(30).AddY(modWheel * (wheel.Height - 36));
            var shadowShape = SdShape.Circle(300).MoveX(-220) * SdShape.Rectangle(400, wheel.Height).MoveX(180);
            var handleShape = SdShape.RectangleRounded(wheel.Width, 20, 5);

            // shadow
            gui.DrawSdShape(wheel.Center, shadowShape)
                .DiamondGradientColor(0x000000cc, 0x00000000, 50);

            // wheel
            gui.DrawSdRect(wheel, 5)
                .OuterShadow(0xffffff22, new Vector2(0, 3), 3)
                .SolidColor(0xffffff11)
                .Stroke(0x000000ff, 3);

            // handle
            gui.DrawSdRect(handle)
                .LinearGradientColor(0x00000088, 0xffffff11, Angle.Turns(-0.25f), 0.54f)
                .InnerShadow(0x000000ff, new Vector2(0, 1), 1, 1)
                .InnerShadow(0xffffff22, new Vector2(0, -1), 1, 1);

            // lighting
            gui.DrawSdRect(wheel, 5)
                .InnerShadow(0xffffff11, new Vector2(8, 0), 5f, -5)                                 // left light
                .InnerShadow(0x000000cc, new Vector2(-8, 0), 5f, -5)                                // right shadow
                .InnerShadow(0x000000cc, new Vector2(0, 10), 10f, -10)                              // top shadow
                .LinearGradientColor(0x000000ee, 0x00000000, MathF.PI * 0.5f, 0.6f, offsetY: -0.5f) // 3d effect
                .LinearGradientColor(0x00000000, 0x00000044, MathF.PI * 0.5f, 0.8f, offsetY: 0.8f); // 3d effect

            // Draw left and right rulers:
            {
                var numLines = 20;
                for (int i = 0; i < numLines; i++)
                {
                    float f = i / (float)numLines;
                    var left = leftRuler.AppendNode(Size.Expand(), 2);
                    var right = rightRuler.AppendNode(Size.Expand(), 2);
                    float brightness = gui.Smoothdamp(f >= modWheel ? 1 : 0f, 10);
                    Color color = Color.Lerp(0x000000ff, 0xE6B455FF, brightness);
                    float outerShadow = brightness * 8;
                    gui.DrawSdRect(left.Rect, 1).SolidColor(color).OuterShadow(new Color(color, 0.2f), outerShadow, 2);
                    gui.DrawSdRect(right.Rect, 1).SolidColor(color).OuterShadow(new Color(color, 0.2f), outerShadow, 2);
                }
            }

            InteractableElement e = gui.GetInteractable(wheel);
            if (e.OnHold(out var args))
            {
                var slideArea = wheel.Padding(0, handle.Height * 0.5f);
                modWheel = Math.Clamp(gui.Input.MousePosition.Y - slideArea.Top, 0, slideArea.Height) / slideArea.Height;
            }
        }

        gui.Node(gap);

        using (gui.Node().Expand().Gap(4).Enter())
        {
            float numKeys = 7 * 12f;

            foreach (var instrument in selectedSnapshot.Instruments)
            {
                Color color = instrument.Color;
                float keyStart = gui.Smoothdamp((1f / numKeys) * instrument.KeyStart);
                float keyLength = gui.Smoothdamp((1f / numKeys) * instrument.KeyLength);

                ImRect rangeRect = gui.Node(Size.Expand(), 14).Rect;
                rangeRect.X += keyStart * rangeRect.Width;
                rangeRect.Width *= keyLength;

                // bg
                gui.DrawSdRect(rangeRect, 7)
                    .SolidColor(0x00000088)
                    .OuterShadow(0xffffff22, new Vector2(0, 1), 2);

                // glow
                gui.DrawSdRect(rangeRect.Padding(2), 5)
                    .SolidColor(color)
                    .InnerShadow(0x000000aa, new Vector2(0, 0), 8.2f, -2);
            }

            // Draw Piano
            ImRect piano = gui.Node().MarginTop(10).Expand().Rect;
            DrawKeyboard(piano, keyPressures);
        }
    }
}

void DrawPadPlayer(PadPlayer padPlayer)
{
    gui.CurrentNode.Padding(padding).Gap(gap);

    StyleBox();

    using (gui.Node().Gap(gap).FlowDir(Axis.X).ExpandWidth().Enter())
    {
        using (gui.Node().ExpandWidth().Gap(gap).Enter())
        {
            gui.SetTextSize(22);

            using (gui.Node().ExpandWidth().FlowDir(Axis.X).Gap(gap).Wrap(4).Enter())
            {
                for (int i = 0; i < padPlayer.Pads.Length; i++)
                {
                    using (gui.Node().ExpandWidth().Height(Size.Ratio(1)).Enter())
                    {
                        StyleButton(i == padPlayer.SelectedPad);
                        gui.DrawText(padPlayer.Pads[i]);

                        if (gui.CurrentNode.OnClick())
                        {
                            padPlayer.SelectedPad = i;
                        }
                    }
                }
            }
        }

        using (gui.Node().ExpandHeight().Width(80).MarginBottom(20).AlignContent(0.5f).Gap(20).Enter())
        {
            DrawVolumeSlider(0xC2984BFF, ref padPlayer.Volume).Width(25);
            gui.SetTextSize(22);
            gui.SetTextFont(arial_bold);
            gui.DrawText("-5 dB");
        }
    }

    using (gui.Node().FlowDir(Axis.X).Gap(gap).AlignContent(0.5f).Enter())
    {
        DrawKnobSliderWithLabel(0xE5B458FF, ref padPlayer.Brightness, "Brightness", $"{padPlayer.Brightness * 100:0}%");
        gui.Node(30);
        DrawKnobSliderWithLabel(0xE5B458FF, ref padPlayer.Shimmer, "Shimmer", $"{padPlayer.Shimmer * 100:0}%");
    }
}

void DrawKnobSliderWithLabel(Color color, ref float value, string label, string valueLabel)
{
    using (gui.Node().FlowDir(Axis.X).AlignContent(0.5f).Gap(gap).Enter())
    {
        DrawKnobSlider(color, 70, ref value);

        using (gui.Node().Gap(10).Enter())
        {
            gui.DrawText(label, Color.White);
            gui.DrawText(valueLabel, (Color)0xffffff88);
        }

        Color col = 0xF18B46FF;
    }
}

LayoutNode DrawKnobSlider(Color color, Size size, ref float value, bool isPan = false)
{
    ref float t = ref gui.Smoothdamp(value);
    LayoutNode node = gui.Node(size);
    float angleOffset = 0.1f;
    float arcLength = Angle.Turns(1 - angleOffset * 2);
    float arcStart = Angle.Turns(0.25f + angleOffset);

    if (node.IsVisible())
    {
        Vector2 center = node.Rect.Center;
        float arcThickness = 3;
        float arcRadius = node.Rect.Width * 0.5f - arcThickness;
        float knobRadius = node.Rect.Width * 0.5f - arcThickness - 8;

        SdShape knobShape;
        SdShape arcSliceShape;

        if (isPan)
        {
            knobShape = SdShape.Circle(knobRadius - 4);
            float panLength = Math.Abs(0.5f - t);
            float len = arcLength * panLength;
            float rot = len * 0.5f - Angle.Turns(0.25f);

            if (t < 0.5f)
            {
                rot -= len;
            }

            arcSliceShape = SdShape.Pie(arcRadius * 2, len)
                .Rotate(rot);

            arcSliceShape *= (knobShape + 10);
            arcSliceShape -= knobShape;
            arcSliceShape -= 3;
        }
        else
        {
            float rotation = arcLength * t * 0.5f + MathF.PI * 0.5f + Angle.Turns(angleOffset);

            knobShape = SdShape.Union(SdShape.Circle(knobRadius - 4), SdShape.EquilateralTriangle(knobRadius).MoveY(-10), smoothness: 10).Rotate(arcStart + arcLength * value + MathF.PI * 0.5f);
            arcSliceShape = SdShape.Pie(arcRadius * 2, arcLength * t).Rotate(rotation);
            arcSliceShape *= (knobShape + 10);
            arcSliceShape -= knobShape;
            arcSliceShape -= 3;
        }

        // Bg
        gui.DrawSdShape(center, knobShape + 10)
            .InnerShadow(0x00000022, new Vector2(0, 10), 20, -10)
            .InnerShadow(0x000000ff, new Vector2(0, -10), 20, -10)
            .SolidColor(0x00000088);

        gui.DrawSdShape(center, arcSliceShape)
            .SolidColor(color);

        //Handle
        gui.DrawSdShape(center, knobShape)
            .LinearGradientColor(0xB9B1AFFF, 0x484443FF, Angle.Turns(-0.25f))
            .OuterShadow(0x000000cc, new Vector2(0, knobRadius * 0.5f), knobRadius * 1.5f, knobRadius * 0.2f)
            .InnerShadow(0xffffff22, new Vector2(0, 1), 1, 1);
    }

    InteractableElement e = gui.GetInteractable(node.Rect);
    if (e.OnHold())
    {
        value += -gui.Input.MouseDelta.Y / 1000;
        value += gui.Input.MouseDelta.X / 1000;
        value = Math.Clamp(value, 0, 1f);

        t = value;
    }

    return node;
}

void DrawLibrary()
{
    StyleBox();

    using (gui.Node().Enter())
    {
        StyleHeader();
        gui.DrawText("Library");
    }

    using (gui.Node().Expand().Margin(1).Enter())
    {
        gui.ScrollY(0x00000055, 0xffffff22);

        foreach (string title in trackLibrary)
        {
            bool isSelected = title == selectedTrack;
            using (gui.Node().ExpandWidth().Padding(padding, 10).Enter())
            {
                if (isSelected)
                {
                    gui.DrawBackgroundRect(0x00000077);
                    gui.SetTextColor(0xffffff99);
                }

                gui.DrawText(title);

                if (gui.CurrentNode.OnClick())
                {
                    selectedTrack = title;
                }
            }
        }
    }
}

void DrawTopToolbar()
{
    using (gui.Node()
        .ExpandWidth()
        .AlignContent(0.5f)
        .Padding(gap)
        .Gap(20)
        .FlowDir(Axis.X)
        .Enter())
    {
        gui.SetZIndex(1000);

        gui.DrawSdBackgroundRect()
            .SolidColor(Color.Lerp(0x252323FF, 0x393433FF, 1))
            .SolidColor(new Color(0x00000044, 1))
            .InnerShadow(new Color(0xffffff11, 1), new Vector2(0, 40), 80, -40)
            .OuterShadow(new Color(0x000000ff, 1), 10)
            .Stroke(0x000000ff, 1, 0);

        gui.DrawText("Some Logo", Color.White).MarginLeft(10);

        // Spacer
        gui.Node().Expand();

        {
            DrawToolbarButtonTemplate(out LayoutNodeScope title, out LayoutNodeScope subTitle, out LayoutNodeScope btn);
            using (title.Enter()) gui.DrawText("Play in");
            using (subTitle.Enter()) gui.DrawText("per patch");
            using (btn.Enter()) gui.DrawText("C");
        }

        {
            DrawToolbarButtonTemplate(out LayoutNodeScope title, out LayoutNodeScope subTitle, out LayoutNodeScope btn);
            using (title.Enter()) gui.DrawText("Hear in");
            using (subTitle.Enter()) gui.DrawText("per patch");
            using (btn.Enter()) gui.DrawText("D");
        }

        {
            DrawToolbarButtonTemplate(out LayoutNodeScope title, out LayoutNodeScope subTitle, out LayoutNodeScope btn);
            using (title.Enter()) gui.DrawText("Tempo");
            using (btn.Enter()) gui.DrawText("122.00 BPM");
        }

        // Spacer
        gui.Node().Expand();

        // Menu button placeholder
        gui.DrawIcon(Icons.MenuBurger, 30).MarginRight(10);
    }
}

void DrawEffectButton(ref Effect effect, in Color instrumentColor)
{
    LayoutNode container = gui.Node().ExpandWidth().FlowDir(Axis.X);
    LayoutNode toggleEffectNode = container.AppendNode(Size.Ratio(1), Size.Expand()).AlignContent(0.5f); // Expand height, and make width as wide as it is tall.
    LayoutNode textNode = container.AppendNode().ExpandWidth()
        .AlignContent(0, 0.5f)
        .Gap(7)
        .PaddingY(10)
        .PaddingLeft(padding);

    float onOffNodeHover = gui.AnimateBool01(toggleEffectNode.OnHover(), 0.2f, Easing.EaseInOutSine);
    float textNodeHover = gui.AnimateBool01(textNode.OnHover(), 0.2f, Easing.EaseInOutSine);

    textNode.ContentOffsetX(textNodeHover * 10);

    Popup popup = GetPopup();

    // We could also use an Icon. This is just to showcase the shapes API a little.
    // Angle is a utility struct that returns a float in radians.
    SdShape onIconShape = SdShape.Arc(13, 2, Angle.Turns(-0.1f), Angle.Turns(0.7f))
                        + SdShape.RectangleRounded(4, 16, 2).MoveY(-10);

    float onBrightness = gui.AnimateBool01(effect.IsOn, 0.2f, Easing.EaseInOutSine);

    Color col1 = Color.Lerp(0x00000077, instrumentColor, onBrightness);
    Color col2 = Color.Lerp(0x00000044, instrumentColor, onBrightness);

    // bg
    gui.DrawSdRect(container.Rect, 10)
        .LinearGradientColor(0xffffff44, 0xffffff11, Angle.Turns(-0.15f))
        .SolidColor(new Color(instrumentColor, textNodeHover * 0.1f))
        .OuterShadow(0x00000055, new Vector2(0, 1), 5);

    // icon bg
    gui.DrawSdRect(toggleEffectNode.Rect, 10, 0, 0, 10)
        .LinearGradientColor(col1, col2)
        .InnerShadow(0xffffff22, new Vector2(0, 0), 1, 0);

    // icon
    using (toggleEffectNode.Enter())
    {
        Color iconColor = effect.IsOn ? 0xffffffff : Color.Lerp(0xffffff33, 0xffffffff, onOffNodeHover);
        gui.SetTextColor(iconColor);
        gui.DrawIcon(Icons.PowerOff, 35);
    }

    using (textNode.Enter())
    {
        // This will clip the contents of the node to the node's size, so overflow content is cut off.
        gui.ClipContent();
        gui.DrawText(effect.Type, 0xffffff55, 16);
        gui.DrawText(effect.Name, 0xffffff88, 20);
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

void StyleHeader()
{
    gui.CurrentNode
        .Height(50)
        .Spacing(Spacing.SpaceBetween)
        .AlignContent(0, 0.5f)
        .ExpandWidth()
        .FlowDir(Axis.X)
        .Padding(20, 0);

    gui.SetTextColor(0xDCF2F2FF);

    gui.DrawSdBackgroundRect()
        .SolidColor(0xffffff11)
        .OuterShadow(0x000000ff, 2);
}

void StyleBox()
{
    var bgShape = SdShape.RectangleRounded(gui.CurrentNode.Rect.Width, gui.CurrentNode.Rect.Height, borderRadius);

    gui.DrawSdShape(gui.CurrentNode.Rect.Center, bgShape)
        .OuterShadow(0x00000088, new Vector2(0, 0), 80, 20);

    gui.DrawSdShape(gui.CurrentNode.Rect.Center, bgShape)
        .RadialGradientColor(0xffffff08, 0xffffff05, 0, gui.CurrentNode.Rect.MaxDimension, offsetY: gui.CurrentNode.Rect.Height * 0.5f)
        .InnerShadow(0xffffff22, 2);
}

void StyleButton(bool on, SdShapePos bgShape)
{
    ImRect rect = gui.CurrentNode
        .Padding(15)
        .MinWidth(Size.Ratio(1))
        .AlignContent(0.5f)
        .Rect;

    InteractableElement e = gui.CurrentNode.GetInteractable();
    float tOn = gui.AnimateBool01(on, 0.2f, Easing.EaseInOutSine);
    float tHover = e.OnHover() ? 1 : 0;
    float tDown = gui.AnimateBool01(e.OnHold(), 0.1f, Easing.EaseInOutSine);
    float tUp = 1 - tDown;

    gui.DrawSdShape(bgShape)
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
    gui.SetTextColor(textColor);
}

void StyleButton(bool on)
{
    var rect = gui.CurrentNode.Rect;
    SdShapePos bgShape = SdShapePos.RectangleRounded(rect.Expand(-3), borderRadius);
    StyleButton(on, bgShape);
}

void DrawVolumeBar(ImRect r, float t)
{
    Color green = 0x39BE69FF;
    Color yellow = 0xF0A948FF;
    Color red = 0xF03E3EFF;

    // Adjust the method of drawing gradients to support vertical gradients
    VertexPlane q = gui.DrawList.AddTriangulatedPlane(2, 4);
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

LayoutNode DrawVolumeSlider(Color color, ref float volume, float min = 0, float max = 1)
{
    ref float animatedVolume = ref gui.GetFloat(volume);
    animatedVolume = ImMath.Lerp(animatedVolume, volume, gui.Time.DeltaTime * 10);

    float tHeight = Math.Clamp((animatedVolume - min) / (max - min), 0, 1);
    LayoutNode node = gui.Node().ExpandWidth().ExpandHeight();
    SdShape bgShape = SdShape.RectangleRounded(node.Rect.Width, node.Rect.Height, node.Rect.Width * 0.4f);
    ImRect laneRect = node.Rect.Padding(0, 10).AlignCenterX(8);
    ImRect handleRect = node.Rect.SetHeight(35).SetY(laneRect.Y + laneRect.Height * (1 - tHeight) - 20);
    InteractableElement sliderInteractable = gui.GetInteractable(laneRect);
    InteractableElement handleInteractable = gui.GetInteractable(handleRect);

    float tFocus = gui.AnimateBool01(handleInteractable.OnHover() || handleInteractable.OnHold(), duration: 0.2f, Easing.EaseInOutSine);
    color = Color.Lerp(color, Color.White, tFocus * 0.2f);

    // Bg
    gui.DrawSdShape(node, bgShape)
        .LinearGradientColor(0x00000044, 0x00000044);

    // Lane
    gui.DrawSdRect(laneRect.Expand(1), 4)
        .SolidColor(0x00000088)
        .OuterShadow(0xffffff22, new Vector2(0, 1), 2);

    // Lane glow
    gui.DrawSdRect(laneRect.AlignBottom(laneRect.Height * tHeight), 4)
        .SolidColor(color)
        .InnerShadow(0x000000aa, new Vector2(0, 0), 8.2f, -2);

    // Handle
    gui.DrawSdRect(handleRect, 3)
        .LinearGradientColor(0xB9B1AFFF, 0x484443FF, Angle.Turns(-0.25f))
        .OuterShadow(0x000000ff, new Vector2(0, 10), 30, 10)
        .InnerShadow(0xffffff22, new Vector2(0, 1), 1, 1);

    // Handle whole
    gui.DrawSdRect(handleRect.AlignCenterY(8))
        .OuterShadow(0xffffff22, new Vector2(0, 2), 1)
        .LinearGradientColor(0xB9B1AFFF, 0x484443FF, Angle.Turns(+0.25f));

    // Input
    if (handleInteractable.OnHold() || sliderInteractable.OnHold())
    {
        volume -= gui.Input.MouseDelta.Y / laneRect.Height * (max - min);
        volume = Math.Clamp(volume, min, max);
        animatedVolume = volume;
    }

    if (sliderInteractable.OnPointerDown())
    {
        var deltaY = gui.Input.MousePosition.Y - node.Rect.TL.Y;
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

LayoutNode DrawToolbarButtonTemplate(out LayoutNodeScope titleText, out LayoutNodeScope subTitleText, out LayoutNodeScope buttonContents)
{
    using (gui.Node().FlowDir(Axis.X).Gap(gap).AlignContent(0.5f).Enter())
    {
        using (gui.Node().AlignContent(0, 0.5f).Gap(5).Enter())
        {
            titleText = gui.Node().ToScope();

            using (subTitleText = gui.Node().Enter())
            {
                gui.SetTextSize(15);
                gui.SetTextColor(0xffffff44);
            }
        }

        using (buttonContents = gui.Node().Enter())
        {
            StyleButton(false);
        }

        return gui.CurrentNode;
    }
}

void DrawKeyboard(ImRect piano, float[] keyPressure)
{
    int numOctaves = keyPressure.Length / 12;
    int numMajorKeys = numOctaves * 7;
    int numMinorKeys = numOctaves * 5;
    ImRect majorKeyRect = piano.AlignLeft(piano.Width / numMajorKeys);

    float minorKeyWidth = majorKeyRect.Width * 0.6f;
    ImRect minorKeyRect = piano.AlignLeft(minorKeyWidth);
    minorKeyRect.X += majorKeyRect.Width * 0.5f + (majorKeyRect.Width - minorKeyWidth) * 0.5f;
    minorKeyRect.Height = majorKeyRect.Height * 0.55f;

    bool isPressingPiano = gui.GetInteractable(piano).IgnorePropagation().OnHold(out var args);

    // Draw major keys
    for (int i = 0; i < numMajorKeys; i++)
    {
        ref float pressure = ref keyPressure[i];

        var darkColor = Color.Lerp(0x00000044, 0x00000088, pressure);

        gui.DrawSdRect(majorKeyRect, 5)
            .SolidColor(0xffffffff)
            .InnerShadow(0x000000ff, 0.1f, 1)
            .LinearGradientColor(0x00000000, darkColor, Angle.Turns(-0.25f), 0.5f);

        // Simulate key release
        pressure = ImMath.Lerp(pressure, 0, gui.Time.DeltaTime * 5);
        if (gui.GetInteractable(majorKeyRect).OnHover() && isPressingPiano)
            pressure = 1;

        majorKeyRect.X += majorKeyRect.Width;
    }

    // Draw minor keys
    for (int i = 0; i < numMinorKeys; i++)
    {
        ref float pressure = ref keyPressure[i + numMajorKeys];

        gui.DrawSdRect(minorKeyRect, 0, 0, 3, 3)
            .SolidColor(Color.Lerp(0x2A2A31FF, 0x757984FF, pressure))
            .InnerShadow(0x00000088, 1f, 2)
            .InnerShadow(0xffffff44, new Vector2(2, 0), 1f);

        // Simulate key release
        pressure = ImMath.Lerp(pressure, 0, gui.Time.DeltaTime * 5);
        if (gui.GetInteractable(minorKeyRect).OnHover() && isPressingPiano)
            pressure = 1;

        minorKeyRect.X += majorKeyRect.Width;
        if (i % 5 == 1 || i % 5 == 4)
            minorKeyRect.X += majorKeyRect.Width;
    }
}
```

Utilities.cs

```cs
using PanGui;
using System.Numerics;

static class Icons
{
    public static ImFont IconFont;

    public const char MenuBurger = '\uf0c9';
    public const char ChartSimpleHorizontal = '\ue474';
    public const char FloppyDisk = '\uf0c7';
    public const char LocationCrosshairSlash = '\uf603';
    public const char LocationSlash = '\uf603';
    public const char NavIcon = '\uf0c9';
    public const char PianoKeyboard = '\uf8d5';
    public const char PlusSquare = '\uf0fe';
    public const char PowerOff = '\uf011';
    public const char Save = '\uf0c7';
    public const char SlidersVSquare = '\uf3f2';
    public const char SquarePlus = '\uf0fe';
    public const char SquareSlidersVertical = '\uf3f2';
    public const char Waveform = '\uf8f1';

    public static LayoutNode DrawIcon(this Gui gui, char icon, float size = 40)
    {
        return gui.DrawGlyph(IconFont, icon, size);
    }
}

struct Popup
{
    public AnimationFloat Visibility;
    public LayoutNodeScope HeaderContainer;
    public LayoutNodeScope BodyContainer;
    public LayoutNodeScope FooterContainer;

    public void Show()
    {
        this.Visibility.AnimateTo(1, 0.3f, Easing.SmoothStep);
    }

    public void Hide()
    {
        this.Visibility.AnimateTo(0, 0.3f, Easing.SmoothStep);
    }

    public bool IsVisible()
    {
        return this.Visibility > 0.001f;
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
Popup GetPopup()
{
    var popup = new Popup()
    {
        Visibility = gui.GetAnimationFloat()
    };

    float tVisibility01 = popup.Visibility.GetValue();

    // Calculate popup and background scaling for animation effects
    Vector2 scalePopupIn = Vector2.Lerp(Vector2.One, new Vector2(0.9f, 0.9f), 1 - tVisibility01);
    Vector2 scaleBgOut = Vector2.Lerp(Vector2.One, new Vector2(0.95f, 0.95f), tVisibility01);

    if (popup.IsVisible())
    {
        // If you're familiar with HTMl / CSS you can think of this layout node as being position:fixed;
        // We will make that API nicer in the near future! It has all the bells and whistles to be far more capable than HTML/CSS, it's just about making the API nicer.
        using (gui.Node()
            .Expand()
            .SetParent(gui.Systems.LayoutSystem.RootNode)
            .PositionRelativeTo(gui.Systems.LayoutSystem.RootNode, 0, 0)
            .AlignContent(0.5f, 0.3f).Enter())
        {
            // Resetting the state to 0, is sort of like a "reset" for the GUI state. In a way it jumps us back to the main loop of the GUI.
            gui.SetState(0);

            // Data scopes are explained further down on the website. Go check it out!
            gui.SetDataScope("popup");

            // This makes the popup appear on top of everything else.
            gui.SetZIndex(100);

            // Draw semi-transparent background for popup
            gui.DrawRect(gui.ScreenRect, new Color(0, 0, 0, 0.8f * tVisibility01));

            // Allow popup to be closed by clicking outside its area
            if (gui.CurrentNode.OnClick())
                popup.Hide();

            // The default size for a layout node is "Fit", so the inner popup container will be the size of its content
            using (gui.Node().Enter())
            {
                gui.CurrentNode.PreventAllInputPropagation();

                // Animate the popup.
                gui.SetOpacity(tVisibility01);

                gui.SetTransform(Matrix3x2.CreateScale(scalePopupIn, gui.ScreenRect.Center));
                gui.DrawSdBackgroundRect(radius: borderRadius)
                    .RadialGradientColor(0x292423FF, 0x322D29FF, 0, gui.CurrentNode.Rect.MaxDimension)
                    .InnerShadow(0xffffff11, new Vector2(0, 40), 80, -40)
                    .OuterShadow(0x000000ff, 20)
                    .Stroke(0x000000ff, 1, 0);

                using (gui.Node()
                    .Margin(gap)
                    .FlowDir(Axis.X)
                    .ExpandWidth()
                    .Gap(gap)
                    .MinWidth(Size.Fit)
                    .Margin(20)
                    .AlignContent(0.5f).Enter())
                {
                    gui.SetTextSize(20);
                    gui.SetTextFont(arial_bold);
                    gui.SetTextColor(0xFFFFFFcc);

                    // Assigns the header container
                    popup.HeaderContainer = gui.CurrentNode.ToScope();
                }

                // Draw separator line
                gui.DrawRect(gui.Node(Size.Expand(), 1), new Color(1, 1, 1, 0.1f));
                gui.SetTextColor(0xffffff88);

                // Assigns the body container
                popup.BodyContainer = gui.Node()
                    .Margin(gap)
                    .ToScope();

                // Draw another separator line
                gui.DrawRect(gui.Node(Size.Expand(), 1), new Color(1, 1, 1, 0.1f));

                // Assigns the footer container
                popup.FooterContainer = gui.Node()
                    .Margin(gap)
                    .FlowDir(Axis.X)
                    .ExpandWidth()
                    .ContentAlignX(1)
                    .Gap(gap)
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
    ref float maxPopupVisibility = ref gui.GetValue<float>("maxPopupVisibility", Pass.CurrentPass, 0f);
    maxPopupVisibility = Math.Max(popup.Visibility, maxPopupVisibility);

    return popup;
}

float GetPopupVisibility()
{
    // This API is particularly likely to become something different, or be completely removed.
    // It's meant to solve the problem of getting computed values that are not yet known during
    // this pass, but were known or calculated during the previous pass.
    //
    // For now, this is kind of a temporary hack; don't worry about it, we will have a much better
    // solution for this problem before PanGui launches.
    return gui.GetValue<float>("maxPopupVisibility", Pass.PreviousPass, 0f);
}

LayoutNode DrawTooltip(Vector2 pivot, string text)
{
    using (gui.EnterDataScope("tooltip"))
    {
        // We can position the tooltip layout node
        // precisely, and then offset its content based
        // on its own size, so it always floats to the
        // left of the pivot point and is vertically
        // centered.
        var tooltipPositioner = gui.Node()
            .PositionRelativeToRoot(pivot)
            .ContentOffsetX(Offset.Percentage(-1))
            .ContentOffsetY(Offset.Percentage(-0.5f));

        using (tooltipPositioner.AppendNode().AlignContent(0.5f).Padding(10, 15).MarginRight(20).Enter())
        {
            var rect = gui.CurrentNode.Rect;
            var tooltipBgShape = SdShape.RectangleRounded(gui.CurrentNode.Rect.Width, rect.Height, 5)
                               + SdShape.EquilateralTriangle(20, Angle.Turns(0.25f))
                                        .Move(rect.Width * 0.5f, 0);

            gui.SetTextSize(16);
            gui.SetTextColor(0xffffffcc);
            gui.SetZIndex(10);

            gui.DrawSdShape(gui.CurrentNode, tooltipBgShape)
                .SolidColor(0x00000099)
                .OuterShadow(0x00000055, new Vector2(5, 8), 5)
                .InnerShadow(0xffffff22, new Vector2(0, 1), 1.2f);

            gui.DrawText(text);
        }

        return tooltipPositioner;
    }
}

void DrawDefaultPopupTitleAndFooter(Popup popup, string title)
{
    using (popup.HeaderContainer.Enter())
    {
        gui.DrawText(title);
    }

    using (popup.FooterContainer.Enter())
    {
        using (gui.Node().Enter())
        {
            StyleButton(false);
            gui.DrawText("Cancel");

            if (gui.CurrentNode.OnClick())
                popup.Hide();
        }

        using (gui.Node().Enter())
        {
            StyleButton(false);
            gui.DrawText("Confirm");

            if (gui.CurrentNode.OnClick())
                popup.Hide();
        }
    }
}
```

Data.cs

```cs
using PanGui;
using System.Numerics;

string[] trackLibrary = ["Into the Wild", "Midnight Serenade", "Echoes of Eternity", "Dreamscape", "Whispering Shadows", "Enchanted Journey", "Mystic Melodies", "Celestial Symphony", "Lost in Time", "Harmony of the Spheres", "Serenity Falls", "Eternal Bliss", "Twilight Reverie", "Melancholy Memories", "Dancing Fireflies", "Starry Night", "Whispers in the Wind", "Moonlit Sonata", "Sunset Serenade", "Ocean's Embrace"];
string selectedTrack = "Lost in Time";
float modWheel = 0.5f;
int numOctaves = 8;
float[] keyPressures = new float[12 * numOctaves];
PadPlayer padPlayer = new PadPlayer();
Snapshot[] snapshots = GenerateSnapshots();
Snapshot selectedSnapshot = snapshots[0];

(float left, float right) GetSimulatedOutputVolume()
{
    float maxPianoKeyPressure = gui.Smoothdamp(keyPressures.Max());
    float mono = MathF.Cos(gui.Time.Elapsed * 4.5f);
    float tLeft = mono + MathF.Cos(gui.Time.Elapsed * 7.5f) * 0.5f;
    float tRight = mono + MathF.Cos(gui.Time.Elapsed * 8.5f) * 0.5f;
    float left = maxPianoKeyPressure * (0.8f + tLeft * 0.1f);
    float right = maxPianoKeyPressure * (0.8f + tRight * 0.1f);
    return (left, right);
}

Snapshot[] GenerateSnapshots()
{
    var rnd = new Random(200);

    return [
        new Snapshot { Name = "Snapshot 1", Instruments = GenerateInstruments() },
        new Snapshot { Name = "Snapshot 2", Instruments = GenerateInstruments() },
        new Snapshot { Name = "Snapshot 3", Instruments = GenerateInstruments() },
        new Snapshot { Name = "Snapshot 4", Instruments = GenerateInstruments() },
        new Snapshot { Name = "Snapshot 5", Instruments = GenerateInstruments() },
        new Snapshot { Name = "Snapshot 6", Instruments = GenerateInstruments() },
    ];

    Instrument[] GenerateInstruments() => [
        new Instrument { Name = "Piano", Color = 0xF0A948FF, Volume = (float)rnd.NextDouble(), Effects = GenerateEffects(), KeyStart = rnd.Next(0, 20), KeyLength = rnd.Next(50, 60), adsr = new ADSR(), Pan = 0.5f },
        new Instrument { Name = "Bass", Color = 0x3BBE69FF, Volume = (float)rnd.NextDouble(), Effects = GenerateEffects(), KeyStart = rnd.Next(0, 20), KeyLength = rnd.Next(50, 60), adsr = new ADSR(), Pan = 0.3f },
        new Instrument { Name = "Drums", Color = 0x3961E6FF, Volume = (float)rnd.NextDouble(), Effects = GenerateEffects(), KeyStart = rnd.Next(0, 20), KeyLength = rnd.Next(50, 60), adsr = new ADSR(), Pan = 0.6f },
        new Instrument { Name = "Guitar", Color = 0x8F59E2FF, Volume = (float)rnd.NextDouble(), Effects = GenerateEffects(), KeyStart = rnd.Next(0, 20), KeyLength = rnd.Next(50, 60), adsr = new ADSR(), Pan = 0.5f },
        new Instrument { Name = "Synth", Color = 0xE673C4FF, Volume = (float)rnd.NextDouble(), Effects = GenerateEffects(), KeyStart = rnd.Next(0, 20), KeyLength = rnd.Next(50, 60), adsr = new ADSR(), Pan = 0.5f },
    ];

    Effect[] GenerateEffects() => [
        new Effect { Name = "Reverb", Type = "Midi", IsOn = rnd.NextDouble() > 0.1, KnobValues = [(float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()] },
        new Effect { Name = "Delay", Type = "Midi", IsOn = rnd.NextDouble() > 0.1, KnobValues = [(float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()] },
        new Effect { Name = "Chorus", Type = "Midi", IsOn = rnd.NextDouble() > 0.1, KnobValues = [(float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()] },
        new Effect { Name = "EQ", Type = "Midi", IsOn = rnd.NextDouble() > 0.1, KnobValues = [(float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()] },
        new Effect { Name = "Compressor", Type = "Midi", IsOn = rnd.NextDouble() > 0.1, KnobValues = [(float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()] }
    ];
}

class Snapshot
{
    public string Name;
    public Instrument[] Instruments;
}

struct Instrument
{
    public string Name;
    public Color Color;
    public float Volume;
    public float Pan;
    public int KeyStart;
    public int KeyLength;
    public Effect[] Effects;
    public ADSR adsr;
}

struct Effect
{
    public bool IsOn;
    public string Name;
    public string Type;
    public float[] KnobValues;
    public float Dry, Wet;
}

class PadPlayer
{
    public string[] Pads = ["A", "Bb", "B", "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab"];
    public int SelectedPad;
    public float Volume;
    public float Brightness;
    public float Shimmer;
    public Effect Effect = new Effect { Name = "Reverb", Type = "Midi" };
}

class ADSR
{
    public float Attack = 0.3f;
    public float Decay = 0.3f;
    public float Sustain = 0.8f;
    public float Release = 0.3f;
}
```
