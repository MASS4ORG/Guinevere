using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using SkiaSharp;
using Buffer = System.Buffer;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace Guinevere.Vulkan.SilkNET;

/// <summary>
/// Represents a Vulkan-based canvas renderer using Silk.NET and SkiaSharp.
/// This class handles rendering operations, screen resizing, and resource disposal
/// for a Vulkan-powered window interface.
/// </summary>
public unsafe class CanvasRenderer : ICanvasRenderer
{
    private Vk _vk = null!;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _presentQueue;
    private SurfaceKHR _surface;
    private SwapchainKHR _swapchain;
    private Image[]? _swapchainImages;
    private ImageView[]? _swapchainImageViews;
    private RenderPass _renderPass;
    private Pipeline _graphicsPipeline;
    private PipelineLayout _pipelineLayout;
    private Framebuffer[]? _framebuffers;
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = null!;
    private VkSemaphore[] _imageAvailableSemaphores = null!;
    private VkSemaphore[] _renderFinishedSemaphores = null!;
    private Fence[] _inFlightFences = null!;
    private VkBuffer _vertexBuffer;
    private VkBuffer _indexBuffer;
    private Image _textureImage;
    private DeviceMemory _textureImageMemory;
    private ImageView _textureImageView;
    private Sampler _textureSampler;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool;
    private DescriptorSet[]? _descriptorSets;

    private KhrSurface _khrSurface = null!;
    private KhrSwapchain _khrSwapchain = null!;

    private SKSurface? _skiaSurface;
    private SKCanvas? _canvas;
    private int _width, _height;
    private uint _currentFrame;
    private const int MaxFramesInFlight = 2;

    private uint _graphicsFamily;
    private uint _presentFamily;
    private Format _swapchainImageFormat;
    private Extent2D _swapchainExtent;

    private IWindow _window = null!;

    // Vertex data for full-screen quad with corrected texture coordinates
    private readonly float[] _vertexData =
    [
        -1f, -1f, 0f, 0f,
        1f, -1f, 1f, 0f,
        1f, 1f, 1f, 1f,
        -1f, 1f, 0f, 1f
    ];

    private readonly ushort[] _indices = [0, 1, 2, 2, 3, 0];

    /// <inheritdoc/>
    public void Initialize(int width, int height)
    {
        throw new InvalidOperationException("Use Initialize(width, height, window) for Vulkan renderer");
    }

