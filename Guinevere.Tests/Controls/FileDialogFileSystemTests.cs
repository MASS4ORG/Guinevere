using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>What a provider gets for free from <see cref="IFileDialogFileSystem"/>: path text handling and completion.</summary>
public sealed class FileDialogFileSystemTests
{
    static InMemoryFileDialogFileSystem Tree() => new InMemoryFileDialogFileSystem()
        .AddDirectory("Docs").AddDirectory("downloads").AddDirectory("music").AddDirectory(".dot").AddFile("data.txt");

    [Fact]
    public async Task CompletionListsMatchingFoldersIgnoringCaseInNameOrder()
    {
        var fake = Tree();
        IFileDialogFileSystem tree = fake;

        var names = await tree.CompleteFoldersAsync(fake.Root, "d", includeHidden: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(["Docs", "downloads"], names);
    }

    [Fact]
    public async Task CompletionLeavesFilesOutAndHidesDotFoldersUnlessAsked()
    {
        var fake = Tree();
        IFileDialogFileSystem tree = fake;

        Assert.Equal(["Docs", "downloads", "music"],
            await tree.CompleteFoldersAsync(fake.Root, "", includeHidden: false, TestContext.Current.CancellationToken));
        Assert.Equal([".dot", "Docs", "downloads", "music"],
            await tree.CompleteFoldersAsync(fake.Root, "", includeHidden: true, TestContext.Current.CancellationToken));
        Assert.Equal([".dot"],
            await tree.CompleteFoldersAsync(fake.Root, ".", includeHidden: false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompletionCanBeCancelled()
    {
        var fake = Tree();
        fake.Gate = new TaskCompletionSource().Task;
        IFileDialogFileSystem tree = fake;
        using var cancel = new CancellationTokenSource();

        var pending = tree.CompleteFoldersAsync(fake.Root, "", includeHidden: false, cancel.Token).AsTask();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public void TheDefaultNormalizationFollowsThePlatformPathRules()
    {
        IFileDialogFileSystem tree = PhysicalFileDialogFileSystem.Instance;
        var folder = Path.GetFullPath(OperatingSystem.IsWindows() ? @"C:\base" : "/base");

        Assert.Equal(Path.Combine(folder, "child"), tree.NormalizePath("  \"child\"  ", folder));
    }
}
