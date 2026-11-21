namespace Guinevere.Tests.Mocks;

/// <summary>
/// A file dialog over an in-memory tree, drawn by a <see cref="FrameHarness"/> with scripted keys, so a test
/// can type into the toolbar and watch what the dialog does with it.
/// </summary>
public sealed class FileDialogRig : IDisposable
{
    readonly FrameHarness _harness = new(1000, 700);

    FileDialogRig(InMemoryFileDialogFileSystem fileSystem)
    {
        FileSystem = fileSystem;
        State = new FileDialogState(fileSystem);
    }

    /// <summary>The tree the dialog browses.</summary>
    public InMemoryFileDialogFileSystem FileSystem { get; }

    /// <summary>The dialog's state.</summary>
    public FileDialogState State { get; }

    /// <summary>The path the request completed with, or null while it has not.</summary>
    public string? Completed { get; private set; }

    /// <summary>Whether the request's callback ran, whatever it was given.</summary>
    public bool Closed { get; private set; }

    /// <summary>The GUI the dialog is drawn into.</summary>
    public Gui Gui => _harness.Gui;

    /// <summary>Opens a dialog over <paramref name="fileSystem"/> and draws it until it has settled.</summary>
    public static async Task<FileDialogRig> OpenAsync(InMemoryFileDialogFileSystem fileSystem,
        FileDialogMode mode = FileDialogMode.OpenFile, string? startPath = null)
    {
        var rig = new FileDialogRig(fileSystem);
        await rig.State.OpenAsync(new FileDialogRequest
        {
            Mode = mode,
            StartPath = startPath ?? fileSystem.Root,
            OnComplete = path => rig.Completed = path,
            OnClosed = _ => rig.Closed = true,
        }, TestContext.Current.CancellationToken);
        rig.Frames();
        return rig;
    }

    void Draw(Gui gui) => gui.FileDialog(State).Width(840).Height(544);

    /// <summary>Draws <paramref name="count"/> frames, which is how posted work and focus changes land.</summary>
    public void Frames(int count = 4)
    {
        for (var i = 0; i < count; i++)
        {
            _harness.Frame(Draw);
        }
    }

    /// <summary>Presses and releases <paramref name="key"/>, holding <paramref name="control"/> when asked.</summary>
    public void Press(KeyboardKey key, bool control = false, bool shift = false)
    {
        if (control)
        {
            _harness.Input.PressKey(KeyboardKey.LeftControl);
        }

        if (shift)
        {
            _harness.Input.PressKey(KeyboardKey.LeftShift);
        }

        _harness.Input.PressKey(key);
        Frames(1);
        _harness.Input.ReleaseKey(key);
        _harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        _harness.Input.ReleaseKey(KeyboardKey.LeftShift);
        Frames(3);
    }

    /// <summary>Types <paramref name="text"/> into whatever has focus.</summary>
    public void Type(string text)
    {
        _harness.Input.TypeText(text);
        Frames(3);
    }

    /// <summary>Clicks the centre of <paramref name="node"/>.</summary>
    public void Click(LayoutNode node)
    {
        _harness.Click(Draw, FrameHarness.Center(node));
        Frames(3);
    }

    /// <summary>Clicks a point of the screen.</summary>
    public void ClickAt(Vector2 point)
    {
        _harness.Click(Draw, point);
        Frames(3);
    }

    /// <summary>The first node whose id ends with <paramref name="suffix"/>, or null.</summary>
    public LayoutNode? Find(string suffix) => Walk(Gui.RootNode!)
        .FirstOrDefault(node => node.Id.EndsWith(suffix, StringComparison.Ordinal));

    /// <summary>The path box's edit state, or null while the bar shows crumbs.</summary>
    public TextEditState? PathBox => Find("/path") is { } node ? Gui.TryGetControlState<TextEditState>(node.Id) : null;

    /// <summary>Whether the path box holds keyboard focus.</summary>
    public bool PathBoxFocused => Find("/path") is { } node && Gui.Focus.HasFocus(node.Id);

    /// <summary>Starts editing the path and draws until the box has focus.</summary>
    public void BeginEditing()
    {
        Press(KeyboardKey.L, control: true);
        Frames();
    }

    /// <summary>Replaces the path box's text with <paramref name="text"/>, as if the user had typed it.</summary>
    public void SetPathText(string text)
    {
        State.EditingPath = text;
        Frames();
    }

    /// <inheritdoc />
    public void Dispose() => _harness.Dispose();

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);
}
