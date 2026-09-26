using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

public class DropIndicatorTests
{
    [Theory]
    [InlineData(DropTargetState.HoverAccepted, 0, 255)]
    [InlineData(DropTargetState.HoverRejected, 255, 0)]
    public void PreviewUsesTheAcceptedOrRejectedColor(DropTargetState state, byte red, byte green)
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        var gui = new TestableGui { Input = new ScriptedInputHandler() };
        gui.SetScreenRect(100, 100);
        var style = new DropIndicatorStyle(Color.FromArgb(255, 0, 255, 0),
            Color.FromArgb(255, 255, 0, 0), FillAlpha: 255);

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.Render();
        gui.EndFrame();

        var pixel = bitmap.GetPixel(25, 25);
        Assert.Equal(red, pixel.Red);
        Assert.Equal(green, pixel.Green);

        void Draw()
        {
            using (gui.Node(50, 50).Enter())
                gui.DrawDropIndicator(state, style: style);
        }
    }
}
