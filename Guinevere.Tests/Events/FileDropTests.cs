using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Events;

/// <summary>Checks native file-drop queues and their delivery against the displayed node tree.</summary>
public sealed class FileDropTests
{
    /// <summary>Native path storage is copied, empty drops are ignored and delivery happens once.</summary>
    [Fact]
    public void QueueCopiesPathsAndPreservesPosition()
    {
        var queue = new FileDropQueue();
        queue.Enqueue([], Vector2.Zero);
        queue.Enqueue(["", " "], Vector2.Zero);
        Assert.False(queue.TryDequeue(out _));
        var paths = new[] { "/first.png", "/second.png" };
        queue.Enqueue(paths, new Vector2(30, 40));
        paths[0] = "/changed.png";
        Assert.True(queue.TryDequeue(out var drop));
        Assert.Equal(["/first.png", "/second.png"], drop.Paths);
        Assert.Equal(new Vector2(30, 40), drop.Position);
        Assert.False(queue.TryDequeue(out _));
    }

    /// <summary>Drops use the displayed hit target, bubble and settle before either new frame pass.</summary>
    [Fact]
    public void DropTargetsDisplayedNodesAtFrameBoundary()
    {
        using var f = new FrameHarness();
        var queue = new FileDropQueue();
        f.Gui.Platform.Register<IFileDropCapability>(queue);
        var calls = new List<string>();
        var delivered = new List<string>();
        void Draw(Gui gui)
        {
            using (gui.Node(200, 150, "folder").Enter())
            {
                gui.On<FileDropEvent>(drop => calls.Add("capture"), capture: true);
                gui.On<FileDropEvent>(drop => calls.Add("bubble"));
                using (gui.Node(100, 100, "child").Enter())
                    gui.On<FileDropEvent>(drop =>
                    {
                        Assert.Equal(Pass.Pass1Build, gui.Pass);
                        Assert.Equal("child", drop.TargetId);
                        delivered.AddRange(drop.Paths);
                        calls.Add("target");
                    });
            }
        }
        f.Frame(Draw);
        queue.Enqueue(["/asset.png"], new Vector2(20, 20));
        Assert.Empty(calls);
        f.Frame(Draw);
        f.Frame(Draw);
        Assert.Equal(["capture", "target", "bubble"], calls);
        Assert.Equal(["/asset.png"], delivered);
    }

    /// <summary>Modal overlays, outside-window positions and propagation stops protect unrelated targets.</summary>
    [Fact]
    public void OverlayAndPropagationControlDelivery()
    {
        using var f = new FrameHarness();
        var queue = new FileDropQueue();
        f.Gui.Platform.Register<IFileDropCapability>(queue);
        var background = 0;
        var foreground = 0;
        void Draw(Gui gui)
        {
            gui.On<FileDropEvent>(_ => background++);
            using (gui.Node(120, 120, "modal").Absolute(0, 0).BlockInput().Enter())
            {
                gui.SetZIndex(10);
                gui.On<FileDropEvent>(drop => { foreground++; drop.StopPropagation(); });
            }
        }
        queue.Enqueue(["/before.png"], Vector2.Zero);
        f.Frame(Draw);
        queue.Enqueue(["/modal.png"], new Vector2(20, 20));
        queue.Enqueue(["/outside.png"], new Vector2(-10));
        f.Frame(Draw);
        Assert.Equal(1, foreground);
        Assert.Equal(0, background);
        Assert.False(queue.TryDequeue(out _));
    }
}
