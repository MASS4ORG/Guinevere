extern alias Vulkan;
using System.Reflection;
using System.Runtime.CompilerServices;
using Serilog;
using Window = Vulkan::Guinevere.GuiWindow;

namespace Guinevere.Tests.Profiling;

public sealed class VulkanHostTests
{
    [Fact]
    public void RenderFailureIsLoggedAndPropagatedAndUninitializedFramesAreIgnored()
    {
        var window = (Window)RuntimeHelpers.GetUninitializedObject(typeof(Window));
        var logger = Substitute.For<ILogger>();
        typeof(Window).GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, logger);
        var callback = typeof(Window).GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic)!;
        callback.Invoke(window, [0d]);
        typeof(Window).GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
        var failure = Assert.Throws<TargetInvocationException>(() => callback.Invoke(window, [0d]));
        Assert.IsType<NullReferenceException>(failure.InnerException);
        logger.Received(1).Error(failure.InnerException, "Render exception");
    }
}
