using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using SkiaSharp;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace Guinevere;

public unsafe partial class CanvasRenderer
{
    SwapchainKHR _swapchain;
    Image[] _swapchainImages = [];
    VkSemaphore[]? _renderFinished;
    Extent2D _extent;
    Format _format;
    ImageUsageFlags _usage;
    SKColorType _colorType;

    void CreateSwapchain()
    {
        var support = QuerySwapchainSupport(_physicalDevice);
        var capabilities = support.Capabilities;
        _extent = ChooseExtent(capabilities);
        if (_extent.Width == 0 || _extent.Height == 0)
        {
            // Minimized: nothing to present until the surface has an area again.
            _swapchainStale = true;
            return;
        }

        var surfaceFormat = ChooseSurfaceFormat(support.Formats);
        _format = surfaceFormat.Format;
        _colorType = _format is Format.R8G8B8A8Unorm or Format.R8G8B8A8Srgb ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
        _usage = ImageUsageFlags.ColorAttachmentBit
                 | (capabilities.SupportedUsageFlags & (ImageUsageFlags.TransferDstBit | ImageUsageFlags.TransferSrcBit));
        if (!IsGpuAccelerated && !_usage.HasFlag(ImageUsageFlags.TransferDstBit))
            throw new InvalidOperationException("The surface cannot receive copies, which CPU rasterization needs.");

        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0) imageCount = Math.Min(imageCount, capabilities.MaxImageCount);

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = _format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = _extent,
            ImageArrayLayers = 1,
            ImageUsage = _usage,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = ChoosePresentMode(support.PresentModes),
            Clipped = true,
        };
        var families = stackalloc uint[] { _graphicsFamily, _presentFamily };
        if (_graphicsFamily != _presentFamily)
        {
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;
            createInfo.PQueueFamilyIndices = families;
        }

        Check(_khrSwapchain.CreateSwapchain(_device, in createInfo, null, out _swapchain), "create swapchain");
        _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, null);
        _swapchainImages = new Image[imageCount];
        fixed (Image* images = _swapchainImages)
            _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, images);

        // One present semaphore per image: an image's semaphore is only reused after that image is acquired again.
        _renderFinished = new VkSemaphore[imageCount];
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        for (var i = 0; i < imageCount; i++)
            Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinished[i]), "create semaphore");

        if (!IsGpuAccelerated) CreateRasterTargets();
        _swapchainStale = false;
    }

    void RecreateSwapchain()
    {
        _vk.DeviceWaitIdle(_device);
        DestroySwapchain();
        CreateSwapchain();
    }

    void DestroySwapchain()
    {
        DestroyRasterTargets();
        if (_renderFinished is not null)
        {
            foreach (var semaphore in _renderFinished) _vk.DestroySemaphore(_device, semaphore, null);
            _renderFinished = null;
        }
        if (_swapchain.Handle != 0) _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
        _swapchain = default;
        _swapchainImages = [];
    }

    Extent2D ChooseExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue) return capabilities.CurrentExtent;
        return new Extent2D(
            Math.Clamp((uint)_width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Math.Clamp((uint)_height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height));
    }

    /// <summary>Prefers 8-bit UNORM formats, which match Skia's byte layout without color conversion.</summary>
    static SurfaceFormatKHR ChooseSurfaceFormat(SurfaceFormatKHR[] formats)
    {
        foreach (var preferred in (ReadOnlySpan<Format>)[Format.B8G8R8A8Unorm, Format.R8G8B8A8Unorm])
            foreach (var format in formats)
                if (format.Format == preferred && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                    return format;
        return formats[0];
    }

    static PresentModeKHR ChoosePresentMode(PresentModeKHR[] modes) =>
        modes.Contains(PresentModeKHR.MailboxKhr) ? PresentModeKHR.MailboxKhr : PresentModeKHR.FifoKhr;

    SwapchainSupportDetails QuerySwapchainSupport(PhysicalDevice device)
    {
        var details = new SwapchainSupportDetails();
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, _surface, out details.Capabilities);

        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface, ref formatCount, null);
        details.Formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* formats = details.Formats)
            _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface, ref formatCount, formats);

        uint modeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface, ref modeCount, null);
        details.PresentModes = new PresentModeKHR[modeCount];
        fixed (PresentModeKHR* modes = details.PresentModes)
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface, ref modeCount, modes);

        return details;
    }

    struct SwapchainSupportDetails
    {
        public SurfaceCapabilitiesKHR Capabilities;
        public SurfaceFormatKHR[] Formats;
        public PresentModeKHR[] PresentModes;
    }
}
