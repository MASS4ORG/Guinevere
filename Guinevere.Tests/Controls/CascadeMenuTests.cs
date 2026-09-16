using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the pop-up cascade menu a panel opens on a right-click: it appears once laid out rather than
/// on the frame it is asked for, an item runs and closes it, and a press outside dismisses it.
/// </summary>
public class CascadeMenuTests
{
    private const int Width = 500;
    private const int Height = 400;

    private static readonly Font TestFont = Font.FromFamilyName("serif");
    private static readonly Vector2 At = new(100, 100);

    private sealed class Harness
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Width, Height));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;

        public Harness()
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(Width, Height);
            _input.GetTypedCharacters().Returns(string.Empty);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public bool IsOpen { get; set; }

        public List<string> Log { get; } = [];

        /// <summary>Runs one frame, optionally opening the menu from the render pass as a panel would.</summary>
        public void Frame(Vector2? mouse = null, bool pressed = false, bool openDuringRender = false)
        {
            var at = mouse ?? new Vector2(-100, -100);
            _input.MousePosition.Returns(at);
            _input.PrevMousePosition.Returns(at);
            _input.IsMouseButtonPressed(Arg.Any<MouseButton>())
                .Returns(call => pressed && call.Arg<MouseButton>() == MouseButton.Left);
            _input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);

            void Draw()
            {
                if (openDuringRender && _gui.Pass == Pass.Pass2Render) IsOpen = true;

                var open = IsOpen;
                // ReSharper disable once ExplicitCallerInfoArgument - one stable id across frames.
                _gui.CascadeMenu(ref open, At, menu => menu
                        .Item("Rename", () => Log.Add("rename"))
                        .Submenu("New", sub => sub.Item("Folder", () => Log.Add("folder"))),
                    filePath: "cascade-test", lineNumber: 1);
                IsOpen = open;
            }

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, TestFont);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();
        }

        public List<LayoutNode> Nodes() => Walk(_gui.RootNode!).ToList();

        private static IEnumerable<LayoutNode> Walk(LayoutNode node)
        {
            if (node.Id.StartsWith("/menubar/", StringComparison.Ordinal)) yield return node;

            foreach (var child in node.Children)
                foreach (var descendant in Walk(child))
                    yield return descendant;
        }
    }

    /// <summary>
    /// A panel opens its menu from the render pass, where the right-click is seen. The menu must survive
    /// that frame and appear on the next one, laid out — not be written back to closed and lost.
    /// </summary>
    [Fact]
    public void OpeningFromTheRenderPassSurvivesToTheNextFrame()
    {
        var h = new Harness();

        h.Frame(openDuringRender: true);
        Assert.True(h.IsOpen);
        Assert.Empty(h.Nodes());

        h.Frame();
        Assert.True(h.IsOpen);
        Assert.NotEmpty(h.Nodes());
        Assert.All(h.Nodes(), node => Assert.True(node.Rect is { W: > 0, H: > 0 }));
    }

    /// <summary>Clicking an item runs it and closes the menu.</summary>
    [Fact]
    public void ClickingAnItemRunsItAndCloses()
    {
        var h = new Harness { IsOpen = true };

        h.Frame();
        h.Frame(new Vector2(At.X + 20, At.Y + 13), pressed: true);

        Assert.Equal(["rename"], h.Log);
        Assert.False(h.IsOpen);
    }

    /// <summary>A press away from the cascade dismisses it without running anything.</summary>
    [Fact]
    public void PressingOutsideDismissesTheMenu()
    {
        var h = new Harness { IsOpen = true };

        h.Frame();
        h.Frame(new Vector2(400, 350), pressed: true);

        Assert.Empty(h.Log);
        Assert.False(h.IsOpen);
    }
}
