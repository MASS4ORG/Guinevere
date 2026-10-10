using Silk.NET.Vulkan;
using SkiaSharp;

namespace Guinevere;

public unsafe partial class CanvasRenderer
{
    GRContext? _grContext;
    GRVkExtensions? _grExtensions;
    GRVkGetProcedureAddressDelegate? _getProcedureAddress;

    /// <summary>Creates the Skia Vulkan context on this renderer's device; leaves it null when Skia refuses.</summary>
    void CreateGpuContext()
    {
        try
        {
            _getProcedureAddress = GetProcedureAddress;
            _grExtensions = GRVkExtensions.Create(_getProcedureAddress, _instance.Handle, _physicalDevice.Handle,
                _instanceExtensions, _deviceExtensions);
            using var backend = new GRVkBackendContext
            {
                VkInstance = _instance.Handle,
                VkPhysicalDevice = _physicalDevice.Handle,
                VkDevice = _device.Handle,
                VkQueue = _graphicsQueue.Handle,
                GraphicsQueueIndex = _graphicsFamily,
                MaxAPIVersion = ApiVersion,
                Extensions = _grExtensions,
                GetProcedureAddress = _getProcedureAddress,
            };
            _grContext = GRContext.CreateVulkan(backend);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Skia Vulkan context failed; falling back to CPU rasterization");
            _grContext = null;
        }

        if (_grContext is null) DisposeGpuContext();
    }

    IntPtr GetProcedureAddress(string name, IntPtr instance, IntPtr device) => device != IntPtr.Zero
        ? (IntPtr)_vk.GetDeviceProcAddr(new Device(device), name).Handle
        : (IntPtr)_vk.GetInstanceProcAddr(new Instance(instance), name).Handle;

    void DisposeGpuContext()
    {
        _grContext?.Dispose();
        _grContext = null;
        _grExtensions?.Dispose();
        _grExtensions = null;
    }

    /// <summary>
    /// Draws the frame on the GPU into the acquired image. A first submission waits for the image and moves it to the
    /// attachment layout Skia is told about; Skia submits its work on the same queue, which leaves the image in that
    /// layout; a last submission moves it to the present layout and signals presentation.
    /// </summary>
    void RenderGpu(uint imageIndex, Action<SKCanvas> draw)
    {
        var image = _swapchainImages[imageIndex];
        var begin = _beginCommands[_currentFrame];
        BeginCommands(begin);
        Barrier(begin, image, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags.AllCommandsBit, 0, PipelineStageFlags.AllCommandsBit,
            AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit);
        _vk.EndCommandBuffer(begin);
        Submit(begin, imageIndex, waitForImage: true, signalPresent: false, default);

        var info = new GRVkImageInfo
        {
            Image = image.Handle,
            ImageTiling = (uint)ImageTiling.Optimal,
            ImageLayout = (uint)ImageLayout.ColorAttachmentOptimal,
            Format = (uint)_format,
            ImageUsageFlags = (uint)_usage,
            SampleCount = 1,
            LevelCount = 1,
            CurrentQueueFamily = _graphicsFamily,
            SharingMode = (uint)(_graphicsFamily == _presentFamily ? SharingMode.Exclusive : SharingMode.Concurrent),
        };
        using (var target = new GRBackendRenderTarget((int)_extent.Width, (int)_extent.Height, info))
        using (var surface = SKSurface.Create(_grContext!, target, GRSurfaceOrigin.TopLeft, _colorType))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            draw(canvas);
            _grContext!.Flush(true, false);
        }

        var end = _endCommands[_currentFrame];
        BeginCommands(end);
        Barrier(end, image, ImageLayout.ColorAttachmentOptimal, ImageLayout.PresentSrcKhr,
            PipelineStageFlags.AllCommandsBit, AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.BottomOfPipeBit, 0);
        _vk.EndCommandBuffer(end);
        Submit(end, imageIndex, waitForImage: false, signalPresent: true, _inFlight[_currentFrame]);
    }
}
