using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Excalibur controls inside a transformed subtree read the pointer where they are drawn.</summary>
public class TransformedControlsTests
{
    static readonly Font TestFont = Font.FromFamilyName("serif");

    /// <summary>A slider scaled twice as wide maps the pointer back onto its track.</summary>
    [Fact]
    public void SliderMapsThePointerThroughTheTransform()
    {
        using var harness = new FrameHarness(800, 200);
        harness.Gui.ConfigureFonts(TestFont);
        var value = 5f;
        void Draw(Gui gui)
        {
            using (gui.Node(400, 60).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f));
                gui.Slider(ref value, 0f, 10f, width: 300, height: 30);
            }
        }

        harness.Input.MoveTo(new Vector2(700, 150));
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(450, 30));
        Assert.Equal(7.5f, value, 2);
    }

    /// <summary>A click on a scaled popup's drawn area keeps it open; a click beyond it dismisses it.</summary>
    [Fact]
    public void PopupDismissesOutsideWhereItIsDrawn()
    {
        using var harness = new FrameHarness(800, 600);
        harness.Gui.ConfigureFonts(TestFont);
        var open = true;
        void Draw(Gui gui)
        {
            using (gui.Node(800, 600).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f));
                gui.Popup(ref open, () => { }, 100, 100, position: new Vector2(10, 10));
            }
        }

        harness.Input.MoveTo(new Vector2(700, 500));
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(200, 200));
        Assert.True(open);
        harness.Click(Draw, new Vector2(300, 300));
        Assert.False(open);
    }

    /// <summary>The pointer helpers use the current node's layout coordinates and never return NaN.</summary>
    [Fact]
    public void PointerHelpersUseLayoutCoordinates()
    {
        using var harness = new FrameHarness();
        harness.Input.MoveTo(new Vector2(40, 40));
        harness.Frame(gui =>
        {
            using (gui.Node(100, 100).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateScale(2f));
                if (gui.Pass != Pass.Pass2Render)
                {
                    return;
                }

                Assert.Equal(new Vector2(20, 20), gui.LocalMousePosition);
                Assert.True(gui.IsPointerIn(new Rect(10, 10, 20, 20)));
                Assert.False(gui.IsPointerIn(new Rect(30, 30, 20, 20)));
                Assert.Equal(new Rect(20, 20, 40, 40), gui.CurrentNode.LocalToScreen(new Rect(10, 10, 20, 20)));
                gui.SetTransform(Matrix3x2.CreateScale(0f));
                Assert.Equal(new Vector2(40, 40), gui.LocalMousePosition);
                Assert.False(gui.IsPointerIn(new Rect(0, 0, 100, 100)));
                gui.SetTransform(Matrix3x2.Identity);
                Assert.Equal(new Rect(1, 2, 3, 4), gui.CurrentNode.LocalToScreen(new Rect(1, 2, 3, 4)));
            }
        });
    }
}
