using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the toast notification widget: messages render pinned to a chosen corner, distinct messages
/// stack away from the screen edge, identical messages stack like any other (each <c>Toast</c> call is
/// its own entry), expired toasts are pruned, and clearing annuls the whole queue. Toasts are queued
/// with <c>Toast</c> and rendered by <c>Toasts</c> from the frame after they are queued, so both passes
/// agree on the tree.
/// </summary>
public class ToastTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly ToastOptions BottomRight = new() { Duration = 10f };
    private static readonly ToastOptions TopLeft = new() { Corner = ToastCorner.TopLeft, Duration = 10f };

    private static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    /// <summary>
    /// Runs <paramref name="content"/> inside a host node (so toast rects can be inspected under one
    /// parent) while calling <c>Toasts()</c> every frame exactly like the examples do.
    /// </summary>
    private static void Frame(Gui gui, Action<Gui>? content = null, IInputHandler? input = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        gui.Input = input ?? NoInput();
        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        BuildContent(gui, content);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        BuildContent(gui, content);
        gui.Render();
        gui.EndFrame();
    }

    private static void BuildContent(Gui gui, Action<Gui>? content)
    {
        using (gui.Node().Enter())
        {
            content?.Invoke(gui);
            gui.Toasts();
        }
    }

    private static IReadOnlyList<LayoutNode> ToastsOf(LayoutNode root)
    {
        if (root is null || root.Children.Count == 0) return [];

        return root.Children[0].ChildNodes;
    }

    private static IInputHandler NoInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        input.MouseDelta.Returns(Vector2.Zero);
        input.MouseWheelDelta.Returns(0f);
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
        input.IsAnyKeyDown.Returns(false);
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        return input;
    }

    [Fact]
    public void QueuedToastRendersFromTheNextFrame()
    {
        var gui = CreateGui();
        Frame(gui, g => g.Toast("Hello"));

        Assert.Single(ToastsOf(gui.RootNode!));
    }

    [Fact]
    public void IdenticalMessagesStackLikeAnyOther()
    {
        var gui = CreateGui();
        Frame(gui, g =>
        {
            g.Toast("Hello");
            g.Toast("Hello");
        });

        Assert.Equal(2, ToastsOf(gui.RootNode!).Count);
    }

    [Fact]
    public void DistinctMessagesStackAboveEachOther()
    {
        var gui = CreateGui();
        Frame(gui, g =>
        {
            g.Toast("First");
            g.Toast("Second");
        });

        var toasts = ToastsOf(gui.RootNode!);
        Assert.Equal(2, toasts.Count);

        var first = toasts[0].Rect;
        var second = toasts[1].Rect;

        Assert.Equal(first.X + first.W, second.X + second.W, 1f);
        Assert.True(first.Y > second.Y, "Newest toast sits closest to the bottom-right corner.");
    }

    [Fact]
    public void ToastPinsToTheRequestedCorner()
    {
        var gui = CreateGui();
        Frame(gui, g =>
        {
            g.Toast("Bottom right", BottomRight);
            g.Toast("Top left", TopLeft);
        });

        var toasts = ToastsOf(gui.RootNode!);
        Assert.Equal(2, toasts.Count);

        var bottomRight = toasts.First(n => n.Rect.Y > Height / 2f);
        var topLeft = toasts.First(n => n.Rect.Y < Height / 2f);

        Assert.Equal(Width - BottomRight.Margin - bottomRight.Rect.W, bottomRight.Rect.X, 1f);
        Assert.Equal(Height - BottomRight.Margin - bottomRight.Rect.H, bottomRight.Rect.Y, 1f);
        Assert.Equal(TopLeft.Margin, topLeft.Rect.X, 1f);
        Assert.Equal(TopLeft.Margin, topLeft.Rect.Y, 1f);
    }

    [Fact]
    public void ExpiredToastsDisappear()
    {
        var gui = CreateGui();
        Frame(gui, g => g.Toast("Short lived", BottomRight));
        Assert.Single(ToastsOf(gui.RootNode!));

        // Advance well past the 10s duration, then host again: the toast must be pruned.
        for (var i = 0; i < 700; i++) Frame(gui);

        Assert.Empty(ToastsOf(gui.RootNode!));
    }

    [Fact]
    public void ClearToastsAnnulsTheQueue()
    {
        var gui = CreateGui();
        Frame(gui, g => g.Toast("Hello"));
        Assert.Single(ToastsOf(gui.RootNode!));

        Frame(gui, g => g.ClearToasts());

        Assert.Empty(ToastsOf(gui.RootNode!));
    }

    [Fact]
    public void ToastRendersAboveRegularContent()
    {
        var gui = CreateGui();
        Frame(gui, g => g.Toast("Hello"));

        var toast = Assert.Single(ToastsOf(gui.RootNode!));
        Assert.True(toast.Scope.Get<LayoutNodeScopeZIndex>().Value > 0,
            "Toasts must float above ordinary z=0 content so later sections cannot paint over them.");
    }

    [Fact]
    public void RepeatingTheSameMessageStacksANewEntryEachTime()
    {
        var gui = CreateGui();
        Frame(gui, g => g.Toast("Hello"));
        Assert.Single(ToastsOf(gui.RootNode!));

        // The harness runs the content once per pass, so every additional frame enqueues two more
        // entries. None expire within the 3s lifetime, so the stack keeps growing.
        for (var i = 0; i < 2; i++)
            Frame(gui, g => g.Toast("Hello"));

        Assert.Equal(5, ToastsOf(gui.RootNode!).Count);

        // The plain frame picks up the entry the previous frame's Pass2 enqueued: six so far, none
        // deduplicated away.
        Frame(gui);
        Assert.Equal(6, ToastsOf(gui.RootNode!).Count);
    }

    [Fact]
    public void TextNodeIsBuiltDuringPass1SoItLaysOutInsideTheToast()
    {
        var gui = CreateGui();
        Frame(gui, g => g.Toast("Hello"));

        // The text must be a measured layout node inside the toast, not a node created during Pass2
        // after CalculateLayout already ran (which would give it a (0,0,0,0) rect and draw it at the
        // canvas origin instead of inside the panel).
        var toast = Assert.Single(ToastsOf(gui.RootNode!));
        var text = Assert.Single(toast.Children);
        Assert.True(text.Rect.W > 0 && text.Rect.H > 0, "Toast text node must be measured during Pass1.");
        Assert.True(toast.Rect.X <= text.Rect.X && text.Rect.X + text.Rect.W <= toast.Rect.X + toast.Rect.W,
            "Toast text must render inside the toast panel.");
    }
}
