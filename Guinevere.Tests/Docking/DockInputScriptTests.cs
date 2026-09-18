namespace Guinevere.Tests.Docking;

/// <summary>
/// Replays the docking input scripts against <see cref="DockFixture"/>. Each encodes a bug that only
/// appeared under a moving pointer: a cross-group drop building a cycle, a splitter that kept sliding
/// after the pointer stopped, a splitter stealing a drag it never started, and a capture that was
/// never given back.
/// </summary>
public class DockInputScriptTests : IDisposable
{
    readonly string _output = Path.Combine(Path.GetTempPath(), $"gv-dock-script-{Guid.NewGuid():N}");

    /// <summary>Removes the frames the scripts dumped.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    static string ScriptsDirectory => Path.Combine(AppContext.BaseDirectory, "Scripts");

    /// <summary>The script files deployed next to the test binary.</summary>
    public static TheoryData<string> Scripts()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(ScriptsDirectory, "*.json").Order())
            data.Add(Path.GetFileName(path));

        return data;
    }

    /// <summary>Every script plays through without a failed expectation.</summary>
    [Theory]
    [MemberData(nameof(Scripts))]
    public void AScriptPlaysWithoutFailingAnExpectation(string fileName)
    {
        var script = InputScript.FromJson(File.ReadAllText(Path.Combine(ScriptsDirectory, fileName)));
        Assert.NotNull(script);

        Directory.CreateDirectory(_output);
        var (fixture, gui, input) = DockFixture.Create();

        var messages = new List<string>();
        var player = new InputScriptPlayer(input, messages.Add, _output);

        var passed = player.Play(script, gui, fixture.Render);

        Assert.True(passed, $"{fileName} failed:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
    }

    /// <summary>The scripts are deployed with the tests, so the theory is never silently empty.</summary>
    [Fact]
    public void TheScriptsAreDeployedWithTheTests()
    {
        Assert.True(Directory.Exists(ScriptsDirectory), $"missing {ScriptsDirectory}");
        Assert.NotEmpty(Directory.EnumerateFiles(ScriptsDirectory, "*.json"));
    }
}
