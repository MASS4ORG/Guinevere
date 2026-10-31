using System.Security.Cryptography;
using System.Text;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Platform;

public sealed class AtSpiProtocolTests
{
    const string Root = "/org/a11y/atspi/accessible/root";
    const string Window = "/org/a11y/atspi/accessible/window";
    public static bool NativeEnabled => AtSpiAccessibilityTests.NativeEnabled;

    [Fact(Skip = "Set MASS4_NATIVE_A11Y_TESTS=1 with Linux native libraries.", SkipUnless = nameof(NativeEnabled))]
    public void UnavailableNativeBusLeavesTheHostCapabilityAbsent()
    {
        var gui = new Gui();
        var bridge = LinuxAccessibility.TryRegister(gui, () => new LinuxAccessibility(gui, "Unavailable bus",
            static () => default, true, getAddress: static () => "unix:path=/tmp/guinevere-nonexistent-a11y/socket"));
        Assert.Null(bridge);
        Assert.False(gui.Platform.Supports<IAccessibilityCapability>());
    }

    [Fact(Skip = "Set MASS4_NATIVE_A11Y_TESTS=1 with a reachable Linux accessibility bus.", SkipUnless = nameof(NativeEnabled))]
    public void NativePropertyCallbackContainsInvalidMarshalledArguments()
    {
        using var connection = new AtSpiConnection("Guinevere callback boundary test", static (_, _) => { });
        Assert.Equal(0, connection.OnProperty(0, 0, Root, "org.a11y.atspi.Accessible", null!, 0, 0));
        Assert.Equal(0, connection.OnProperty(0, 0, "/missing", "org.a11y.atspi.Accessible", "Name", 0, 0));
    }

