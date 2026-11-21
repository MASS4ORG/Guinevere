using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Behaviour of the stylesheet-driven button family: <c>Button</c> with any content, <c>ImageButton</c>.</summary>
public class ButtonFamilyTests
{
    static readonly Vector2 Inside = new(20, 20);
    static readonly Icon Plus = Icon.FromGlyph("+");

    static SKImage Solid(SKColor color)
    {
        var bitmap = new SKBitmap(40, 30);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>A click reports once on the returned node, through text, icon and built content and the image button.</summary>
    [Fact]
    public void ClickReportsActivation()
    {
        using var harness = new FrameHarness(200, 100);
        using var image = Solid(SKColors.Red);
        bool text = false, icon = false, built = false, picture = false;

        harness.Click(g => text |= g.Button("Go").Width(60).Height(30).Clicked, Inside);
        harness.Click(g => icon |= g.Button(Plus.Labeled("Add")).Clicked, Inside);
        harness.Click(g => built |= g.Button(Content.Build(() => g.Node(50, 30))).Clicked, Inside);
        harness.Click(g => picture |= g.ImageButton(image, image, image, content: "Go").Clicked, Inside);

        Assert.True(text && icon && built && picture);
    }

    /// <summary>Disabled buttons never report, whatever the pointer does.</summary>
    [Fact]
    public void DisabledNeverActivates()
    {
        using var harness = new FrameHarness(200, 100);
        using var image = Solid(SKColors.Red);
        var reported = false;

        harness.Click(g => reported |= g.Button("Go", enabled: false).Width(60).Height(30).Clicked, Inside);
        harness.Click(g => reported |= g.ImageButton(image, image, image, Solid(SKColors.Gray), enabled: false)
            .Clicked, Inside);
        harness.Click(g => reported |= g.Button(Plus, enabled: false).Clicked, Inside);

        Assert.False(reported);
    }

    /// <summary>A clicked button takes focus, and Space then activates it from the keyboard.</summary>
    [Fact]
    public void FocusedActivatesWithSpace()
    {
        using var harness = new FrameHarness(200, 100);
        var activated = false;
        void Draw(Gui g) => activated |= g.Button("Go").Width(60).Height(30).Clicked;
        harness.Click(Draw, Inside);
        activated = false;

        harness.Input.MoveTo(new Vector2(-50, -50));
        harness.Input.PressKey(KeyboardKey.Space);
        harness.Frame(Draw);

        Assert.True(activated);
    }

    /// <summary>
    /// A text button fits its caption plus padding; an icon-only one is a field-height square with the <c>icon</c>
    /// class and an icon scaled to two thirds of it, also after the caller resizes the node.
    /// </summary>
    [Fact]
    public void SizesFollowContentUnlessTheNodeIsSized()
    {
        using var harness = new FrameHarness(300, 200);
        LayoutNode? text = null, icon = null, big = null;
        harness.Frame(g =>
        {
            text = g.Button("Caption");
            icon = g.Button(Plus);
            big = g.Button(Plus).Width(60).Height(60);
        });

        var caption = Assert.Single(text!.ChildNodes);
        Assert.Equal(caption.Rect.W + text.PaddingLeftPixels + text.PaddingRightPixels, text.Rect.W, 0.5f);
        Assert.Equal(harness.Gui.ControlStyle.FieldHeight, icon!.Rect.W);
        Assert.Equal(icon.Rect.W, icon.Rect.H);
        Assert.Contains("icon", icon.StyleTarget!.Value.Classes);
        Assert.Equal(40f, Assert.Single(big!.ChildNodes).Rect.W, 0.5f);
    }

    /// <summary>Two buttons from different lines keep separate identities, and an explicit id wins.</summary>
    [Fact]
    public void IdentityIsTheCallSiteUnlessAnIdIsGiven()
    {
        using var harness = new FrameHarness();
        LayoutNode? first = null, second = null, named = null;
        harness.Frame(g =>
        {
            first = g.Button("A");
            second = g.Button("A");
            named = g.Button("A", id: "save");
        });

        Assert.NotEqual(first!.Id, second!.Id);
        Assert.Equal("save", named!.Id);
    }

    /// <summary>
    /// An image button shows the image for the pointer state, under the sheet's border; caller classes reach the sheet.
    /// </summary>
    [Fact]
    public void ImageButtonShowsStateImageUnderTheBorder()
    {
        using var normal = Solid(SKColors.Red);
        using var hover = Solid(SKColors.Yellow);
        using var pressed = Solid(SKColors.Blue);

        SKColor Pixel(bool press, int x)
        {
            using var harness = new FrameHarness(80, 60);
            harness.Gui.StyleSheets.Add(StyleSheet.Parse("button.framed { border-color = #00ff00; border-width = 4; }"));
            harness.Input.MoveTo(Inside);
            if (press)
            {
                harness.Input.PressButton();
            }

            for (var i = 0; i < 3; i++)
            {
                harness.Frame(g => g.ImageButton(normal, hover, pressed, classes: ["framed"]));
            }

            using var snapshot = harness.Snapshot();
            using var bitmap = SKBitmap.FromImage(snapshot);
            return bitmap.GetPixel(x, 15);
        }

        Assert.Equal(SKColors.Yellow, Pixel(press: false, 20));
        Assert.Equal(new SKColor(0, 255, 0), Pixel(press: false, 1));
        Assert.Equal(SKColors.Blue, Pixel(press: true, 20));
    }
}
