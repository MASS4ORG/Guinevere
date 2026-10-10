using Serilog;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using SkiaSharp;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace Guinevere;

/// <summary>
/// Renders Guinevere frames into a Vulkan swapchain. Skia draws on the GPU straight into the swapchain images; when a
/// Skia Vulkan context cannot be created, or <c>GUINEVERE_RENDERER=raster</c> is set, Skia rasterizes on the CPU into
/// mapped memory that is copied into the swapchain image.
/// </summary>
public unsafe partial class CanvasRenderer : ICanvasRenderer
{
    const int MaxFramesInFlight = 2;

    readonly ILogger _logger;
    Vk _vk = null!;
    IWindow _window = null!;
    int _width, _height;
    bool _swapchainStale;
    uint _currentFrame;

    CommandPool _commandPool;
    readonly CommandBuffer[] _beginCommands = new CommandBuffer[MaxFramesInFlight];
    readonly CommandBuffer[] _endCommands = new CommandBuffer[MaxFramesInFlight];
    readonly VkSemaphore[] _imageAvailable = new VkSemaphore[MaxFramesInFlight];
    readonly Fence[] _inFlight = new Fence[MaxFramesInFlight];

    /// <summary>
    /// Initializes a renderer using the supplied logger, or the Serilog global logger when omitted.
    /// </summary>
    /// <param name="logger">The logger that receives renderer diagnostics.</param>
    public CanvasRenderer(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    /// <summary>Whether Skia draws on the GPU; false when it rasterizes on the CPU and the result is copied.</summary>
    public bool IsGpuAccelerated => _grContext is not null;

    /// <inheritdoc/>
    public void Initialize(int width, int height)
    {
        throw new InvalidOperationException("Use Initialize(width, height, window) for Vulkan renderer");
    }

    /// <summary>
    /// Initializes the renderer for <paramref name="window"/>: Vulkan device, swapchain and the Skia context.
    /// </summary>
    /// <param name="width">The requested width when the surface does not dictate one.</param>
    /// <param name="height">The requested height when the surface does not dictate one.</param>
    /// <param name="window">The window context to associate with the renderer.</param>
    /// <param name="firstTime">True creates everything; false only recreates the swapchain at the new size.</param>
    public void Initialize(int width, int height, IWindow window, bool firstTime = true)
    {
        _width = width;
        _height = height;
        _window = window;
        if (!firstTime)
        {
            RecreateSwapchain();
            return;
        }

        try
        {
            _vk = Vk.GetApi();
            CreateInstance();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateFrameObjects();
            if (Environment.GetEnvironmentVariable("GUINEVERE_RENDERER") != "raster") CreateGpuContext();
            CreateSwapchain();
            _logger.Information("Vulkan renderer ready: {Mode}, {Width}x{Height}",
                IsGpuAccelerated ? "GPU Skia" : "CPU raster", _extent.Width, _extent.Height);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CanvasRenderer initialization failed");
            throw;
        }
    }

    /// <inheritdoc/>
    public void Render(Action<SKCanvas> draw)
    {
        if (_swapchainStale) RecreateSwapchain();
        if (_swapchain.Handle == 0) return;

        var fence = _inFlight[_currentFrame];
        _vk.WaitForFences(_device, 1, in fence, true, ulong.MaxValue);

        uint imageIndex = 0;
        var acquired = _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue,
            _imageAvailable[_currentFrame], default, ref imageIndex);
        if (acquired == Result.ErrorOutOfDateKhr)
        {
            _swapchainStale = true;
            return;
        }
        if (acquired != Result.Success && acquired != Result.SuboptimalKhr)
        {
            _logger.Error("Failed to acquire swapchain image: {Result}", acquired);
            return;
        }

        _vk.ResetFences(_device, 1, in fence);
        if (IsGpuAccelerated) RenderGpu(imageIndex, draw);
        else RenderRaster(imageIndex, draw);
        Present(imageIndex);
        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == _width && height == _height)) return;
        _width = width;
        _height = height;
        _swapchainStale = true;
    }

    void Present(uint imageIndex)
    {
        var swapchain = _swapchain;
        var finished = _renderFinished![imageIndex];
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &finished,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };
        var presented = _khrSwapchain.QueuePresent(_presentQueue, in presentInfo);
        if (presented is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr) _swapchainStale = true;
        else if (presented != Result.Success) _logger.Error("Failed to present: {Result}", presented);
    }

    /// <summary>Submits one frame's commands: waits for the acquired image and signals the present semaphore.</summary>
    void Submit(CommandBuffer commands, uint imageIndex, bool waitForImage, bool signalPresent, Fence fence)
    {
        var wait = _imageAvailable[_currentFrame];
        var signal = _renderFinished![imageIndex];
        var waitStage = PipelineStageFlags.AllCommandsBit;
        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = waitForImage ? 1u : 0u,
            PWaitSemaphores = &wait,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &commands,
            SignalSemaphoreCount = signalPresent ? 1u : 0u,
            PSignalSemaphores = &signal,
        };
        var result = _vk.QueueSubmit(_graphicsQueue, 1, in submit, fence);
        if (result != Result.Success) _logger.Error("Failed to submit frame commands: {Result}", result);
    }

    /// <summary>Records a whole-image layout transition.</summary>
    void Barrier(CommandBuffer commands, Image image, ImageLayout from, ImageLayout to, PipelineStageFlags srcStage,
        AccessFlags srcAccess, PipelineStageFlags dstStage, AccessFlags dstAccess)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = from,
            NewLayout = to,
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };
        _vk.CmdPipelineBarrier(commands, srcStage, dstStage, 0, 0, null, 0, null, 1, in barrier);
    }

    void BeginCommands(CommandBuffer commands)
    {
        _vk.ResetCommandBuffer(commands, 0);
        var begin = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        _vk.BeginCommandBuffer(commands, in begin);
    }

    void CreateFrameObjects()
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _graphicsFamily,
        };
        Check(_vk.CreateCommandPool(_device, in poolInfo, null, out _commandPool), "create command pool");

        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = MaxFramesInFlight,
        };
        fixed (CommandBuffer* begin = _beginCommands)
            Check(_vk.AllocateCommandBuffers(_device, in allocInfo, begin), "allocate command buffers");
        fixed (CommandBuffer* end = _endCommands)
            Check(_vk.AllocateCommandBuffers(_device, in allocInfo, end), "allocate command buffers");

        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailable[i]), "create semaphore");
            Check(_vk.CreateFence(_device, in fenceInfo, null, out _inFlight[i]), "create fence");
        }
    }

    static void Check(Result result, string action)
    {
        if (result != Result.Success) throw new InvalidOperationException($"Vulkan failed to {action}: {result}");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_device.Handle != 0)
        {
            _vk.DeviceWaitIdle(_device);
            DestroySwapchain();
            DisposeGpuContext();
            for (var i = 0; i < MaxFramesInFlight; i++)
            {
                _vk.DestroySemaphore(_device, _imageAvailable[i], null);
                _vk.DestroyFence(_device, _inFlight[i], null);
            }
            _vk.DestroyCommandPool(_device, _commandPool, null);
            _vk.DestroyDevice(_device, null);
        }

        if (_instance.Handle != 0)
        {
            _khrSurface.DestroySurface(_instance, _surface, null);
            _vk.DestroyInstance(_instance, null);
        }

        _vk?.Dispose();
    }
}