    [Fact(Skip = "Set MASS4_NATIVE_A11Y_TESTS=1 with a reachable Linux accessibility bus.", SkipUnless = nameof(NativeEnabled))]
    public void NativeProtocolPreservesStatesRelationshipsCoordinatesAndStaleElementRemoval()
    {
        using var harness = new FrameHarness();
        using var bridge = LinuxAccessibility.TryRegister(harness.Gui, "Guinevere native protocol test", static () => new(50, 60));
        Assert.NotNull(bridge);
        using var client = new AtSpiClient(bridge.BusName!);
        var flags = ControlVisualState.Focused | ControlVisualState.Selected | ControlVisualState.Checked;
        var element = new AccessibilityElement("item", "parent", "Group", "A 'quoted' \\ label\nλ", "Description\r\t",
            new Rect(10, 20, 100, 30), flags, Actions: ["Focus", "Activate"], ScopeId: "scope", Key: "key",
            SourceId: "source", TargetId: "target", Expanded: false);
        void Publish()
        {
            bridge.BeginFrame();
            bridge.PublishElement(new("parent", null, "Graph", "Parent", "", default, ControlVisualState.None));
            bridge.PublishElement(new("source", null, "Port", "Source", "", default, ControlVisualState.None));
            bridge.PublishElement(new("target", null, "Port", "Target", "", default, ControlVisualState.None));
            bridge.PublishElement(element);
            bridge.Announce("scope", "Operation completed", "item");
            bridge.Announce("missing", "An announcement without an element");
            bridge.EndFrame();
        }
        Publish();
        var path = Path("item");
        WaitFor(() => client.Accessible(Window, "GetChildren").Contains(Path("parent"), StringComparison.Ordinal));
        Assert.Contains("A", client.Property(path, "Accessible", "Name"), StringComparison.Ordinal);
        Assert.Contains("uint32 99", client.Accessible(path, "GetRole"), StringComparison.Ordinal);
        foreach (var method in new[] { "GetRoleName", "GetLocalizedRoleName", "GetState", "GetRelationSet",
                     "GetIndexInParent", "GetChildren", "GetAttributes", "GetApplication", "GetInterfaces" })
        {
            Assert.NotEmpty(client.Accessible(path, method));
        }
        Assert.Contains("(10,", client.Accessible(path, "GetRelationSet"), StringComparison.Ordinal);
        Assert.Contains("(11,", client.Accessible(path, "GetRelationSet"), StringComparison.Ordinal);
        Assert.Contains("uint32 1", client.Property(path, "Accessible", "version"), StringComparison.Ordinal);
        foreach (var property in new[] { "Description", "Parent", "ChildCount", "Locale", "AccessibleId", "HelpText" })
        {
            Assert.NotEmpty(client.Property(path, "Accessible", property));
        }
        foreach (var property in new[] { "Version", "ToolkitVersion", "AtspiVersion", "InterfaceVersion", "Id" })
        {
            Assert.NotEmpty(client.Property(Root, "Application", property));
        }
        Assert.NotEmpty(client.Call(Root, "org.a11y.atspi.Application", "GetLocale", "(uint32 0,)"));
        Assert.NotEmpty(client.Call(Root, "org.a11y.atspi.Application", "GetApplicationBusAddress"));
        Assert.Contains("Description", client.Call("/org/a11y/atspi/cache", "org.a11y.atspi.Cache", "GetItems"), StringComparison.Ordinal);
        Assert.NotEmpty(client.Property("/org/a11y/atspi/cache", "Cache", "version"));
        VerifyComponent(client, path);
        foreach (var method in new[] { "GetName", "GetLocalizedName", "GetDescription", "GetKeyBinding" })
        {
            Assert.NotEmpty(client.Call(path, "org.a11y.atspi.Action", method, "(0,)"));
        }
        Assert.Contains("2", client.Property(path, "Action", "NActions"), StringComparison.Ordinal);
        Assert.Contains("true", client.Call(path, "org.a11y.atspi.Action", "DoAction", "(1,)"), StringComparison.Ordinal);
        Assert.True(bridge.TryDequeue("scope", out _));
        element = element with
        {
            Name = "Changed",
            Description = "Updated",
            ParentId = null,
            Expanded = true,
            State = ControlVisualState.Disabled,
            Virtualized = true
        };
        Publish();
        WaitFor(() => client.Property(path, "Accessible", "Name").Contains("Changed", StringComparison.Ordinal));
        Assert.Contains("false", client.Call(path, "org.a11y.atspi.Action", "DoAction", "(0,)"), StringComparison.Ordinal);
        Assert.Contains("false", client.Call(path, "org.a11y.atspi.Component", "Contains", "(15, 25, uint32 1)"), StringComparison.Ordinal);
        element = element with { State = ControlVisualState.None, Actions = [null!], Description = "Malformed action provider" };
        Publish();
        WaitFor(() => client.Property(path, "Accessible", "Description").Contains("Malformed", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => client.Call(path, "org.a11y.atspi.Action", "GetActions"));
        Assert.Contains("Changed", client.Property(path, "Accessible", "Name"), StringComparison.Ordinal);
        bridge.BeginFrame();
        bridge.EndFrame();
        WaitFor(() => !client.Accessible(Window, "GetChildren").Contains(path, StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => client.Property(path, "Accessible", "Name"));
    }

    static void VerifyComponent(AtSpiClient client, string path)
    {
        Assert.Contains("100", client.Call(path, "org.a11y.atspi.Component", "GetSize"), StringComparison.Ordinal);
        Assert.Contains("60, 80", client.Call(path, "org.a11y.atspi.Component", "GetPosition", "(uint32 0,)"), StringComparison.Ordinal);
        Assert.Contains("10, 20", client.Call(path, "org.a11y.atspi.Component", "GetExtents", "(uint32 1,)"), StringComparison.Ordinal);
        Assert.Contains("true", client.Call(path, "org.a11y.atspi.Component", "Contains", "(15, 25, uint32 1)"), StringComparison.Ordinal);
        Assert.Contains(path, client.Call(Path("parent"), "org.a11y.atspi.Component", "GetAccessibleAtPoint", "(15, 25, uint32 1)"), StringComparison.Ordinal);
        foreach (var method in new[] { "GetLayer", "GetMDIZOrder", "GetAlpha", "GrabFocus" })
        {
            Assert.NotEmpty(client.Call(path, "org.a11y.atspi.Component", method));
        }
        Assert.Contains("true", client.Call(path, "org.a11y.atspi.Component", "ScrollTo", "(uint32 0,)"), StringComparison.Ordinal);
        Assert.Contains("true", client.Call(path, "org.a11y.atspi.Component", "ScrollToPoint", "(uint32 1, 0, 0)"), StringComparison.Ordinal);
        Assert.Contains("false", client.Call(path, "org.a11y.atspi.Component", "SetSize", "(20, 20)"), StringComparison.Ordinal);
        Assert.Contains("false", client.Call(path, "org.a11y.atspi.Component", "SetPosition", "(0, 0, uint32 1)"), StringComparison.Ordinal);
        Assert.Contains("false", client.Call(path, "org.a11y.atspi.Component", "SetExtents", "(0, 0, 20, 20, uint32 1)"), StringComparison.Ordinal);
    }

    static string Path(string id) => "/org/a11y/atspi/accessible/e" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
    static void WaitFor(Func<bool> condition) => Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(3)));
}