    /// <summary>
    /// Initializes the CanvasRenderer with the specified dimensions, window context, and configuration.
    /// </summary>
    /// <param name="width">The width of the rendering canvas.</param>
    /// <param name="height">The height of the rendering canvas.</param>
    /// <param name="window">The window context to associate with the renderer.</param>
    /// <param name="firstTime">
    /// Indicates whether this is the first time initialization. If true, performs full initialization.
    /// False allows for partial initialization, such as during resizing.
    /// </param>
    public void Initialize(int width, int height, IWindow window, bool firstTime = true)
    {
        Console.WriteLine($"Initializing CanvasRenderer: {width}x{height}");
        _width = width;
        _height = height;
        _window = window;

        try
        {
            Console.WriteLine("Getting Vulkan API...");
            _vk = Vk.GetApi();
            Console.WriteLine("✓ Vulkan API obtained");

            InitializeVulkan(firstTime);
            InitializeSkia();

            Console.WriteLine("CanvasRenderer initialization complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization failed: {ex}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void InitializeVulkan(bool firstTime = true)
    {
        if (firstTime)
        {
            Console.WriteLine("Starting Vulkan initialization...");
            CreateInstance();
            Console.WriteLine("✓ Instance created");
            CreateSurface();
            Console.WriteLine("✓ Surface created");
            PickPhysicalDevice();
            Console.WriteLine("✓ Physical device picked");
            CreateLogicalDevice();
        }

        Console.WriteLine("✓ Logical device created");
        CreateSwapchain();
        Console.WriteLine("✓ Swapchain created");
        CreateImageViews();

        // if (firstTime)
        {
            Console.WriteLine("✓ Image views created");
            CreateRenderPass();
            Console.WriteLine("✓ Render pass created");
            CreateDescriptorSetLayout();
        }

        Console.WriteLine("✓ Descriptor set layout created");
        CreateGraphicsPipeline();
        Console.WriteLine("✓ Graphics pipeline created");
        CreateFramebuffers();
        // if (firstTime)
        {
            Console.WriteLine("✓ Framebuffers created");
            CreateCommandPool();
            Console.WriteLine("✓ Command pool created");
            CreateCommandBuffers();
            Console.WriteLine("✓ Command buffers created");
            CreateSyncObjects();
        }

        Console.WriteLine("✓ Sync objects created");
        CreateTextureImage();
        Console.WriteLine("✓ Texture image created");
        CreateTextureImageView();
        if (firstTime)
        {
            Console.WriteLine("✓ Texture image view created");
            CreateTextureSampler();
            Console.WriteLine("✓ Texture sampler created");
            CreateVertexBuffer();
            Console.WriteLine("✓ Vertex buffer created");
            CreateIndexBuffer();
        }

        Console.WriteLine("✓ Index buffer created");
        CreateDescriptorPool();
        Console.WriteLine("✓ Descriptor pool created");
        CreateDescriptorSets();
        Console.WriteLine("✓ Descriptor sets created");
        Console.WriteLine("Vulkan initialization complete!");
    }

    private void InitializeSkia()
    {
        Console.WriteLine($"Initializing Skia surface: {_width}x{_height}");
        _skiaSurface = SKSurface.Create(new SKImageInfo(_width, _height, SKColorType.Rgba8888));
        _canvas = _skiaSurface?.Canvas;
        if (_canvas == null)
        {
            Console.WriteLine("ERROR: Failed to create Skia canvas!");
        }
        else
        {
            Console.WriteLine("✓ Skia surface and canvas created");
        }
    }

    private void CreateInstance()
    {
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Guinevere Vulkan App"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Guinevere"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        // Manually specify required extensions for Linux
        var extensionNames = new[]
        {
            "VK_KHR_surface", "VK_KHR_xlib_surface", // For Linux X11
            "VK_KHR_wayland_surface" // For Linux Wayland
        };

        var extensions = SilkMarshal.StringArrayToPtr(extensionNames);

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensionNames.Length,
            PpEnabledExtensionNames = (byte**)extensions,
            EnabledLayerCount = 0,
            PpEnabledLayerNames = null
        };

        if (_vk.CreateInstance(in createInfo, null, out _instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
            throw new Exception("Failed to get KHR Surface extension");

        SilkMarshal.Free(extensions);
    }

    private void CreateSurface()
    {
        // Use Silk.NET's CreateVkSurface if available
        if (_window.VkSurface != null)
        {
            _surface = _window.VkSurface.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
            return;
        }

        // Fallback: Try X11 first, then Wayland
        if (_window.Native?.X11 != null)
        {
            // X11 surface creation
            if (!_vk.TryGetInstanceExtension(_instance, out KhrXlibSurface xlibSurface))
            {
                throw new Exception("Failed to get X11 surface extension");
            }

            var x11Display = _window.Native.X11.Value.Display;
            var x11Window = _window.Native.X11.Value.Window;

            var createInfo = new XlibSurfaceCreateInfoKHR
            {
                SType = StructureType.XlibSurfaceCreateInfoKhr, Dpy = (nint*)x11Display, Window = (nint)x11Window
            };

            if (xlibSurface.CreateXlibSurface(_instance, in createInfo, null, out _surface) != Result.Success)
            {
                throw new Exception("Failed to create X11 Vulkan surface");
            }
        }
        else if (_window.Native?.Wayland != null)
        {
            // Wayland surface creation
            if (!_vk.TryGetInstanceExtension(_instance, out KhrWaylandSurface waylandSurface))
            {
                throw new Exception("Failed to get Wayland surface extension");
            }

            var display = _window.Native.Wayland.Value.Display;
            var surface = _window.Native.Wayland.Value.Surface;

            var createInfo = new WaylandSurfaceCreateInfoKHR
            {
                SType = StructureType.WaylandSurfaceCreateInfoKhr,
                Display = (nint*)display,
                Surface = (nint*)surface
            };

            if (waylandSurface.CreateWaylandSurface(_instance, in createInfo, null, out _surface) != Result.Success)
            {
                throw new Exception("Failed to create Wayland Vulkan surface");
            }
        }
        else
        {
            throw new Exception("No supported native window handle available (X11 or Wayland)");
        }
    }

    private void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);

        if (deviceCount == 0)
            throw new Exception("Failed to find GPUs with Vulkan support");

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);

        foreach (var device in devices)
        {
            if (IsDeviceSuitable(device))
            {
                _physicalDevice = device;
                break;
            }
        }

        if (_physicalDevice.Handle == 0)
            throw new Exception("Failed to find a suitable GPU");
    }

    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        var indices = FindQueueFamilies(device);
        var extensionsSupported = CheckDeviceExtensionSupport(device);

        var swapchainAdequate = false;
        if (extensionsSupported)
        {
            var swapchainSupport = QuerySwapchainSupport(device);
            swapchainAdequate = swapchainSupport.Formats.Length > 0 && swapchainSupport.PresentModes.Length > 0;
        }

        return indices.IsComplete && extensionsSupported && swapchainAdequate;
    }

    private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
    {
        var indices = new QueueFamilyIndices();

        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indices.GraphicsFamily = i;

            _khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupport);
            if (presentSupport)
                indices.PresentFamily = i;

