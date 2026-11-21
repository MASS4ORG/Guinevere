using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Drives the file dialog's path box the way a user does — keys, clicks and typing over an in-memory tree — and
/// checks where it goes, what it keeps and what it tells the user.
/// </summary>
public sealed class FileDialogPathEditingTests
{
    static InMemoryFileDialogFileSystem Tree() => new InMemoryFileDialogFileSystem()
        .AddFile("docs", "a.txt")
        .AddDirectory("docs", "sub")
        .AddDirectory("Downloads")
        .AddDirectory("music")
        .AddDirectory(".config")
        .AddDirectory("secret")
        .Deny("secret")
        .AddFile("root.txt");

    static async Task<FileDialogRig> OpenAsync(InMemoryFileDialogFileSystem tree,
        FileDialogMode mode = FileDialogMode.OpenFile, string? start = null) =>
        await FileDialogRig.OpenAsync(tree, mode, start);

    [Fact]
    public async Task CtrlLStartsEditingWithTheWholePathSelectedAndFocused()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);

        rig.BeginEditing();

        Assert.Equal(tree.Root, rig.State.EditingPath);
        Assert.True(rig.PathBoxFocused);
        Assert.Equal(tree.Root, rig.PathBox!.SelectedText);
    }

    [Fact]
    public async Task CtrlLWhileEditingSelectsEverythingAgain()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type("x");
        Assert.False(rig.PathBox!.HasSelection);

        rig.BeginEditing();

        Assert.Equal("x", rig.PathBox!.SelectedText);
        Assert.True(rig.State.IsOpen);
    }

    [Fact]
    public async Task TypingASlashInTheListingStartsEditingWithTheCaretAfterIt()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.Click(rig.Find("/row/" + tree.P("docs"))!);

        rig.Type("/");

        Assert.Equal("/", rig.State.EditingPath);
        Assert.True(rig.PathBoxFocused);
        Assert.False(rig.PathBox!.HasSelection);

        rig.Type("tmp");
        Assert.Equal("/tmp", rig.State.EditingPath);
    }

    [Fact]
    public async Task TypingATildeInTheListingStartsEditing()
    {
        using var rig = await OpenAsync(Tree());
        rig.Click(rig.Find("/row/" + rig.FileSystem.P("docs"))!);

        rig.Type("~");

        Assert.Equal("~", rig.State.EditingPath);
    }

    [Fact]
    public async Task TypingASlashInTheNameFieldDoesNotStartEditing()
    {
        using var rig = await OpenAsync(Tree(), FileDialogMode.SaveFile);

        rig.Type("a/b");

        Assert.Null(rig.State.EditingPath);
        Assert.Equal("a/b", rig.State.Name);
    }

    [Fact]
    public async Task ClickingTheEmptyPartOfTheCrumbBarStartsEditing()
    {
        using var rig = await OpenAsync(Tree());
        var toggle = rig.Find("/path-toggle")!;

        rig.ClickAt(new Vector2(toggle.Rect.X - 8f, toggle.Rect.Y + toggle.Rect.H * 0.5f));

        Assert.NotNull(rig.State.EditingPath);
        Assert.True(rig.PathBoxFocused);
    }

    [Fact]
    public async Task ClickingACrumbNavigatesInsteadOfEditing()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree, start: tree.P("docs"));
        var crumb = Walk(rig.Gui.RootNode!).Where(static node => node.StyleTarget?.Type == "crumb").ElementAt(1);

        rig.Click(crumb);

        Assert.Null(rig.State.EditingPath);
        Assert.Equal(tree.Root, rig.State.Browser.CurrentPath);
    }

    [Fact]
    public async Task EnterGoesToTheTypedFolderAndLeavesEditing()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("docs", "sub"));

        rig.Press(KeyboardKey.Enter);

        Assert.Equal(tree.P("docs", "sub"), rig.State.Browser.CurrentPath);
        Assert.Null(rig.State.EditingPath);
        Assert.Null(rig.State.Message);
        Assert.True(rig.State.IsOpen);
    }

    [Fact]
    public async Task ARelativePathWithDotsIsResolvedAgainstTheCurrentFolder()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree, start: tree.P("docs"));
        rig.BeginEditing();
        rig.Type($"\"sub{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}sub\"");

        rig.Press(KeyboardKey.Enter);

        Assert.Equal(tree.P("docs", "sub"), rig.State.Browser.CurrentPath);
    }

    [Fact]
    public async Task TheDoneButtonGoesToTheTypedPath()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("music"));

        rig.Click(rig.Find("/path-toggle")!);

        Assert.Equal(tree.P("music"), rig.State.Browser.CurrentPath);
        Assert.Null(rig.State.EditingPath);
    }

    [Fact]
    public async Task ATypedFileOpensItsFolderAndSelectsIt()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("docs", "a.txt"));

        rig.Press(KeyboardKey.Enter);

        Assert.Equal(tree.P("docs"), rig.State.Browser.CurrentPath);
        Assert.Equal(tree.P("docs", "a.txt"), rig.State.Selected?.FullPath);
        Assert.Null(rig.State.EditingPath);
        Assert.True(rig.State.IsOpen);

        rig.Press(KeyboardKey.Enter);
        Assert.Equal(tree.P("docs", "a.txt"), rig.Completed);
    }

    [Fact]
    public async Task ATypedFileFillsTheNameFieldInASaveDialog()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree, FileDialogMode.SaveFile);
        rig.BeginEditing();
        rig.Type(tree.P("docs", "a.txt"));

        rig.Press(KeyboardKey.Enter);

        Assert.Equal(tree.P("docs"), rig.State.Browser.CurrentPath);
        Assert.Equal("a.txt", rig.State.Name);
    }

    [Fact]
    public async Task AMissingPathIsReportedAndTheTypedTextKept()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("docz"));

        rig.Press(KeyboardKey.Enter);

        Assert.Equal($"'{tree.P("docz")}' does not exist.", rig.State.Message);
        Assert.Equal(tree.P("docz"), rig.State.EditingPath);
        Assert.Equal(tree.Root, rig.State.Browser.CurrentPath);
        Assert.True(rig.PathBoxFocused);

        // The typo can be fixed in place: the caret is still in the box, at the end.
        rig.Press(KeyboardKey.Backspace);
        rig.Type("s");
        rig.Press(KeyboardKey.Enter);

        Assert.Equal(tree.P("docs"), rig.State.Browser.CurrentPath);
        Assert.Null(rig.State.Message);
    }

    [Fact]
    public async Task AMissingPathTypedThenTheDoneButtonKeepsEditingAndFocus()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type("nowhere");

        rig.Click(rig.Find("/path-toggle")!);

        Assert.Equal("nowhere", rig.State.EditingPath);
        Assert.NotNull(rig.State.Message);
        Assert.True(rig.PathBoxFocused);
    }

    [Fact]
    public async Task APathWithoutPermissionIsReportedAndKept()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("secret"));

        rig.Press(KeyboardKey.Enter);

        Assert.Equal($"No permission to open '{tree.P("secret")}'.", rig.State.Message);
        Assert.Equal(tree.P("secret"), rig.State.EditingPath);
        Assert.Equal(tree.Root, rig.State.Browser.CurrentPath);
    }

    [Fact]
    public async Task ATypedPathThatNormalizesToNothingAsksForAPathAndKeepsEditing()
    {
        using var rig = await OpenAsync(Tree());
        rig.BeginEditing();
        rig.Type("   ");

        var navigation = ControlsExtensions.NavigateTypedAsync(rig.State, "   ").AsTask();
        rig.Frames(1);
        await navigation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("Enter a path.", rig.State.Message);
        Assert.NotNull(rig.State.EditingPath);
    }

    [Fact]
    public async Task EscapeDiscardsTheErrorAndTheTypedText()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("secret"));
        rig.Press(KeyboardKey.Enter);

        rig.Press(KeyboardKey.Escape);

        Assert.Null(rig.State.EditingPath);
        Assert.Null(rig.State.Message);
        Assert.Null(rig.State.Browser.Error);
        Assert.True(rig.State.IsOpen);
    }

    [Fact]
    public async Task MovingFocusToTheSearchBoxCancelsTheEdit()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("music"));

        rig.Click(rig.Find("/search")!);

        Assert.Null(rig.State.EditingPath);
        Assert.Equal(tree.Root, rig.State.Browser.CurrentPath);
    }

    [Fact]
    public async Task ClickingAListingRowCancelsTheEdit()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();

        rig.Click(rig.Find("/row/" + tree.P("root.txt"))!);

        Assert.Null(rig.State.EditingPath);
        Assert.Equal(tree.P("root.txt"), rig.State.Selected?.FullPath);
    }

    [Fact]
    public async Task GoingToAnotherFolderByAnotherRouteEndsTheEdit()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type("half-typed");

        await rig.State.Browser.NavigateAsync(tree.P("docs"), TestContext.Current.CancellationToken);
        rig.Frames();

        Assert.Null(rig.State.EditingPath);
    }

    [Fact]
    public async Task AnEditStartedFromTheStateAloneStillTakesFocus()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);

        rig.SetPathText(tree.P("docs"));

        Assert.True(rig.PathBoxFocused);
        Assert.Equal(tree.P("docs"), rig.PathBox!.SelectedText);
    }

    [Fact]
    public async Task TabCompletesAUniqueFolderAndKeepsGoingIntoIt()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("mu"));

        rig.Press(KeyboardKey.Tab);

        var sep = Path.DirectorySeparatorChar;
        Assert.Equal(tree.P("music") + sep, rig.State.EditingPath);
        Assert.True(rig.PathBoxFocused);
        Assert.Equal(rig.State.EditingPath!.Length, rig.PathBox!.CursorPosition);
    }

    [Fact]
    public async Task RepeatedTabCyclesThroughTheMatchesAndShiftTabGoesBack()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("d"));
        var sep = Path.DirectorySeparatorChar;

        rig.Press(KeyboardKey.Tab);
        Assert.Equal(tree.P("docs") + sep, rig.State.EditingPath);
        Assert.Contains("2 folders match", rig.State.Message, StringComparison.Ordinal);

        rig.Press(KeyboardKey.Tab);
        Assert.Equal(tree.P("Downloads") + sep, rig.State.EditingPath);

        rig.Press(KeyboardKey.Tab);
        Assert.Equal(tree.P("docs") + sep, rig.State.EditingPath);

        rig.Press(KeyboardKey.Tab, shift: true);
        Assert.Equal(tree.P("Downloads") + sep, rig.State.EditingPath);
        Assert.True(rig.PathBoxFocused);
    }

    [Fact]
    public async Task TypingAfterACycleStartsANewCompletion()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("d"));
        rig.Press(KeyboardKey.Tab);

        rig.Press(KeyboardKey.Backspace);
        rig.Type("x");
        rig.Press(KeyboardKey.Tab);

        Assert.Equal("No folder starts with 'docsx'.", rig.State.Message);
    }

    [Fact]
    public async Task TabOnAFolderSeparatorListsItsSubfoldersAndHidesDotFolders()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.Root + Path.DirectorySeparatorChar);

        rig.Press(KeyboardKey.Tab);

        var sep = Path.DirectorySeparatorChar;
        Assert.Equal(tree.P("docs") + sep, rig.State.EditingPath);
        Assert.DoesNotContain(".config", rig.State.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TabCompletesRelativeToTheCurrentFolder()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type("mus");

        rig.Press(KeyboardKey.Tab);

        Assert.Equal("music" + Path.DirectorySeparatorChar, rig.State.EditingPath);
    }

    [Fact]
    public async Task TabInAnUnreadableFolderFindsNothingWithoutLeavingTheBox()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("secret") + Path.DirectorySeparatorChar);

        rig.Press(KeyboardKey.Tab);

        Assert.Equal("There are no folders here.", rig.State.Message);
        Assert.True(rig.PathBoxFocused);
    }

    [Fact]
    public async Task TabDoesNotMoveFocusOutOfThePathBox()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.BeginEditing();
        rig.Type(tree.P("zzz"));

        rig.Press(KeyboardKey.Tab);

        Assert.True(rig.PathBoxFocused);
        Assert.NotNull(rig.State.EditingPath);
    }

    [Fact]
    public async Task StateWrittenAfterAnAwaitWaitsForTheUiThread()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree);
        rig.SetPathText(tree.P("docs"));
        var gate = new TaskCompletionSource();
        tree.Gate = gate.Task;
        rig.State.Selected = rig.State.Browser.Entries.First();

        var navigation = ControlsExtensions.NavigateTypedAsync(rig.State, tree.P("docs")).AsTask();
        await Task.Run(gate.SetResult, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // The read finished on another thread, but nothing the UI reads changes until a frame hands it over.
        Assert.False(navigation.IsCompleted);
        Assert.NotNull(rig.State.EditingPath);
        Assert.NotNull(rig.State.Selected);

        rig.Frames(1);
        await navigation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Null(rig.State.EditingPath);
        Assert.Null(rig.State.Selected);
    }

    [Fact]
    public async Task ANewFolderIsSelectedOnlyOnceTheUiThreadHasTheResult()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree, FileDialogMode.SaveFile);

        var created = ControlsExtensions.NewFolderAsync(rig.State).AsTask();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(created.IsCompleted);
        Assert.Null(rig.State.Selected);

        rig.Frames(1);
        await created.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(tree.P("New Folder"), rig.State.Selected?.FullPath);
        Assert.Equal("New Folder", rig.State.Name);
    }

    [Fact]
    public async Task ASaveDialogStartsWithTheNameFieldFocusedSoEnterAcceptsIt()
    {
        var tree = Tree();
        using var rig = await OpenAsync(tree, FileDialogMode.SaveFile);

        rig.Type("new.txt");
        rig.Press(KeyboardKey.Enter);

        Assert.Equal(tree.P("new.txt"), rig.Completed);
    }

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);
}
