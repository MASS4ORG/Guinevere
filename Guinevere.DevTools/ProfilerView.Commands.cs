namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    void ApplyCommand(string name, string? key)
    {
        Message = null;
        if (SelectionCommand(name, key) || ControlCommand(name) || ReplayCommand(name) || FileCommand(name, key))
        {
            return;
        }
        WorkspaceCommand(name, key);
    }

    bool SelectionCommand(string name, string? key)
    {
        switch (name)
        {
            case "source": SelectSource(key); break;
            case "frame": SelectFrame(key); break;
            case "object":
                SelectedObject = key;
                if (_trees.TryGetValue("layout", out var tree)) { tree.SelectedId = key; }
                break;
            case "copy":
                if (_selectedText is not null) { Gui.Input.SetClipboardText(_selectedText); }
                break;
            case "live": FollowLatest = true; break;
            default: return false;
        }
        return true;
    }

    bool ControlCommand(string name)
    {
        switch (name)
        {
            case "pause":
                if (Target is { } pause)
                {
                    pause.Controller.Paused = !pause.Controller.Paused;
                    FollowLatest |= !pause.Controller.Paused;
                }
                break;
            case "step": Target?.Controller.Step(); break;
            case "detail":
                if (Target is { } detail) { detail.Profiler.Detailed = !detail.Profiler.Detailed; }
                break;
            case "record":
                if (Target is { } record) { record.Controller.Recording = !record.Controller.Recording; }
                break;
            default: return false;
        }
        return true;
    }

    bool ReplayCommand(string name)
    {
        switch (name)
        {
            case "play": PlayRecording(); break;
            case "stop": Target?.Controller.Stop(); break;
            case "loop": ToggleLoop(); break;
            case "slower":
                if (Target is { } slower) { slower.Controller.TimeScale = Math.Max(.125, slower.Controller.TimeScale / 2); }
                break;
            case "faster":
                if (Target is { } faster) { faster.Controller.TimeScale = Math.Min(8, faster.Controller.TimeScale * 2); }
                break;
            default: return false;
        }
        return true;
    }

    void SelectSource(string? key)
    {
        if (Guid.TryParse(key, out var sourceId) && _sources.Any(source => source.Info.Id == sourceId))
        {
            _sourceId = sourceId;
            SelectedObject = null;
            Capture = null;
            _pinnedFrame = null;
            FollowLatest = true;
            _trees.Clear();
        }
    }

    void PlayRecording()
    {
        if (Target is not { } target) { return; }
        try
        {
            target.Controller.Play(target.Controller.GetRecording());
            target.Controller.Paused = false;
        }
        catch (InvalidOperationException error) { Message = error.Message; }
        catch (ArgumentException error) { Message = error.Message; }
    }

    void ToggleLoop()
    {
        if (Target is not { } target) { return; }
        if (!target.Controller.Loop && target.Controller.ResetTarget is null)
        {
            Message = "Looping requires an application-owned reset/checkpoint.";
            return;
        }
        target.Controller.Loop = !target.Controller.Loop;
    }

    void WorkspaceCommand(string name, string? key)
    {
        switch (name)
        {
            case "panel":
                if (key is not null && Panels.Contains(key))
                {
                    ActivePanel = key;
                    Layout.EnsurePanel(key);
                    Layout.Activate(key);
                }
                break;
            case "next-panel":
                WorkspaceCommand("panel", Panels[(Array.IndexOf(Panels, ActivePanel) + 1) % Panels.Length]);
                break;
            case "float": Layout.Float(ActivePanel, new Rect(40, 100, 520, 360)); break;
            case "dock-left": Layout.DockAtEdge(ActivePanel, DockZone.Left); break;
            case "dock-right": Layout.DockAtEdge(ActivePanel, DockZone.Right); break;
            case "dock-bottom": Layout.DockAtEdge(ActivePanel, DockZone.Bottom); break;
            case "tabs": Layout.DockAtEdge(ActivePanel, DockZone.Center); break;
            case "skin": SetSkin(ReferenceEquals(_skin, ProfilerSkins.Light) ? ProfilerSkins.Dark : ProfilerSkins.Light); break;
            default: Message = $"Unavailable command: {name}"; break;
        }
    }

    void ReadActions()
    {
        if (Gui.Platform.TryGet<IInputHandler>(out _))
        {
            var input = Gui.Input;
            if (input.IsKeyPressed(KeyboardKey.F6)) { Command("next-panel"); }
            if (input.IsKeyPressed(KeyboardKey.F8)) { Command("pause"); }
            if (input.IsKeyPressed(KeyboardKey.F10)) { Command("step"); }
        }
        if (Gui.Platform.TryGet<IAccessibilityActionSource>(out var source))
        {
            for (var i = 0; i < 64 && source!.TryDequeue("profiler", out var action); i++)
            {
                Command(action.Name, action.Key);
            }
        }
    }
}
