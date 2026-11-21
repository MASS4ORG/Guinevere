namespace Guinevere.Tests.Controls;

/// <summary>What the path box accepts: quotes, spaces, <c>~</c>, dots and relative paths, and where completion cuts.</summary>
public sealed class FilePathTextTests
{
    static readonly string Base = Path.GetFullPath(OperatingSystem.IsWindows() ? @"C:\base\folder" : "/base/folder");
    static readonly string Home = Path.GetFullPath(OperatingSystem.IsWindows() ? @"C:\home\me" : "/home/me");

    static string Sep(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void BlankTextHasNoPath(string input) => Assert.Equal("", FilePathText.Normalize(input, Base, Home));

    [Theory]
    [InlineData("  child  ")]
    [InlineData("\"child\"")]
    [InlineData("'child'")]
    [InlineData("\"child")]
    [InlineData(" \" child \" ")]
    public void SpacesAndPastedQuotesAreStripped(string input) =>
        Assert.Equal(Path.Combine(Base, "child"), FilePathText.Normalize(input, Base, Home));

    [Theory]
    [InlineData("child/grand", "child/grand")]
    [InlineData("./child", "child")]
    [InlineData("child/../other", "other")]
    [InlineData("..", "..")]
    public void RelativePathsAreResolvedAgainstTheCurrentFolder(string input, string expected) =>
        Assert.Equal(Path.GetFullPath(Sep(expected), Base), FilePathText.Normalize(Sep(input), Base, Home));

    [Fact]
    public void AnAbsolutePathIgnoresTheCurrentFolder() =>
        Assert.Equal(Path.Combine(Home, "x"), FilePathText.Normalize(Path.Combine(Home, "x"), Base, Home));

    [Theory]
    [InlineData("~", "")]
    [InlineData("~/docs", "docs")]
    [InlineData("~/docs/", "docs")]
    public void ATildeStandsForTheHomeFolder(string input, string below) =>
        Assert.Equal(Path.GetFullPath(Sep(below), Home), FilePathText.Normalize(Sep(input), Base, Home));

    [Fact]
    public void ATildeInsideANameIsLeftAlone() =>
        Assert.Equal(Path.Combine(Base, "~backup"), FilePathText.Normalize("~backup", Base, Home));

    [Fact]
    public void ATildeStaysPutWhenThereIsNoHome() =>
        Assert.Equal(Path.Combine(Base, "~"), FilePathText.Normalize("~", Base, ""));

    [Fact]
    public void ATrailingSeparatorIsDroppedButTheRootKeepsOne()
    {
        Assert.Equal(Path.Combine(Base, "child"), FilePathText.Normalize("child" + Path.DirectorySeparatorChar, Base, Home));

        var root = Path.GetPathRoot(Base)!;
        Assert.Equal(root, FilePathText.Normalize(root, Base, Home));
    }

    [Fact]
    public void ABaseThatIsNotAbsoluteFallsBackToTheWorkingFolder() =>
        Assert.Equal(Path.GetFullPath("child"), FilePathText.Normalize("child", "", Home));

    [Fact]
    public void TextThePlatformCannotResolveComesBackTrimmed()
    {
        var result = FilePathText.Normalize("  a\0b  ", Base, Home);

        Assert.Equal("a\0b", result);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("abc", "", "abc")]
    [InlineData("/abc/de", "/abc/", "de")]
    [InlineData("/abc/", "/abc/", "")]
    [InlineData("/", "/", "")]
    public void CompletionCutsAtTheLastSeparator(string text, string head, string prefix)
    {
        var (actualHead, actualPrefix) = FilePathText.SplitLastSegment(Sep(text));

        Assert.Equal(Sep(head), actualHead);
        Assert.Equal(prefix, actualPrefix);
    }

    [Fact]
    public void ALoneTildeCompletesInsideTheHomeFolder()
    {
        var (head, prefix) = FilePathText.SplitLastSegment("~");

        Assert.Equal("~" + Path.DirectorySeparatorChar, head);
        Assert.Equal("", prefix);
    }
}
