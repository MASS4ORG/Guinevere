using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks <see cref="Gui.SetTransform"/> drawing, clipping, opacity layers and hit testing.</summary>
public class GuiTransformTests
{
    static readonly Vector2 Corner = new(50, 50);

    static SKColor Pixel(FrameHarness harness, int x, int y)
    {
        using var image = harness.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.GetPixel(x, y);
    }

    /// <summary>A 20×20 box at (50, 50) scaled about its corner, so it is drawn over (50..90, 50..90).</summary>
    static LayoutNode Box(Gui gui, Matrix3x2? transform = null, Color? color = null)
    {
        var node = gui.Node(20, 20).Absolute(Corner.X, Corner.Y);
        using (node.Enter())
        {
            gui.SetTransform(transform ?? Matrix3x2.CreateScale(2f, Corner));
            gui.DrawBackgroundRect(color ?? Color.Red);
        }
        return node;
    }

    /// <summary>The subtree is drawn transformed, and identity clears a transform.</summary>
    [Fact]
    public void Draws_Transformed()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui => Box(gui));
        Assert.Equal(SKColors.Red, Pixel(harness, 85, 85));

        LayoutNode? node = null;
        harness.Frame(gui =>
        {
            node = Box(gui, Matrix3x2.CreateTranslation(0, 100), Color.Blue);
            using (node.Enter()) gui.SetTransform(Matrix3x2.Identity);
        });
        Assert.Equal(SKColors.Blue, Pixel(harness, 55, 55));
        Assert.Equal(Matrix3x2.Identity, node!.Transform);
    }

    /// <summary>Events and interactables hit the node where it is drawn, not where it was laid out.</summary>
    [Fact]
    public void HitTests_ThroughTheTransform()
    {
        using var harness = new FrameHarness();
        var clicks = 0;
        var hovered = false;
        void Draw(Gui gui)
        {
            var node = Box(gui);
            node.On<ClickEvent>(_ => clicks++);
            using (node.Enter())
                if (gui.Pass == Pass.Pass2Render) hovered = gui.GetInteractable().OnHover();
        }

        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(85, 85));
        Assert.Equal(1, clicks);
        Assert.True(hovered);

        harness.Click(Draw, new Vector2(95, 95));
        Assert.Equal(1, clicks);
        Assert.False(hovered);
    }

    /// <summary>Interaction masks test the pointer through the transform.</summary>
    [Fact]
    public void InteractionMasks_UseTheTransform()
    {
        using var harness = new FrameHarness();
        var results = new List<bool>();
        var mask = Interactions.Hover;
        void Draw(Gui gui)
        {
            using (Box(gui).Enter())
                if (gui.Pass == Pass.Pass2Render) results.Add(gui.GetInteractable().On(mask));
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(85, 85));
        harness.Frame(Draw);
        mask = Interactions.Click;
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(Draw);
        mask = Interactions.Hold;
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Input.ReleaseButton(MouseButton.Left);
        mask = Interactions.Hover;
        harness.Input.MoveTo(new Vector2(95, 95));
        harness.Frame(Draw);
        Assert.Equal([false, true, true, false, true, true, false], results);
    }

    /// <summary>A transform that collapses the node leaves nothing to hit.</summary>
    [Fact]
    public void SingularTransform_IsNotHittable()
    {
        using var harness = new FrameHarness();
        var clicks = 0;
        LayoutNode? node = null;
        void Draw(Gui gui)
        {
            node = Box(gui, Matrix3x2.CreateScale(0f, 1f, Corner));
            node.On<ClickEvent>(_ => clicks++);
        }

        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(50, 55));
        Assert.Equal(0, clicks);
        Assert.True(float.IsNaN(node!.ScreenToLocal(new Vector2(50, 55)).X));
    }

    /// <summary>A child's transform applies before its ancestors', and the mapping round-trips.</summary>
    [Fact]
    public void NestedTransforms_Compose()
    {
        using var harness = new FrameHarness();
        LayoutNode? child = null;
        harness.Frame(gui =>
        {
            using (gui.Node(100, 100).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f));
                child = gui.Node(10, 10).Absolute(5, 5);
                using (child.Enter()) gui.SetTransform(Matrix3x2.CreateTranslation(10, 0));
            }
        });

        Assert.Equal(new Vector2(30, 10), child!.LocalToScreen(new Vector2(5, 5)));
        var local = child.ScreenToLocal(new Vector2(30, 10));
        Assert.Equal(5f, local.X, 3);
        Assert.Equal(5f, local.Y, 3);
        Assert.Equal(Matrix3x2.CreateTranslation(10, 0) * Matrix3x2.CreateScale(2f), child.WorldTransform);
    }

    /// <summary>An ancestor's clip follows its transform for drawing and for hit testing.</summary>
    [Fact]
    public void AncestorClips_FollowTheTransform()
    {
        using var harness = new FrameHarness();
        var clicks = 0;
        void Draw(Gui gui)
        {
            using (gui.Node(20, 20).Absolute(Corner.X, Corner.Y).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f, Corner));
                gui.ClipContent();
                var inner = gui.Node(60, 60);
                inner.On<ClickEvent>(_ => clicks++);
                using (inner.Enter()) gui.DrawBackgroundRect(Color.Green);
            }
        }

        harness.Frame(Draw);
        Assert.True(Pixel(harness, 85, 85).Green > 100);
        Assert.Equal(0, Pixel(harness, 95, 95).Green);
        harness.Click(Draw, new Vector2(95, 95));
        Assert.Equal(0, clicks);
        harness.Click(Draw, new Vector2(85, 85));
        Assert.Equal(1, clicks);
    }

    /// <summary>A faded group's layer covers its transformed ink, not its untransformed layout.</summary>
    [Fact]
    public void OpacityGroups_CoverTransformedInk()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            using (gui.Node(20, 20).Absolute(Corner.X, Corner.Y).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f, Corner));
                gui.SetOpacity(0.5f);
                using (gui.Node(20, 20).Enter()) gui.DrawBackgroundRect(Color.Red);
            }
        });

        Assert.InRange(Pixel(harness, 85, 85).Alpha, (byte)120, (byte)135);
    }

    /// <summary>The wheel scrolls a transformed scroll container where it is drawn, vertically or with Shift sideways.</summary>
    [Fact]
    public void Wheel_ScrollsWhereDrawn()
    {
        using var harness = new FrameHarness();
        LayoutNode? container = null;
        void Draw(Gui gui)
        {
            container = gui.Node(40, 40).Absolute(Corner.X, Corner.Y);
            using (container.Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f, Corner));
                gui.Scroll();
                gui.Node(200, 200);
            }
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(100, 100));
        harness.Input.Scroll(-1f);
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.LeftShift);
        harness.Input.Scroll(-1f);
        harness.Frame(Draw);
        harness.Input.Scroll(0.0001f);
        harness.Frame(Draw);
        Assert.Equal(new Vector2(50, 50), harness.Gui.GetScrollState(container!.Id)!.ScrollOffset);
    }

    /// <summary>A drag that starts on the transformed node keeps the pointer; deltas stay in screen space.</summary>
    [Fact]
    public void Drags_KeepCaptureWithScreenDeltas()
    {
        using var harness = new FrameHarness();
        var held = false;
        var delta = Vector2.Zero;
        void Draw(Gui gui)
        {
            using (Box(gui).Enter())
            {
                if (gui.Pass != Pass.Pass2Render) return;
                held = gui.GetInteractable().OnDrag(out var args);
                if (held) delta = args.FrameDelta;
            }
        }

        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(85, 85));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        Assert.True(held);
        harness.Input.MoveTo(new Vector2(150, 150));
        harness.Frame(Draw);
        Assert.True(held);
        Assert.Equal(new Vector2(65, 65), delta);
        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(Draw);
        Assert.False(held);
    }
}
