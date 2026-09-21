namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the model behind the studio's file dialog: what a directory lists, how it is ordered,
/// filtered and searched, where the history goes, and what it does with a folder or a link it cannot
/// read — a picker that throws on one unreadable entry is worse than no picker.
/// </summary>
public sealed class FileBrowserTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), $"turian-filebrowser-{Guid.NewGuid():N}");
    readonly FileBrowser browser = new();

    /// <summary>A small tree: two folders, three files of different sizes, and a hidden one of each.</summary>
    public FileBrowserTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "beta"));
        Directory.CreateDirectory(Path.Combine(root, "Alpha"));
        Directory.CreateDirectory(Path.Combine(root, ".hidden"));
        File.WriteAllText(Path.Combine(root, "big.png"), new string('x', 5000));
        File.WriteAllText(Path.Combine(root, "mid.PNG"), new string('x', 100));
        File.WriteAllText(Path.Combine(root, "small.txt"), "hi");

        browser.NavigateAsync(root, TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        GC.SuppressFinalize(this);
    }

    string Names() => string.Join(",", browser.Entries.Select(entry => entry.Name));

    /// <summary>Directories lead whatever the column, and hidden entries are left out.</summary>
    [Fact]
    public void DirectoriesLeadAndHiddenEntriesAreLeftOut() =>
        Assert.Equal("Alpha,beta,big.png,mid.PNG,small.txt", Names());

    /// <summary>The hidden toggle brings back dot-files without disturbing the order.</summary>
    [Fact]
    public void TheHiddenToggleBringsBackDotFiles()
    {
        browser.ShowHidden = true;

        Assert.Equal(".hidden,Alpha,beta,big.png,mid.PNG,small.txt", Names());
    }

    /// <summary>Sorting by the column already in use reverses it, which is what clicking it twice does.</summary>
    [Fact]
    public void SortingTheSameColumnTwiceReversesIt()
    {
        browser.SortBy(FileSortColumn.Name);

        Assert.Equal("beta,Alpha,small.txt,mid.PNG,big.png", Names());
    }

    /// <summary>Another column sorts the files by it while the directories stay in front.</summary>
    [Fact]
    public void AnotherColumnStillKeepsDirectoriesInFront()
    {
        browser.SortBy(FileSortColumn.Size);

        Assert.Equal("Alpha,beta,small.txt,mid.PNG,big.png", Names());
    }

    /// <summary>A filter matches extensions case-insensitively and never hides a folder.</summary>
    [Fact]
    public void AFilterMatchesCaseInsensitivelyAndSparesFolders()
    {
        browser.Filter = FileDialogFilter.Of("Images", ".png");

        Assert.Equal("Alpha,beta,big.png,mid.PNG", Names());
    }

    /// <summary>The search box matches any part of a name, regardless of case.</summary>
    [Fact]
    public void SearchMatchesAnyPartOfANameRegardlessOfCase()
    {
        browser.Search = "ALP";

        Assert.Equal("Alpha", Names());
    }

    /// <summary>Back and forward walk the history the way a browser's buttons do.</summary>
    [Fact]
    public async Task BackAndForwardWalkTheHistory()
    {
        var child = Path.Combine(root, "beta");
        await browser.NavigateAsync(child, TestContext.Current.CancellationToken);
        Assert.True(browser.CanGoBack);
        Assert.False(browser.CanGoForward);

        await browser.GoBackAsync(TestContext.Current.CancellationToken);
        Assert.Equal(root, browser.CurrentPath);
        Assert.True(browser.CanGoForward);

        await browser.GoForwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(child, browser.CurrentPath);

        await browser.GoUpAsync(TestContext.Current.CancellationToken);
        Assert.Equal(root, browser.CurrentPath);
    }

    /// <summary>
    /// A file resolves to the folder holding it, which is what lets a dialog open on the path of
    /// something already chosen.
    /// </summary>
    [Fact]
    public async Task AFilePathOpensTheFolderHoldingIt()
    {
        Assert.True(await browser.NavigateAsync(Path.Combine(root, "small.txt"),
            TestContext.Current.CancellationToken));
        Assert.Equal(root, browser.CurrentPath);
    }

    /// <summary>A path that is not there is refused, leaving the listing where it was.</summary>
    [Fact]
    public async Task APathThatIsNotThereIsRefused()
    {
        Assert.False(await browser.NavigateAsync(Path.Combine(root, "nowhere"),
            TestContext.Current.CancellationToken));
        Assert.Equal(root, browser.CurrentPath);
    }

    /// <summary>A new folder appears in the listing; a name already taken or empty is refused.</summary>
    [Fact]
    public async Task CreatingAFolderListsItAndRefusesATakenName()
    {
        Assert.Equal(Path.Combine(root, "Made"),
            await browser.CreateFolderAsync("Made", TestContext.Current.CancellationToken));
        Assert.Contains("Made", Names(), StringComparison.Ordinal);

        Assert.Null(await browser.CreateFolderAsync("Made", TestContext.Current.CancellationToken));
        Assert.NotNull(browser.Error);
        Assert.Null(await browser.CreateFolderAsync(" ", TestContext.Current.CancellationToken));
    }

    /// <summary>The breadcrumbs run from the filesystem root to the directory being shown.</summary>
    [Fact]
    public void BreadcrumbsRunFromTheRootToHere()
    {
        var crumbs = browser.Breadcrumbs();

        Assert.Equal(Path.GetPathRoot(root), crumbs[0].Path);
        Assert.Equal(root, crumbs[^1].Path);
    }

    /// <summary>Sizes are shown in whole units, and something without one shows nothing.</summary>
    [Theory]
    [InlineData(-1L, "")]
    [InlineData(0L, "0 B")]
    [InlineData(999L, "999 B")]
    [InlineData(5000L, "4.9 KB")]
    [InlineData(1048576L, "1 MB")]
    public void SizesAreShownInWholeUnits(long bytes, string expected) =>
        Assert.Equal(expected, FileBrowser.FormatSize(bytes));

    /// <summary>A folder the user cannot read reports the reason instead of throwing.</summary>
    [Fact]
    public async Task AnUnreadableFolderReportsInsteadOfThrowing()
    {
        if (OperatingSystem.IsWindows()) return;

        var locked = Path.Combine(root, "locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);

        try
        {
            Assert.False(await browser.NavigateAsync(locked, TestContext.Current.CancellationToken));

            Assert.NotNull(browser.Error);
        }
        finally
        {
            File.SetUnixFileMode(locked,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// A link with nothing at the other end is listed like any other entry: reading its length is
    /// what throws, and one broken link must not cost the whole listing.
    /// </summary>
    [Fact]
    public async Task ADanglingLinkIsStillListed()
    {
        File.CreateSymbolicLink(Path.Combine(root, "dangling"), Path.Combine(root, "gone"));

        await browser.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Null(browser.Error);
        Assert.Contains("dangling", Names(), StringComparison.Ordinal);
    }

    /// <summary>An injected provider may complete later without blocking the frame that started it.</summary>
    [Fact]
    public async Task NavigationUsesTheInjectedProviderAsynchronously()
    {
        var provider = new DelayedFileSystem();
        var virtualBrowser = new FileBrowser(provider);

        var navigation = virtualBrowser.NavigateAsync("virtual", TestContext.Current.CancellationToken);

        Assert.False(navigation.IsCompleted);
        Assert.True(virtualBrowser.IsLoading);
        provider.Complete([new FileEntry("/virtual/asset.png", "asset.png", false, 42, DateTime.MinValue)]);
        Assert.True(await navigation);
        Assert.Equal("asset.png", Assert.Single(virtualBrowser.Entries).Name);
        Assert.False(virtualBrowser.IsLoading);
    }

    /// <summary>Cancellation is available as an explicit result instead of relying on a null path.</summary>
    [Fact]
    public async Task ClosingReportsAnExplicitCancellationResult()
    {
        FileDialogResult? result = null;
        var state = new FileDialogState();
        await state.OpenAsync(new FileDialogRequest
        {
            Mode = FileDialogMode.OpenFile,
            StartPath = root,
            OnClosed = value => result = value,
        }, TestContext.Current.CancellationToken);

        state.Close(null);

        Assert.True(Assert.IsType<FileDialogResult>(result).WasCancelled);
    }

    sealed class DelayedFileSystem : IFileDialogFileSystem
    {
        readonly TaskCompletionSource<IReadOnlyList<FileEntry>> entries =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(IReadOnlyList<FileEntry> result) => entries.SetResult(result);

        public ValueTask<string?> ResolveDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>("/virtual");

        public ValueTask<IReadOnlyList<FileEntry>> EnumerateAsync(string path,
            CancellationToken cancellationToken = default) => new(entries.Task);

        public ValueTask<string> CreateDirectoryAsync(string parentPath, string name,
            CancellationToken cancellationToken = default) => ValueTask.FromResult($"{parentPath}/{name}");

        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public string? GetParent(string path) => null;

        public IReadOnlyList<(string Label, string Path)> GetBreadcrumbs(string path) => [("virtual", path)];

        public ValueTask<IReadOnlyList<FilePlace>> GetPlacesAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<FilePlace>>([new FilePlace("Virtual", "/virtual")]);
    }
}
