using Guinevere.Profiling;

namespace Guinevere.DevTools;

/// <summary>Owns one live collector/controller pair; disposing detaches it before application code unload.</summary>
public sealed class ProfilerTarget : IDisposable
{
    internal ProfilerTarget(Gui gui, ProfileSession session, string name)
    {
        Profiler = new GuiProfiler(gui, session, name);
        try
        {
            Controller = new GuiFrameController(gui);
        }
        catch
        {
            Profiler.Dispose();
            throw;
        }
    }
    /// <summary>Target collector and copied captures.</summary>
    public GuiProfiler Profiler { get; }
    /// <summary>Target-owned pause, time and input control.</summary>
    public GuiFrameController Controller { get; }
    /// <summary>Releases both registrations and cached presentation resources.</summary>
    public void Dispose()
    {
        Controller.Dispose();
        Profiler.Dispose();
    }
}
