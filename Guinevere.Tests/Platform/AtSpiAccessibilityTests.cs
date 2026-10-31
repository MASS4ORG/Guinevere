using System.Security.Cryptography;
using System.Text;
using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Platform;

public sealed class AtSpiAccessibilityTests
{
    const string Root = "/org/a11y/atspi/accessible/root";
    const string Window = "/org/a11y/atspi/accessible/window";

    public static bool NativeEnabled => OperatingSystem.IsLinux()
        && Environment.GetEnvironmentVariable("MASS4_NATIVE_A11Y_TESTS") == "1";

    [Fact(Skip = "Set MASS4_NATIVE_A11Y_TESTS=1 with a reachable Linux accessibility bus.", SkipUnless = nameof(NativeEnabled))]
    public void NativeBusExportsLogicalGraphAndRoutesActionsToTheRealEditor()
    {
        using var harness = new FrameHarness(1100, 700);
        using var bridge = LinuxAccessibility.TryRegister(harness.Gui, "Guinevere native accessibility test",
            static () => new(50, 60));
        Assert.NotNull(bridge);
        using var client = new AtSpiClient(bridge.BusName!);
        var scene = new SandboxScene();
        scene.Canvas.Camera.SetView(new GraphPoint(50_000, 50_000), 1);
        void Draw(Gui gui) => scene.View.Draw(gui, "native-graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var graph = Path("native-graph");
        WaitFor(() => client.Accessible(Window, "GetChildren").Contains(graph, StringComparison.Ordinal));
        Assert.Contains("uint32 75", client.Accessible(Root, "GetRole"), StringComparison.Ordinal);
        Assert.Contains("Guinevere", client.Property(Root, "Application", "ToolkitName"), StringComparison.Ordinal);
        Assert.Contains(Window, client.Accessible(Root, "GetChildAtIndex", "(0,)"), StringComparison.Ordinal);
        Assert.Contains("uint32 6", client.Accessible(graph, "GetRole"), StringComparison.Ordinal);
        var node = scene.View.Editor.Document.Nodes.First();
        var path = Path("native-graph/graph/" + node.Id);
        Assert.Contains("Number", client.Property(path, "Accessible", "Name"), StringComparison.Ordinal);
        Assert.Contains("Node", client.Property(path, "Accessible", "Description"), StringComparison.Ordinal);
        Assert.Contains("true", client.Accessible(path, "GetAttributes"), StringComparison.Ordinal);
        Assert.Contains("uint32 7", client.Accessible(path, "GetRelationSet"), StringComparison.Ordinal);
        Assert.Contains(Root, client.Accessible(path, "GetApplication"), StringComparison.Ordinal);
        Assert.Contains("Component", client.Accessible(path, "GetInterfaces"), StringComparison.Ordinal);
        Assert.Contains("true", client.Call(path, "org.a11y.atspi.Component", "GrabFocus"), StringComparison.Ordinal);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(node.Id.ToString(), scene.Canvas.Navigation.FocusedKey);
        Assert.Contains(scene.Canvas.VisibleElements, e => e.Key == node.Id.ToString());
        Act(client, path, 1);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Contains(node.Id.ToString(), scene.Canvas.Navigation.SelectedKeys);
        var before = scene.View.Editor.Document.Nodes.Count;
        Act(client, path, 3);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(before - 1, scene.View.Editor.Document.Nodes.Count);
        WaitFor(() => !client.Accessible(graph, "GetChildren").Contains(path, StringComparison.Ordinal));
        var actions = client.Call(graph, "org.a11y.atspi.Action", "GetActions");
        Assert.Contains("Undo", actions, StringComparison.Ordinal);
        var undo = actions.Split("('", StringSplitOptions.None).Skip(1)
            .Select(static s => s[..s.IndexOf('\'', StringComparison.Ordinal)]).ToList().IndexOf("Undo");
        Act(client, graph, undo);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(before, scene.View.Editor.Document.Nodes.Count);
        Assert.Contains("false", client.Call(graph, "org.a11y.atspi.Action", "DoAction", "(-1,)"), StringComparison.Ordinal);
    }

    static void Act(AtSpiClient client, string path, int index) => Assert.Contains("true",
        client.Call(path, "org.a11y.atspi.Action", "DoAction", $"({index},)"), StringComparison.Ordinal);

    static string Path(string id) => "/org/a11y/atspi/accessible/e" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));

    static void WaitFor(Func<bool> condition) => Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(3)));
}
