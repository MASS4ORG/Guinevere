using Silk.NET.Vulkan;
using SkiaSharp;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Guinevere;

public unsafe partial class CanvasRenderer
{
    readonly VkBuffer[] _rasterBuffers = new VkBuffer[MaxFramesInFlight];
    readonly DeviceMemory[] _rasterMemory = new DeviceMemory[MaxFramesInFlight];
    readonly IntPtr[] _rasterMapped = new IntPtr[MaxFramesInFlight];
    readonly SKSurface?[] _rasterSurfaces = new SKSurface?[MaxFramesInFlight];
    bool _rasterCopies;

    /// <summary>
    /// Creates one persistently mapped host buffer per frame in flight. Skia draws straight into it when the memory is
    /// host-cached; uncached (write-combined) memory is slow to blend in, so Skia then draws into system memory and
    /// the frame is copied over in one sequential write.
    /// </summary>
    void CreateRasterTargets()
    {
        var width = (int)_extent.Width;
        var height = (int)_extent.Height;
        var size = (ulong)width * (ulong)height * 4;
        var info = new SKImageInfo(width, height, _colorType, SKAlphaType.Premul);
        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = BufferUsageFlags.TransferSrcBit,
                SharingMode = SharingMode.Exclusive,
            };
            Check(_vk.CreateBuffer(_device, in bufferInfo, null, out _rasterBuffers[i]), "create staging buffer");
            _vk.GetBufferMemoryRequirements(_device, _rasterBuffers[i], out var requirements);
            var hostVisible = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
            var memoryType = FindMemoryType(requirements.MemoryTypeBits, hostVisible | MemoryPropertyFlags.HostCachedBit)
                             ?? FindMemoryType(requirements.MemoryTypeBits, hostVisible)
                             ?? throw new InvalidOperationException("Failed to find host-visible memory");
            _rasterCopies = !HasProperty(memoryType, MemoryPropertyFlags.HostCachedBit);
            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = memoryType,
            };
            Check(_vk.AllocateMemory(_device, in allocInfo, null, out _rasterMemory[i]), "allocate staging memory");
            _vk.BindBufferMemory(_device, _rasterBuffers[i], _rasterMemory[i], 0);

            void* mapped;
            Check(_vk.MapMemory(_device, _rasterMemory[i], 0, size, 0, &mapped), "map staging memory");
            _rasterMapped[i] = (IntPtr)mapped;
            _rasterSurfaces[i] = _rasterCopies ? SKSurface.Create(info) : SKSurface.Create(info, (IntPtr)mapped, width * 4);
        }
    }

    void DestroyRasterTargets()
    {
        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            _rasterSurfaces[i]?.Dispose();
            _rasterSurfaces[i] = null;
            _rasterMapped[i] = IntPtr.Zero;
            if (_rasterMemory[i].Handle != 0)
            {
                _vk.UnmapMemory(_device, _rasterMemory[i]);
                _vk.FreeMemory(_device, _rasterMemory[i], null);
                _rasterMemory[i] = default;
            }
            if (_rasterBuffers[i].Handle != 0) _vk.DestroyBuffer(_device, _rasterBuffers[i], null);
            _rasterBuffers[i] = default;
        }
    }

    /// <summary>Rasterizes on the CPU, then copies the frame into the acquired image in one submission.</summary>
    void RenderRaster(uint imageIndex, Action<SKCanvas> draw)
    {
        var surface = _rasterSurfaces[_currentFrame]!;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        draw(canvas);
        canvas.Flush();
        if (_rasterCopies && surface.PeekPixels() is { } pixels)
        {
            using (pixels)
            {
                var bytes = (long)pixels.RowBytes * pixels.Height;
                System.Buffer.MemoryCopy((void*)pixels.GetPixels(), (void*)_rasterMapped[_currentFrame], bytes, bytes);
            }
        }

        var image = _swapchainImages[imageIndex];
        var commands = _beginCommands[_currentFrame];
        BeginCommands(commands);
        Barrier(commands, image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
            PipelineStageFlags.TransferBit, 0, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit);
        var region = new BufferImageCopy
        {
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            ImageExtent = new Extent3D(_extent.Width, _extent.Height, 1),
        };
        _vk.CmdCopyBufferToImage(commands, _rasterBuffers[_currentFrame], image, ImageLayout.TransferDstOptimal, 1,
            in region);
        Barrier(commands, image, ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr,
            PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit, PipelineStageFlags.BottomOfPipeBit, 0);
        _vk.EndCommandBuffer(commands);
        Submit(commands, imageIndex, waitForImage: true, signalPresent: true, _inFlight[_currentFrame]);
    }

    uint? FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memory);
        for (var i = 0; i < memory.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << i)) != 0 && (memory.MemoryTypes[i].PropertyFlags & properties) == properties)
                return (uint)i;
        }
        return null;
    }

    bool HasProperty(uint memoryType, MemoryPropertyFlags property)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memory);
        return memory.MemoryTypes[(int)memoryType].PropertyFlags.HasFlag(property);
    }
}
