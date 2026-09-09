using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>
/// Provides shared helper methods for layout node tests.
/// </summary>
public abstract class LayoutNodeTestBase
{
    private static uint _globalOrderTemp;

    /// <summary>
    /// Creates a testable GUI with the given screen dimensions and begins a frame.
    /// </summary>
    protected TestableGui CreateTestGui(int width = 800, int height = 600)
    {
        var gui = new TestableGui();
        gui.SetScreenRect(width, height);
        // Still need a canvas for BeginFrame, but we'll override ScreenRect
        var canvas = CreateTestCanvas();
        gui.BeginFrame(canvas);
        return gui;
    }

    private SKCanvas CreateTestCanvas()
    {
        var surface = SKSurface.Create(new SKImageInfo(800, 600));
        return surface.Canvas;
    }

    /// <summary>
    /// Creates a layout node with an optional parent, width, and height, using the provided GUI or creating a new one.
    /// </summary>
    protected LayoutNode CreateTestLayoutNode(Gui? gui = null, LayoutNode? parent = null, float? width = null,
        float? height = null)
    {
        gui ??= CreateTestGui();
        return new LayoutNode($"{_globalOrderTemp++}", gui, parent, width, height);
    }

    /// <summary>
    /// Asserts that all four margin values on the node's style match the expected values.
    /// </summary>
    protected void AssertMarginValues(LayoutNode node, float expectedTop, float expectedRight, float expectedBottom,
        float expectedLeft)
    {
        Assert.Equal(expectedTop, node.Style.MarginTop);
        Assert.Equal(expectedRight, node.Style.MarginRight);
        Assert.Equal(expectedBottom, node.Style.MarginBottom);
        Assert.Equal(expectedLeft, node.Style.MarginLeft);
    }

    /// <summary>
    /// Asserts that all four padding values on the node's style match the expected values.
    /// </summary>
    protected void AssertPaddingValues(LayoutNode node, float expectedTop, float expectedRight, float expectedBottom,
        float expectedLeft)
    {
        Assert.Equal(expectedTop, node.Style.PaddingTop);
        Assert.Equal(expectedRight, node.Style.PaddingRight);
        Assert.Equal(expectedBottom, node.Style.PaddingBottom);
        Assert.Equal(expectedLeft, node.Style.PaddingLeft);
    }

    /// <summary>
    /// Asserts that the rect's X, Y, W, and H values match the expected values within a tolerance of two decimal places.
    /// </summary>
    protected void AssertRectValues(Rect rect, float expectedX, float expectedY, float expectedW, float expectedH)
    {
        Assert.Equal(expectedX, rect.X, 2);
        Assert.Equal(expectedY, rect.Y, 2);
        Assert.Equal(expectedW, rect.W, 2);
        Assert.Equal(expectedH, rect.H, 2);
    }

    /// <summary>
    /// Sets the GUI stage to the specified pass.
    /// </summary>
    protected void SetGuiStage(Gui gui, Pass pass)
    {
        gui.SetStage(pass);
    }

    /// <summary>
    /// Creates a layout node initialized with a build-stage GUI.
    /// </summary>
    protected LayoutNode CreateNodeWithBuildStage()
    {
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        return CreateTestLayoutNode(gui);
    }

    /// <summary>
    /// Creates a layout node initialized with a render-stage GUI.
    /// </summary>
    protected LayoutNode CreateNodeWithRenderStage()
    {
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass2Render);
        return CreateTestLayoutNode(gui);
    }

    /// <summary>
    /// Verifies that a fluent method returns the same original instance.
    /// </summary>
    protected void VerifyFluentReturn<T>(T original, T returned) where T : LayoutNode
    {
        Assert.Same(original, returned);
    }

    /// <summary>
    /// Gets the value of a private instance field via reflection.
    /// </summary>
    protected T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new ArgumentException($"Field '{fieldName}' not found on type '{obj.GetType().Name}'");

        var value = field.GetValue(obj);
        return value == null ? default(T)! : (T)value;
    }

    /// <summary>
    /// Gets a named property value from the node's style via reflection, treating -1f as null for nullable floats.
    /// </summary>
    protected T GetStyleProperty<T>(LayoutNode node, string propertyName)
    {
        var styleField = node.GetType().GetField("Style", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (styleField == null)
            throw new ArgumentException("_style field not found on LayoutNode");

        var style = styleField.GetValue(node);
        if (style == null)
            throw new ArgumentException("_style is null");

        var property = style.GetType().GetProperty(propertyName);
        if (property == null)
            throw new ArgumentException($"Property '{propertyName}' not found on LayoutStyle");

        var value = property.GetValue(style);

        // Special handling for nullable float types - convert -1f to null
        if (typeof(T) == typeof(float?) && value is -1f)
        {
            return default(T)!;
        }

        return value == null ? default(T)! : (T)value;
    }
}
