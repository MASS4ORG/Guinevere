namespace Guinevere.Tests.Platform;

public class PlatformCapabilitiesTests
{
    [Fact]
    public void GuiPublishesItsClockAndAssignedLegacyServices()
    {
        var gui = new Gui();
        var host = Substitute.For<IInputHandler, IWindowHandler>();

        gui.Input = host;
        gui.WindowHandler = (IWindowHandler)host;

        Assert.Same(gui.Time, gui.Platform.Require<ITimeCapability>());
        Assert.Same(host, gui.Platform.Require<IInputHandler>());
        Assert.Same(host, gui.Platform.Require<IClipboard>());
        Assert.Same((IWindowHandler)host, gui.Platform.Require<IWindowHandler>());
    }

    [Fact]
    public void OptionalCapabilityDiscoveryIsExplicit()
    {
        var platform = new PlatformCapabilities();

        Assert.False(platform.Supports<ICursorCapability>());
        Assert.False(platform.TryGet<ICursorCapability>(out var cursor));
        Assert.Null(cursor);
        var error = Assert.Throws<PlatformCapabilityException>(() => platform.Require<ICursorCapability>());
        Assert.Equal(typeof(ICursorCapability), error.CapabilityType);
    }

    [Fact]
    public void RegistrationReplacementAndRemovalHaveDeterministicOwnership()
    {
        var platform = new PlatformCapabilities();
        var first = Substitute.For<ICursorCapability>();
        var second = Substitute.For<ICursorCapability>();

        platform.Register<ICursorCapability>(first);
        platform.Register<ICursorCapability>(second);

        Assert.Same(second, platform.Require<ICursorCapability>());
        Assert.True(platform.Remove<ICursorCapability>());
        Assert.False(platform.Supports<ICursorCapability>());
    }

    [Fact]
    public void SharedConformanceChecksRequiredAndPublishedCapabilities()
    {
        var platform = new PlatformCapabilities();
        var host = Substitute.For<IInputHandler, IWindowHandler>();
        host.GetClipboardText().Returns(string.Empty);
        var display = Substitute.For<IDisplayCapability>();
        display.ScaleFactor.Returns(2f);
        display.LogicalSize.Returns(new Vector2(800, 600));
        display.FramebufferSize.Returns(new Vector2(1600, 1200));

        platform.Register<IInputHandler>(host)
            .Register<IClipboard>(host)
            .Register<IWindowHandler>((IWindowHandler)host)
            .Register<ITimeCapability>(new Time())
            .Register<IDisplayCapability>(display);

        Assert.Empty(PlatformConformance.Validate(platform, requireRenderer: false));
    }

    [Fact]
    public void SharedConformanceReportsMissingServicesAndInvalidDpi()
    {
        var platform = new PlatformCapabilities();
        var display = Substitute.For<IDisplayCapability>();
        display.ScaleFactor.Returns(0f);
        display.LogicalSize.Returns(new Vector2(-1, 600));
        display.FramebufferSize.Returns(new Vector2(float.NaN, 1200));
        platform.Register<IDisplayCapability>(display);

        var failures = PlatformConformance.Validate(platform);

        Assert.Contains(failures, failure => failure.Contains(nameof(IInputHandler)));
        Assert.Contains(failures, failure => failure.Contains(nameof(ICanvasRenderer)));
        Assert.Contains(failures, failure => failure.Contains(nameof(IDisplayCapability.ScaleFactor)));
        Assert.Contains(failures, failure => failure.Contains(nameof(IDisplayCapability.LogicalSize)));
        Assert.Contains(failures, failure => failure.Contains(nameof(IDisplayCapability.FramebufferSize)));
    }
}
