using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Behaviour of the stylesheet-driven button family: <c>Button</c>, <c>IconButton</c>, <c>ImageButton</c>.</summary>
public class ButtonFamilyTests
{
    static readonly Vector2 Inside = new(20, 20);

    static SKImage Solid(SKColor color)
    {
        var bitmap = new SKBitmap(40, 30);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>A click reports once, through each button kind.</summary>
    [Fact]
    public void Click_ReportsActivation()
    {
        using var harness = new FrameHarness(200, 100);
        using var image = Solid(SKColors.Red);
        bool button = false, icon = false, charIcon = false, picture = false;

        harness.Click(g => button |= g.Button("Go", 60, 30), Inside);
        harness.Click(g => icon |= g.IconButton("+", 30), Inside);
        harness.Click(g =>
        {
            var clicked = false;
            g.IconButton('x', ref clicked, 30);
            charIcon |= clicked;
        }, Inside);
        harness.Click(g => picture |= g.ImageButton(image, image, image, text: "Go"), Inside);

        Assert.True(button && icon && charIcon && picture);
    }

    /// <summary>Disabled buttons never report, whatever the pointer does; a null icon draws an empty square.</summary>
    [Fact]
    public void Disabled_NeverActivates()
    {
        using var harness = new FrameHarness(200, 100);
        using var image = Solid(SKColors.Red);
        var reported = false;

        harness.Click(g => reported |= g.Button("Go", 60, 30, enabled: false), Inside);
        harness.Click(g => reported |= g.ImageButton(image, image, image, Solid(SKColors.Gray), enabled: false), Inside);
        harness.Click(g =>
        {
            var clicked = false;
            g.IconButton(null, ref clicked, 30, enabled: false);
            reported |= clicked;
        }, Inside);

        Assert.False(reported);
    }

    /// <summary>A clicked button takes focus, and Space then activates it from the keyboard.</summary>
    [Fact]
    public void Focused_ActivatesWithSpace()
    {
        using var harness = new FrameHarness(200, 100);
        harness.Click(g => g.Button("Go", 60, 30), Inside);

        var activated = false;
        harness.Input.MoveTo(new Vector2(-50, -50));
        harness.Input.PressKey(KeyboardKey.Space);
        harness.Frame(g => activated |= g.Button("Go", 60, 30));

        Assert.True(activated);
    }

    /// <summary>
    /// An image button shows the image for the pointer state, under the sheet's border; caller classes reach the sheet.
    /// </summary>
    [Fact]
    public void ImageButton_ShowsStateImageUnderTheBorder()
    {
        using var normal = Solid(SKColors.Red);
        using var hover = Solid(SKColors.Yellow);
        using var pressed = Solid(SKColors.Blue);

        SKColor Pixel(bool press, int x)
        {
            using var harness = new FrameHarness(80, 60);
            harness.Gui.StyleSheets.Add(StyleSheet.Parse("button.framed { border-color = #00ff00; border-width = 4; }"));
            harness.Input.MoveTo(Inside);
            if (press) harness.Input.PressButton(MouseButton.Left);
            for (var i = 0; i < 3; i++) harness.Frame(g => g.ImageButton(normal, hover, pressed, classes: ["framed"]));
            using var snapshot = harness.Snapshot();
            using var bitmap = SKBitmap.FromImage(snapshot);
            return bitmap.GetPixel(x, 15);
        }

        Assert.Equal(SKColors.Yellow, Pixel(press: false, 20));
        Assert.Equal(new SKColor(0, 255, 0), Pixel(press: false, 1));
        Assert.Equal(SKColors.Blue, Pixel(press: true, 20));
    }
}
