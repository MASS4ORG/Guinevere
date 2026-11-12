namespace Guinevere;

/// <summary>Host-level control of target callbacks, virtual time and cached presentation.</summary>
public interface IGuiFrameController
{
    /// <summary>Accepts a host update without requiring target execution.</summary>
    void UpdateTime(double deltaSeconds);
    /// <summary>Executes or presents one host frame.</summary>
    void RenderFrame(SKCanvas canvas, Action draw);
    /// <summary>Queues a native event in host order, before polling can collapse its edges.</summary>
    void Input(in GuiInputEvent input);
}

/// <summary>Native pointer/keyboard event copied without a window or device reference.</summary>
/// <param name="Kind">move, press, release, key-down, key-up, text or scroll.</param>
/// <param name="Position">Pointer coordinates for move events.</param>
/// <param name="Code">Mouse button or keyboard key code.</param>
/// <param name="Text">Typed characters.</param>
/// <param name="Delta">Scroll delta.</param>
public readonly record struct GuiInputEvent(string Kind, Vector2 Position = default, int Code = 0,
    string? Text = null, float Delta = 0);

public partial class Gui
{
    /// <summary>Optional host gate. Default integrations consult it before callbacks and time updates.</summary>
    public IGuiFrameController? FrameController { get; set; }

    /// <summary>Routes a host time update through the optional frame controller.</summary>
    public void UpdateFrameTime(double deltaSeconds)
    {
        if (FrameController is { } controller)
        {
            controller.UpdateTime(deltaSeconds);
        }
        else
        {
            Time.Update(deltaSeconds);
        }
    }

    /// <summary>Runs the complete two-pass callback through the optional host controller.</summary>
    public void RenderFrame(SKCanvas canvas, Action draw)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(draw);
        if (FrameController is { } controller)
        {
            controller.RenderFrame(canvas, draw);
        }
        else
        {
            ExecuteFrame(canvas, draw);
        }
    }

    internal void ExecuteFrame(SKCanvas canvas, Action draw)
    {
        try
        {
            SetStage(Pass.Pass1Build);
            BeginFrame(canvas);
            draw();
            CalculateLayout();
            SetStage(Pass.Pass2Render);
            draw();
            Render();
            EndFrame();
        }
        catch (Exception exception)
        {
            Canvas = null;
            LayoutNodeScopeStack.Clear();
            FrameObserver?.Failed(exception);
            throw;
        }
    }
}
