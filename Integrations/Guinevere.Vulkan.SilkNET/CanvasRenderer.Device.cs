using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Guinevere;

public unsafe partial class CanvasRenderer
{
    static readonly uint ApiVersion = Vk.Version12;

    Instance _instance;
    PhysicalDevice _physicalDevice;
    Device _device;
    Queue _graphicsQueue;
    Queue _presentQueue;
    SurfaceKHR _surface;
    KhrSurface _khrSurface = null!;
    KhrSwapchain _khrSwapchain = null!;
    uint _graphicsFamily;
    uint _presentFamily;
    string[] _instanceExtensions = [];
    readonly string[] _deviceExtensions = [KhrSwapchain.ExtensionName];

    void CreateInstance()
    {
        var applicationName = SilkMarshal.StringToPtr("Guinevere Vulkan App");
        var engineName = SilkMarshal.StringToPtr("Guinevere");
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)applicationName,
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)engineName,
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = ApiVersion
        };

        _instanceExtensions = RequiredInstanceExtensions();
        var extensions = SilkMarshal.StringArrayToPtr(_instanceExtensions);
        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)_instanceExtensions.Length,
            PpEnabledExtensionNames = (byte**)extensions,
        };

        try
        {
            Check(_vk.CreateInstance(in createInfo, null, out _instance), "create instance");
        }
        finally
        {
            SilkMarshal.Free(extensions);
            SilkMarshal.Free(applicationName);
            SilkMarshal.Free(engineName);
        }

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
            throw new InvalidOperationException("Failed to get KHR Surface extension");
    }

    string[] RequiredInstanceExtensions()
    {
        if (_window.VkSurface == null)
            return ["VK_KHR_surface", "VK_KHR_xlib_surface", "VK_KHR_wayland_surface"];

        var extensions = _window.VkSurface.GetRequiredExtensions(out var count);
        return SilkMarshal.PtrToStringArray((nint)extensions, (int)count);
    }

    void CreateSurface()
    {
        if (_window.VkSurface != null)
        {
            _surface = _window.VkSurface.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
            return;
        }

        if (_window.Native?.X11 is { } x11)
        {
            if (!_vk.TryGetInstanceExtension(_instance, out KhrXlibSurface xlibSurface))
                throw new InvalidOperationException("Failed to get X11 surface extension");
            var createInfo = new XlibSurfaceCreateInfoKHR
            {
                SType = StructureType.XlibSurfaceCreateInfoKhr,
                Dpy = (nint*)x11.Display,
                Window = (nint)x11.Window
            };
            Check(xlibSurface.CreateXlibSurface(_instance, in createInfo, null, out _surface), "create X11 surface");
        }
        else if (_window.Native?.Wayland is { } wayland)
        {
            if (!_vk.TryGetInstanceExtension(_instance, out KhrWaylandSurface waylandSurface))
                throw new InvalidOperationException("Failed to get Wayland surface extension");
            var createInfo = new WaylandSurfaceCreateInfoKHR
            {
                SType = StructureType.WaylandSurfaceCreateInfoKhr,
                Display = (nint*)wayland.Display,
                Surface = (nint*)wayland.Surface
            };
            Check(waylandSurface.CreateWaylandSurface(_instance, in createInfo, null, out _surface),
                "create Wayland surface");
        }
        else
        {
            throw new InvalidOperationException("No supported native window handle available (X11 or Wayland)");
        }
    }

    void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
        if (deviceCount == 0) throw new InvalidOperationException("Failed to find GPUs with Vulkan support");

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);

        foreach (var device in devices)
        {
            if (!IsDeviceSuitable(device)) continue;
            _physicalDevice = device;
            return;
        }

        throw new InvalidOperationException("Failed to find a suitable GPU");
    }

    bool IsDeviceSuitable(PhysicalDevice device)
    {
        if (!FindQueueFamilies(device, out _, out _) || !SupportsDeviceExtensions(device)) return false;
        var support = QuerySwapchainSupport(device);
        return support.Formats.Length > 0 && support.PresentModes.Length > 0;
    }

    bool FindQueueFamilies(PhysicalDevice device, out uint graphics, out uint present)
    {
        uint familyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref familyCount, null);
        var families = new QueueFamilyProperties[familyCount];
        fixed (QueueFamilyProperties* familiesPtr = families)
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref familyCount, familiesPtr);

        uint? graphicsFamily = null, presentFamily = null;
        for (uint i = 0; i < familyCount && (graphicsFamily is null || presentFamily is null); i++)
        {
            if (families[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit)) graphicsFamily ??= i;
            _khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupport);
            if (presentSupport) presentFamily ??= i;
        }

        graphics = graphicsFamily ?? 0;
        present = presentFamily ?? 0;
        return graphicsFamily is not null && presentFamily is not null;
    }

    bool SupportsDeviceExtensions(PhysicalDevice device)
    {
        uint extensionCount = 0;
        _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);
        var available = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* availablePtr = available)
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, availablePtr);

        var required = new HashSet<string>(_deviceExtensions);
        foreach (var extension in available)
            required.Remove(Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)!);
        return required.Count == 0;
    }

    void CreateLogicalDevice()
    {
        FindQueueFamilies(_physicalDevice, out _graphicsFamily, out _presentFamily);
        var families = new HashSet<uint> { _graphicsFamily, _presentFamily }.ToArray();
        var queueInfos = stackalloc DeviceQueueCreateInfo[families.Length];
        var priority = 1.0f;
        for (var i = 0; i < families.Length; i++)
        {
            queueInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = families[i],
                QueueCount = 1,
                PQueuePriorities = &priority
            };
        }

        var features = new PhysicalDeviceFeatures();
        var extensions = SilkMarshal.StringArrayToPtr(_deviceExtensions);
        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)families.Length,
            PQueueCreateInfos = queueInfos,
            PEnabledFeatures = &features,
            EnabledExtensionCount = (uint)_deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)extensions
        };

        try
        {
            Check(_vk.CreateDevice(_physicalDevice, in createInfo, null, out _device), "create logical device");
        }
        finally
        {
            SilkMarshal.Free(extensions);
        }

        _vk.GetDeviceQueue(_device, _graphicsFamily, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_device, _presentFamily, 0, out _presentQueue);
        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
            throw new InvalidOperationException("Failed to get KHR Swapchain extension");
    }
}