            if (indices.IsComplete)
                break;
        }

        return indices;
    }

    private struct QueueFamilyIndices
    {
        public uint? GraphicsFamily;
        public uint? PresentFamily;
        public bool IsComplete => GraphicsFamily.HasValue && PresentFamily.HasValue;
    }

    private bool CheckDeviceExtensionSupport(PhysicalDevice device)
    {
        uint extensionCount = 0;
        _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, availableExtensionsPtr);

        var requiredExtensions = new HashSet<string> { KhrSwapchain.ExtensionName };

        foreach (var extension in availableExtensions)
        {
            var extensionName = Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName);
            requiredExtensions.Remove(extensionName!);
        }

        return requiredExtensions.Count == 0;
    }

    private SwapchainSupportDetails QuerySwapchainSupport(PhysicalDevice device)
    {
        var details = new SwapchainSupportDetails();

        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, _surface, out details.Capabilities);

        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface, ref formatCount, null);

        if (formatCount != 0)
        {
            details.Formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
                _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface, ref formatCount, formatsPtr);
        }

        uint presentModeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface, ref presentModeCount, null);

        if (presentModeCount != 0)
        {
            details.PresentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* presentModesPtr = details.PresentModes)
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface, ref presentModeCount,
                    presentModesPtr);
        }

        return details;
    }

    private struct SwapchainSupportDetails
    {
        public SurfaceCapabilitiesKHR Capabilities;
        public SurfaceFormatKHR[] Formats;
        public PresentModeKHR[] PresentModes;
    }

    private void CreateLogicalDevice()
    {
        var indices = FindQueueFamilies(_physicalDevice);
        _graphicsFamily = indices.GraphicsFamily!.Value;
        _presentFamily = indices.PresentFamily!.Value;

        var uniqueQueueFamilies = new HashSet<uint> { _graphicsFamily, _presentFamily };
        var queueCreateInfos = new DeviceQueueCreateInfo[uniqueQueueFamilies.Count];

        var queuePriority = 1.0f;
        var i = 0;
        foreach (var queueFamily in uniqueQueueFamilies)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = queueFamily,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
            i++;
        }

        var deviceFeatures = new PhysicalDeviceFeatures();

        var extensions = stackalloc byte*[] { (byte*)SilkMarshal.StringToPtr(KhrSwapchain.ExtensionName) };

        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)queueCreateInfos.Length,
            PQueueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref queueCreateInfos[0]),
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = 1,
            PpEnabledExtensionNames = extensions
        };

        if (_vk.CreateDevice(_physicalDevice, in createInfo, null, out _device) != Result.Success)
            throw new Exception("Failed to create logical device");

        _vk.GetDeviceQueue(_device, _graphicsFamily, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_device, _presentFamily, 0, out _presentQueue);

        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
            throw new Exception("Failed to get KHR Swapchain extension");
    }

    private void CreateSwapchain()
    {
        var swapchainSupport = QuerySwapchainSupport(_physicalDevice);

        var surfaceFormat = ChooseSwapSurfaceFormat(swapchainSupport.Formats);
        var presentMode = ChooseSwapPresentMode(swapchainSupport.PresentModes);
        var extent = ChooseSwapExtent(swapchainSupport.Capabilities);

        var imageCount = swapchainSupport.Capabilities.MinImageCount + 1;
        if (swapchainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapchainSupport.Capabilities.MaxImageCount)
            imageCount = swapchainSupport.Capabilities.MaxImageCount;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit
        };

        if (_graphicsFamily != _presentFamily)
        {
            var queueFamilyIndices = stackalloc uint[] { _graphicsFamily, _presentFamily };
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;
            createInfo.PQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        createInfo.PreTransform = swapchainSupport.Capabilities.CurrentTransform;
        createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
        createInfo.PresentMode = presentMode;
        createInfo.Clipped = true;

        if (_khrSwapchain.CreateSwapchain(_device, in createInfo, null, out _swapchain) != Result.Success)
            throw new Exception("Failed to create swap chain");

        _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, null);
        _swapchainImages = new Image[imageCount];
        fixed (Image* swapchainImagesPtr = _swapchainImages)
            _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, swapchainImagesPtr);

        _swapchainImageFormat = surfaceFormat.Format;
        _swapchainExtent = extent;
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (var format in availableFormats)
        {
            if (format is { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
                return format;
        }

        return availableFormats[0];
    }

    private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        foreach (var mode in availablePresentModes)
        {
            if (mode == PresentModeKHR.MailboxKhr)
                return mode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        return new Extent2D
        {
            Width = Math.Clamp((uint)_width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Height = Math.Clamp((uint)_height, capabilities.MinImageExtent.Height,
                capabilities.MaxImageExtent.Height)
        };
    }

    private void CreateImageViews()
    {
        if (_swapchainImages == null) return;
        _swapchainImageViews = new ImageView[_swapchainImages.Length];

        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainImageFormat,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (_vk.CreateImageView(_device, in createInfo, null, out _swapchainImageViews[i]) != Result.Success)
                throw new Exception("Failed to create image views");
        }
    }

    private void CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = _swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        var colorAttachmentRef = new AttachmentReference
        {
            Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (_vk.CreateRenderPass(_device, in renderPassInfo, null, out _renderPass) != Result.Success)
            throw new Exception("Failed to create render pass");
    }

    private void CreateDescriptorSetLayout()
    {
        var samplerLayoutBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImmutableSamplers = null,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        var layoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &samplerLayoutBinding
        };

        if (_vk.CreateDescriptorSetLayout(_device, in layoutInfo, null, out _descriptorSetLayout) != Result.Success)
            throw new Exception("Failed to create descriptor set layout");
    }

    private void CreateGraphicsPipeline()
    {
        Console.WriteLine("Creating graphics pipeline...");
        // For simplicity, create minimal shader bytecode inline
        var vertShaderCode = GetShaderSpirv("vert");
        Console.WriteLine($"✓ Vertex shader loaded: {vertShaderCode.Length} bytes");
        var fragShaderCode = GetShaderSpirv("frag");
        Console.WriteLine($"✓ Fragment shader loaded: {fragShaderCode.Length} bytes");

        Console.WriteLine("Creating shader modules...");
        var vertShaderModule = CreateShaderModule(vertShaderCode);
        Console.WriteLine("✓ Vertex shader module created");
        var fragShaderModule = CreateShaderModule(fragShaderCode);
        Console.WriteLine("✓ Fragment shader module created");

        var vertShaderStageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        var fragShaderStageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        var shaderStages = stackalloc PipelineShaderStageCreateInfo[] { vertShaderStageInfo, fragShaderStageInfo };

        // Set up vertex input state to use our vertex buffer
        var bindingDescription = GetBindingDescription();
        var attributeDescriptions = GetAttributeDescriptions();

        // Create pipeline layout and pipeline inside fixed block for safe pointer access
        fixed (VertexInputAttributeDescription* attributePtr = attributeDescriptions)
        {
            // Create pipeline layout first
            var descriptorSetLayout = _descriptorSetLayout;
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &descriptorSetLayout,
                PushConstantRangeCount = 0
            };

            if (_vk.CreatePipelineLayout(_device, in pipelineLayoutInfo, null, out _pipelineLayout) != Result.Success)
                throw new Exception("Failed to create pipeline layout");
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                PVertexAttributeDescriptions = attributePtr
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };

            var viewport = new Viewport
            {
                X = 0.0f,
                Y = 0.0f,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };

            var scissor = new Rect2D { Offset = { X = 0, Y = 0 }, Extent = _swapchainExtent };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = &viewport,
                ScissorCount = 1,
                PScissors = &scissor
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.Clockwise,
                DepthBiasEnable = false
            };

            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask =
                    ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                    ColorComponentFlags.ABit,
                BlendEnable = false
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
                BasePipelineHandle = default
            };

            Console.WriteLine("Creating graphics pipeline...");
            Console.WriteLine($"Pipeline stages: {pipelineInfo.StageCount}");
            Console.WriteLine($"Vertex input bindings: {vertexInputInfo.VertexBindingDescriptionCount}");
            Console.WriteLine($"Vertex input attributes: {vertexInputInfo.VertexAttributeDescriptionCount}");
            Console.WriteLine($"Render pass handle: {_renderPass.Handle}");
            Console.WriteLine($"Pipeline layout handle: {_pipelineLayout.Handle}");

            var result = _vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out _graphicsPipeline);
            Console.WriteLine($"CreateGraphicsPipelines result: {result}");

            if (result != Result.Success)
                throw new Exception($"Failed to create graphics pipeline: {result}");
            Console.WriteLine("✓ Graphics pipeline created successfully");
        }

        Console.WriteLine("Cleaning up shader modules...");
        _vk.DestroyShaderModule(_device, fragShaderModule, null);
        _vk.DestroyShaderModule(_device, vertShaderModule, null);
        Console.WriteLine("✓ Graphics pipeline creation complete");
    }

    private byte[] GetShaderSpirv(string shaderName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Guinevere.Vulkan.SilkNET.Shaders.{shaderName}.spv";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new Exception($"Shader resource '{resourceName}' not found in assembly.");
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load shader '{shaderName}': {ex.Message}");
            throw new Exception($"Failed to load {shaderName} shader SPIR-V resource!");
        }
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        Console.WriteLine($"Creating shader module with {code.Length} bytes");

        // Validate SPIR-V header
        if (code.Length < 20)
        {
            throw new Exception("SPIR-V file too small - invalid format");
        }

        // Check SPIR-V magic number (0x07230203)
        var magic = BitConverter.ToUInt32(code, 0);
        if (magic != 0x07230203)
        {
            Console.WriteLine($"Invalid SPIR-V magic number: 0x{magic:X8}, expected 0x07230203");
            throw new Exception("Invalid SPIR-V file - wrong magic number");
        }

        Console.WriteLine("✓ SPIR-V header validation passed");

        var createInfo = new ShaderModuleCreateInfo
        {
            SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length
        };

        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;

            Console.WriteLine("Calling vkCreateShaderModule...");
            var result = _vk.CreateShaderModule(_device, in createInfo, null, out var shaderModule);
            Console.WriteLine($"vkCreateShaderModule result: {result}");

            if (result != Result.Success)
                throw new Exception($"Failed to create shader module: {result}");

            Console.WriteLine("✓ Shader module created successfully");
            return shaderModule;
        }
    }

    // TODO: not used
    private VertexInputBindingDescription GetBindingDescription() => new()
    {
        Binding = 0,
        Stride = 4 * sizeof(float), // 2 pos + 2 uv
        InputRate = VertexInputRate.Vertex
    };

    // TODO: not used
    private VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        return
        [
            new() { Binding = 0, Location = 0, Format = Format.R32G32Sfloat, Offset = 0 },
            new() { Binding = 0, Location = 1, Format = Format.R32G32Sfloat, Offset = (uint)sizeof(Vector2) }
        ];
    }

    /// <inheritdoc/>
    public void Render(Action<SKCanvas> draw)
    {
        if (_canvas == null || _skiaSurface == null)
        {
            Console.WriteLine("Canvas or Skia surface is null!");
            return;
        }

        // Clear canvas and draw using Skia
        _canvas.Clear(SKColors.Transparent); // Use transparent background
        draw(_canvas);
        _canvas.Flush();
        _skiaSurface.Flush();

        // Upload Skia surface to Vulkan texture and render
        try
        {
            UploadSkiaToVulkan();
            RenderToVulkan();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in rendering pipeline: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void UploadSkiaToVulkan()
    {
        try
        {
            if (_skiaSurface == null || _textureImage.Handle == 0)
            {
                Console.WriteLine("ERROR: Skia surface or texture image is null/invalid!");
                return;
            }

            // Get pixel data from Skia surface
            var pixmap = _skiaSurface.PeekPixels();
            if (pixmap == null)
            {
                Console.WriteLine("ERROR: Failed to get pixmap from Skia surface!");
                return;
            }

            var pixelData = pixmap.GetPixels();
            var dataSize = (uint)(pixmap.Width * pixmap.Height * 4); // RGBA

            // Create the staging buffer
            CreateBuffer(dataSize, BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                out var stagingBuffer, out var stagingBufferMemory);

            // Copy pixel data to staging buffer
            void* data;
            var result = _vk.MapMemory(_device, stagingBufferMemory, 0, dataSize, 0, &data);
            if (result != Result.Success)
            {
                Console.WriteLine($"ERROR: Failed to map staging buffer memory: {result}");
                return;
            }

            // Make the image upside down because Vulkan uses a different coordinate system
            var src = (byte*)pixelData.ToPointer();
            var dst = (byte*)data;
            for (var y = 0; y < pixmap.Height; y++)
            {
                var srcRow = src + (pixmap.Height - 1 - y) * pixmap.RowBytes;
                Buffer.MemoryCopy(srcRow, dst + y * pixmap.RowBytes, pixmap.RowBytes, pixmap.RowBytes);
            }

            // Copy pixel data with proper dimensions from Skia surface
            _vk.UnmapMemory(_device, stagingBufferMemory);

            // Transition texture from shader read-only to transfer destination
            TransitionImageLayout(_textureImage, Format.B8G8R8A8Srgb, ImageLayout.ShaderReadOnlyOptimal,
                ImageLayout.TransferDstOptimal);

            // Copy staging buffer to texture - use current dimensions
            CopyBufferToImage(stagingBuffer, _textureImage, (uint)_width, (uint)_height);

            // Transition texture back to shader read-only
            TransitionImageLayout(_textureImage, Format.B8G8R8A8Srgb, ImageLayout.TransferDstOptimal,
                ImageLayout.ShaderReadOnlyOptimal);

            // Cleanup staging buffer
            _vk.DestroyBuffer(_device, stagingBuffer, null);
            _vk.FreeMemory(_device, stagingBufferMemory, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in UploadSkiaToVulkan: {ex.Message}");
            throw;
        }
    }

    private void CreateBuffer(uint size, BufferUsageFlags usage, MemoryPropertyFlags properties, out VkBuffer buffer,
        out DeviceMemory bufferMemory)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive,
        };

        if (_vk.CreateBuffer(_device, in bufferInfo, null, out buffer) != Result.Success)
        {
            throw new Exception("Failed to create buffer!");
        }

        _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
        };

        if (_vk.AllocateMemory(_device, in allocInfo, null, out bufferMemory) != Result.Success)
        {
            throw new Exception("Failed to allocate buffer memory!");
        }

        _vk.BindBufferMemory(_device, buffer, bufferMemory, 0);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 &&
                (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new Exception("Failed to find suitable memory type!");
    }

    private void TransitionImageLayout(Image image, Format _, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var commandBuffer = BeginSingleTimeCommands();

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            }
        };

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else if (oldLayout == ImageLayout.ShaderReadOnlyOptimal && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.ShaderReadBit;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            sourceStage = PipelineStageFlags.FragmentShaderBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else
        {
            throw new Exception("Unsupported layout transition!");
        }

        _vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, barrier);

        EndSingleTimeCommands(commandBuffer);
    }

    private void CopyBufferToImage(VkBuffer buffer, Image image, uint width, uint height)
    {
        var commandBuffer = BeginSingleTimeCommands();

        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit, MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1,
            },
            ImageOffset = new Offset3D { X = 0, Y = 0, Z = 0 },
            ImageExtent = new Extent3D { Width = width, Height = height, Depth = 1 },
        };

        _vk.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, region);

        EndSingleTimeCommands(commandBuffer);
    }

    private CommandBuffer BeginSingleTimeCommands()
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = _commandPool,
            CommandBufferCount = 1,
        };

        _vk.AllocateCommandBuffers(_device, in allocInfo, out var commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        return commandBuffer;
    }

    private void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        _vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &commandBuffer,
        };

        _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, default);
        _vk.QueueWaitIdle(_graphicsQueue);

        _vk.FreeCommandBuffers(_device, _commandPool, 1, commandBuffer);
    }

    private void CreateTextureImage()
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D { Width = (uint)_width, Height = (uint)_height, Depth = 1, },
            MipLevels = 1,
            ArrayLayers = 1,
            Format = Format.B8G8R8A8Srgb,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit,
        };

        if (_vk.CreateImage(_device, in imageInfo, null, out _textureImage) != Result.Success)
        {
            throw new Exception("Failed to create texture image!");
        }

        _vk.GetImageMemoryRequirements(_device, _textureImage, out var memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        if (_vk.AllocateMemory(_device, in allocInfo, null, out _textureImageMemory) != Result.Success)
        {
            throw new Exception("Failed to allocate texture image memory!");
        }

        _vk.BindImageMemory(_device, _textureImage, _textureImageMemory, 0);

        // Initialize texture with clear data
        InitializeTextureWithClearData();
    }

    private void InitializeTextureWithClearData()
    {
        var dataSize = (uint)(_width * _height * 4); // RGBA
        var clearData = new byte[dataSize];

        // Create staging buffer
        CreateBuffer(dataSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out var stagingBuffer, out var stagingBufferMemory);

        // Copy clear data to staging buffer
        void* data;
        _vk.MapMemory(_device, stagingBufferMemory, 0, dataSize, 0, &data);
        fixed (byte* clearDataPtr = clearData)
        {
            Buffer.MemoryCopy(clearDataPtr, data, dataSize, dataSize);
        }

        _vk.UnmapMemory(_device, stagingBufferMemory);

        // Transition texture to transfer destination
        TransitionImageLayout(_textureImage, Format.B8G8R8A8Srgb, ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal);

        // Copy staging buffer to texture
        CopyBufferToImage(stagingBuffer, _textureImage, (uint)_width, (uint)_height);

        // Transition texture to shader read-only
        TransitionImageLayout(_textureImage, Format.B8G8R8A8Srgb, ImageLayout.TransferDstOptimal,
            ImageLayout.ShaderReadOnlyOptimal);

        // Cleanup staging buffer
        _vk.DestroyBuffer(_device, stagingBuffer, null);
        _vk.FreeMemory(_device, stagingBufferMemory, null);
    }

    private void CreateTextureImageView()
    {
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _textureImage,
            ViewType = ImageViewType.Type2D,
            Format = Format.B8G8R8A8Srgb,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            }
        };

        if (_vk.CreateImageView(_device, in viewInfo, null, out _textureImageView) != Result.Success)
        {
            throw new Exception("Failed to create texture image view!");
        }
    }

    private void CreateTextureSampler()
    {
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = false,
            MaxAnisotropy = 1.0f,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f,
        };

        if (_vk.CreateSampler(_device, in samplerInfo, null, out _textureSampler) != Result.Success)
        {
            throw new Exception("Failed to create texture sampler!");
        }
    }

    private void CreateVertexBuffer()
    {
        var bufferSize = (uint)(_vertexData.Length * sizeof(float));

        CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out var stagingBuffer, out var stagingBufferMemory);

        void* data;
        _vk.MapMemory(_device, stagingBufferMemory, 0, bufferSize, 0, &data);
        fixed (float* vertexPtr = _vertexData)
        {
            Buffer.MemoryCopy(vertexPtr, data, bufferSize, bufferSize);
        }

        _vk.UnmapMemory(_device, stagingBufferMemory);

        CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit, out _vertexBuffer, out _);

        CopyBuffer(stagingBuffer, _vertexBuffer, bufferSize);

        _vk.DestroyBuffer(_device, stagingBuffer, null);
        _vk.FreeMemory(_device, stagingBufferMemory, null);
    }

    private void CreateIndexBuffer()
    {
        var bufferSize = (uint)(sizeof(ushort) * _indices.Length);

        CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out var stagingBuffer, out var stagingBufferMemory);

        void* data;
        _vk.MapMemory(_device, stagingBufferMemory, 0, bufferSize, 0, &data);
        fixed (ushort* indicesPtr = _indices)
        {
            Buffer.MemoryCopy(indicesPtr, data, bufferSize, bufferSize);
        }

        _vk.UnmapMemory(_device, stagingBufferMemory);

        CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit, out _indexBuffer, out _);

        CopyBuffer(stagingBuffer, _indexBuffer, bufferSize);

        _vk.DestroyBuffer(_device, stagingBuffer, null);
        _vk.FreeMemory(_device, stagingBufferMemory, null);
    }

    private void CopyBuffer(VkBuffer srcBuffer, VkBuffer dstBuffer, uint size)
    {
        var commandBuffer = BeginSingleTimeCommands();

        BufferCopy copyRegion = new() { Size = size, };

        _vk.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, copyRegion);

        EndSingleTimeCommands(commandBuffer);
    }

    private void CreateDescriptorPool()
    {
        DescriptorPoolSize poolSize = new()
        {
            Type = DescriptorType.CombinedImageSampler, DescriptorCount = MaxFramesInFlight,
        };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
            MaxSets = MaxFramesInFlight,
        };

        if (_vk.CreateDescriptorPool(_device, in poolInfo, null, out _descriptorPool) != Result.Success)
        {
            throw new Exception("Failed to create descriptor pool!");
        }
    }

    private void CreateDescriptorSets()
    {
        _descriptorSets = new DescriptorSet[MaxFramesInFlight];

        var layouts = stackalloc DescriptorSetLayout[MaxFramesInFlight];
        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            layouts[i] = _descriptorSetLayout;
        }

        DescriptorSetAllocateInfo allocInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = MaxFramesInFlight,
            PSetLayouts = layouts,
        };

        fixed (DescriptorSet* descriptorSetsPtr = _descriptorSets)
        {
            if (_vk.AllocateDescriptorSets(_device, in allocInfo, descriptorSetsPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate descriptor sets!");
            }
        }

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            DescriptorImageInfo imageInfo = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = _textureImageView,
                Sampler = _textureSampler,
            };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descriptorSets[i],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &imageInfo,
            };

            _vk.UpdateDescriptorSets(_device, 1, descriptorWrite, 0, null);
        }
    }

    private void RenderToVulkan()
    {
        try
        {
            // Simple minimal rendering to present something to the swapchain
            uint imageIndex = 0;
            var result = _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue,
                _imageAvailableSemaphores[_currentFrame], default, &imageIndex);

            if (result == Result.ErrorOutOfDateKhr)
            {
                Console.WriteLine("Swapchain out of date, skipping frame");
                return;
            }
            else if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                Console.WriteLine($"ERROR: Failed to acquire swapchain image: {result}");
                return;
            }

            // Wait for the previous frame
            var fence = _inFlightFences[_currentFrame];
            var waitResult = _vk.WaitForFences(_device, 1, in fence, true, ulong.MaxValue);
            if (waitResult != Result.Success)
            {
                Console.WriteLine($"ERROR: Failed to wait for fence: {waitResult}");
                return;
            }

            var resetResult = _vk.ResetFences(_device, 1, in fence);
            if (resetResult != Result.Success)
            {
                Console.WriteLine($"ERROR: Failed to reset fence: {resetResult}");
                return;
            }

            // Submit a simple command buffer that just clears the screen
            var resetCmdResult = _vk.ResetCommandBuffer(_commandBuffers[_currentFrame], 0);
            if (resetCmdResult != Result.Success)
            {
                Console.WriteLine($"ERROR: Failed to reset command buffer: {resetCmdResult}");
                return;
            }

            RecordCommandBuffer(_commandBuffers[_currentFrame], imageIndex);

            var waitSemaphore = _imageAvailableSemaphores[_currentFrame];
            var signalSemaphore = _renderFinishedSemaphores[_currentFrame];
            var commandBuffer = _commandBuffers[_currentFrame];
            var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &waitSemaphore,
                PWaitDstStageMask = &waitStage,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = &signalSemaphore,
            };

            var submitResult = _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]);
            if (submitResult != Result.Success)
            {
                Console.WriteLine($"ERROR: Failed to submit command buffer: {submitResult}");
                return;
            }

            // Present the image
            var swapchain = _swapchain;
            var presentSemaphore = _renderFinishedSemaphores[_currentFrame];

            PresentInfoKHR presentInfo = new()
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &presentSemaphore,
                SwapchainCount = 1,
                PSwapchains = &swapchain,
                PImageIndices = &imageIndex,
            };

            var presentResult = _khrSwapchain.QueuePresent(_presentQueue, in presentInfo);
            if (presentResult != Result.Success && presentResult != Result.SuboptimalKhr)
            {
                Console.WriteLine($"ERROR: Failed to present: {presentResult}");
                return;
            }

            _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in RenderToVulkan: {ex.Message}");
            throw;
        }
    }

    private void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex)
    {
        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo, };

        _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        var clearValue = new ClearValue { Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f), };

        RenderPassBeginInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D { Offset = new Offset2D { X = 0, Y = 0 }, Extent = _swapchainExtent, },
            ClearValueCount = 1,
            PClearValues = &clearValue,
        };

        _vk.CmdBeginRenderPass(commandBuffer, in renderPassInfo, SubpassContents.Inline);

        // Bind graphics pipeline
        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);

        // Bind vertex buffer
        var vertexBuffers = stackalloc VkBuffer[] { _vertexBuffer };
        var offsets = stackalloc ulong[] { 0 };
        _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffers, offsets);

        // Bind index buffer
        _vk.CmdBindIndexBuffer(commandBuffer, _indexBuffer, 0, IndexType.Uint16);

        // Bind descriptor sets
        var descriptorSets = stackalloc DescriptorSet[] { _descriptorSets[_currentFrame] };
        _vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, descriptorSets, 0,
            null);

        // Draw indexed quad
        _vk.CmdDrawIndexed(commandBuffer, (uint)_indices.Length, 1, 0, 0, 0);

        _vk.CmdEndRenderPass(commandBuffer);
        _vk.EndCommandBuffer(commandBuffer);
    }

    private void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[_swapchainImageViews.Length];

        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachment = _swapchainImageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1,
            };

            if (_vk.CreateFramebuffer(_device, in framebufferInfo, null, out _framebuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to create framebuffer!");
            }
        }
    }

    private void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _graphicsFamily,
        };

        if (_vk.CreateCommandPool(_device, in poolInfo, null, out _commandPool) != Result.Success)
        {
            throw new Exception("Failed to create command pool!");
        }
    }

    private void CreateCommandBuffers()
    {
        _commandBuffers = new CommandBuffer[MaxFramesInFlight];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = MaxFramesInFlight,
        };

        fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
        {
            if (_vk.AllocateCommandBuffers(_device, in allocInfo, commandBuffersPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate command buffers!");
            }
        }
    }

    private void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new VkSemaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new VkSemaphore[MaxFramesInFlight];
        _inFlightFences = new Fence[MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo, };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit,
        };

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            if (_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailableSemaphores[i]) !=
                Result.Success ||
                _vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinishedSemaphores[i]) !=
                Result.Success ||
                _vk.CreateFence(_device, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new Exception("Failed to create synchronization objects for a frame!");
            }
        }
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == _width && height == _height))
            return;

        try
        {
            // Wait for all operations to complete
            _vk.DeviceWaitIdle(_device);

            // var oldWidth = _width;
            // var oldHeight = _height;
            // _width = width;
            // _height = height;

            // Recreate Skia surface first (safer)
            _skiaSurface?.Dispose();

            // Clean up swapchain resources in proper order
            CleanupSwapchain();
            Initialize(width, height, _window, false);

            // try
            // {
            //     // Recreate resources in correct order
            //     InitializeVulkan(false);
            //     InitializeSkia();
            // }
            // catch (Exception createEx)
            // {
            //     Console.WriteLine($"Error recreating Vulkan resources: {createEx.Message}");
            //     // Restore old dimensions and try again
            //     _width = oldWidth;
            //     _height = oldHeight;
            //     throw;
            // }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error during Vulkan resize: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void CleanupSwapchain()
    {
        try
        {
            // Wait for device idle before cleanup
            _vk.DeviceWaitIdle(_device);

            // Clean up framebuffers first
            if (_framebuffers != null)
            {
                for (var i = 0; i < _framebuffers.Length; i++)
                {
                    if (_framebuffers[i].Handle != 0)
                    {
                        _vk.DestroyFramebuffer(_device, _framebuffers[i], null);
                        _framebuffers[i] = default;
                    }
                }

                _framebuffers = null;
            }

            // Clean up image views
            if (_swapchainImageViews != null)
            {
                for (var i = 0; i < _swapchainImageViews.Length; i++)
                {
                    if (_swapchainImageViews[i].Handle != 0)
                    {
                        _vk.DestroyImageView(_device, _swapchainImageViews[i], null);
                        _swapchainImageViews[i] = default;
                    }
                }

                _swapchainImageViews = null;
            }

            // Clean up swapchain last
            if (_swapchain.Handle != 0)
            {
                _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
                _swapchain = default;
            }

            // Clear the images array (we don't own these, they're owned by swapchain)
            _swapchainImages = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during swapchain cleanup: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _vk.DeviceWaitIdle(_device);

        // Clean up Vulkan resources
        if (_device.Handle != 0)
        {
            _vk.DestroyDevice(_device, null);
        }

        if (_instance.Handle != 0)
        {
            _khrSurface.DestroySurface(_instance, _surface, null);
            _vk.DestroyInstance(_instance, null);
        }

        _canvas = null;
        _skiaSurface?.Dispose();
        _vk.Dispose();
    }
}
