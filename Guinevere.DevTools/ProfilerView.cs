using MASS4.Profiling;

namespace Guinevere.DevTools;

/// <summary>Dockable capture viewer with its own GUI, clock, input route and PSS appearance.</summary>
public sealed partial class ProfilerView : IDisposable
{
    static readonly string[] Panels = ["sources", "timeline", "preview", "layout", "interactables", "styles", "draw", "text", "state",
        "input", "memory", "diagnostics"];
    readonly ProfileSession _session;
    readonly Dictionary<Guid, ProfilerTarget> _targets = [];
    readonly Queue<(string Name, string? Key)> _commands = [];
    readonly Dictionary<string, TreeViewState> _trees = [];
    IReadOnlyList<ProfileSource> _sources = [];
    IReadOnlyList<ProfileCapture> _history = [];
    Guid _sourceId;
    StyleSheet _skin;
    string? _selectedText;
    bool _disposed;

    /// <summary>Creates a viewer for live or imported captures without changing any target GUI's sheets.</summary>
    /// <param name="session">Shared producer session, owned by the host.</param>
    /// <param name="fileDialog">Optional host-customized file dialog; any open request closes on viewer disposal.</param>
    public ProfilerView(ProfileSession session, FileDialogState? fileDialog = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _files = fileDialog ?? new FileDialogState();
        Gui = new Gui();
        _skin = ProfilerSkins.Dark;
        ExcaliburStyles.Ensure(Gui);
        Gui.StyleSheets.Add(_skin);
        Layout = new DockLayout
        {
            Root = new DockSplit(Axis.Horizontal, new DockLeaf("sources"),
                new DockSplit(Axis.Vertical, new DockLeaf("timeline"),
                    new DockLeaf("preview", "layout", "interactables", "styles", "draw", "text", "state", "input", "memory", "diagnostics")), .25f)
        };
        Layout.Activate(ActivePanel);
    }

    /// <summary>Independent GUI to give to a native window or embedded host.</summary>
    public Gui Gui { get; }
    /// <summary>Docking layout; persist through DockLayout.ToJson/FromJson.</summary>
    public DockLayout Layout { get; private set; }
    /// <summary>Owned selected capture, including after eviction, detach or import.</summary>
    public ProfileCapture? Capture { get; private set; }
    /// <summary>Selected producer-local layout identity, qualified by the selected capture.</summary>
    public string? SelectedObject { get; private set; }
    /// <summary>Panel addressed by keyboard docking commands.</summary>
    public string ActivePanel { get; private set; } = "layout";
    /// <summary>Whether incoming frames replace the selected capture.</summary>
    public bool FollowLatest { get; set; } = true;
    /// <summary>Latest rejected command or import diagnostic.</summary>
    public string? Message { get; private set; }

    /// <summary>Registers a live target without modifying its application draw callback.</summary>
    public ProfilerTarget Attach(Gui target, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(target, Gui))
        {
            throw new ArgumentException("The viewer cannot profile itself as an application target.", nameof(target));
        }
        var registration = new ProfilerTarget(target, _session, name);
        _targets.Add(registration.Profiler.Source.Info.Id, registration);
        return registration;
    }

    /// <summary>Releases a target without invalidating its already selected capture.</summary>
    public bool Detach(Guid sourceId)
    {
        if (!_targets.Remove(sourceId, out var target))
        {
            return false;
        }
        if (_sourceId == sourceId)
        {
            FollowLatest = false;
        }
        target.Dispose();
        return true;
    }

    /// <summary>Queues a command for the next build boundary, keeping both render passes consistent.</summary>
    public void Command(string name, string? key = null)
    {
        if (_commands.Count >= 256)
        {
            Message = "Viewer command queue is full.";
            return;
        }
        _commands.Enqueue((name, key));
        Gui.RequestFrame();
    }

    /// <summary>Selects an owned imported or historical capture without looking at live current nodes.</summary>
    public void SelectCapture(ProfileCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Capture = capture;
        _pinnedFrame = null;
        _sourceId = capture.Source.Id;
        FollowLatest = false;
        SelectedObject = null;
        _trees.Clear();
    }

    /// <summary>Reads an AOT-safe capture using the shared schema.</summary>
    public void Import(Stream stream) => SelectCapture(ProfileExport.Read(stream));

    /// <summary>Writes the selected capture using the shared schema.</summary>
    public void Export(Stream stream)
    {
        if (Capture is null)
        {
            throw new InvalidOperationException("No capture is selected.");
        }
        ProfileExport.Write(stream, Capture);
    }

    /// <summary>Replaces the viewer skin with a built-in or consumer-supplied PSS sheet.</summary>
    public void SetSkin(StyleSheet skin)
    {
        ArgumentNullException.ThrowIfNull(skin);
        var index = Gui.StyleSheets.IndexOf(_skin);
        if (index >= 0)
        {
            Gui.StyleSheets[index] = skin;
        }
        else
        {
            Gui.StyleSheets.Add(skin);
        }
        _skin = skin;
        Gui.RequestFrame();
    }

    /// <summary>Restores a validated docking layout and removes unavailable panel names.</summary>
    public bool RestoreLayout(string json)
    {
        if (DockLayout.FromJson(json) is not { } layout)
        {
            return false;
        }
        layout.RemoveUnknown(Panels);
        Layout = layout;
        return true;
    }

    /// <summary>Application callback for this viewer GUI; call at the same site in both passes.</summary>
    public void Draw()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Gui.Pass == Pass.Pass1Build)
        {
            ReadActions();
            while (_commands.TryDequeue(out var command))
            {
                ApplyCommand(command.Name, command.Key);
            }
            Refresh();
        }
        using (Gui.StyledNode("profiler", id: "profiler").Expand().Direction(Axis.Vertical).Enter())
        {
            Toolbar();
            FrameStrip();
            Gui.DockSpace(Layout, static id => new DockPanelInfo(id), DrawPanel, id: "profiler-workspace");
            Gui.FileDialog(_files, id: "profiler-files");
        }
        if (Gui.Pass == Pass.Pass2Render)
        {
            PublishSemantics();
        }
    }

    void Refresh()
    {
        _sources = _session.GetSources();
        if (_sourceId == Guid.Empty && _sources.Count > 0)
        {
            _sourceId = _sources[0].Info.Id;
        }
        _history = _sources.FirstOrDefault(source => source.Info.Id == _sourceId)?.GetHistory() ?? [];
        if (FollowLatest && _history.Count > 0)
        {
            Capture = _history[^1];
            _pinnedFrame = null;
        }
        RecordFrames();
    }

    ProfilerTarget? Target => Capture is { } capture && capture.Session != _session.Id
        ? null : _targets.GetValueOrDefault(_sourceId);

    /// <summary>Detaches all target registrations; callers retain ownership of the shared session.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        foreach (var target in _targets.Values)
        {
            target.Dispose();
        }
        _targets.Clear();
        _commands.Clear();
        _sources = [];
        _history = [];
        _trees.Clear();
        Capture = null;
        _frames.Clear();
        _pinnedFrame = null;
        _captureRecord = default;
        _flameFrame = null;
        _flame = [];
        _files.Close(null);
        _disposed = true;
    }
}
