namespace Guinevere.Tests.Mocks;

/// <summary>
/// Test subclass of Gui that allows overriding the screen rectangle for testing.
/// </summary>
public class TestableGui : Gui
{
    Rect _testScreenRect;
    bool _useTestScreenRect;

    /// <summary>
    /// Gets the screen rectangle, returning the test override if set.
    /// </summary>
    public override Rect ScreenRect => _useTestScreenRect ? _testScreenRect : base.ScreenRect;

    /// <summary>
    /// Sets the screen rectangle using width and height values.
    /// </summary>
    public void SetScreenRect(float width, float height)
    {
        _testScreenRect = new Rect(0, 0, width, height);
        _useTestScreenRect = true;
    }

    /// <summary>
    /// Sets the screen rectangle using a Rect value.
    /// </summary>
    public void SetScreenRect(Rect rect)
    {
        _testScreenRect = rect;
        _useTestScreenRect = true;
    }

    /// <summary>
    /// Disables the test screen rectangle override and reverts to the real canvas size.
    /// </summary>
    public void UseRealCanvas()
    {
        _useTestScreenRect = false;
    }
}
