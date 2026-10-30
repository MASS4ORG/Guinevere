namespace Guinevere;

sealed partial class AtSpiConnection
{
    static string Introspection => "<node>"
        + Interface("Accessible",
            Property("version", "u") + Property("Name", "s") + Property("Description", "s")
            + Property("Parent", "(so)") + Property("ChildCount", "i") + Property("Locale", "s")
            + Property("AccessibleId", "s") + Property("HelpText", "s")
            + MethodXml("GetChildAtIndex", ["i"], ["(so)"])
            + MethodXml("GetChildren", [], ["a(so)"]) + MethodXml("GetIndexInParent", [], ["i"])
            + MethodXml("GetRelationSet", [], ["a(ua(so))"]) + MethodXml("GetRole", [], ["u"])
            + MethodXml("GetRoleName", [], ["s"]) + MethodXml("GetLocalizedRoleName", [], ["s"])
            + MethodXml("GetState", [], ["au"]) + MethodXml("GetAttributes", [], ["a{ss}"])
            + MethodXml("GetApplication", [], ["(so)"]) + MethodXml("GetInterfaces", [], ["as"]))
        + Interface("Action", Property("version", "u") + Property("NActions", "i")
            + MethodXml("GetDescription", ["i"], ["s"]) + MethodXml("GetName", ["i"], ["s"])
            + MethodXml("GetLocalizedName", ["i"], ["s"]) + MethodXml("GetKeyBinding", ["i"], ["s"])
            + MethodXml("GetActions", [], ["a(sss)"]) + MethodXml("DoAction", ["i"], ["b"]))
        + Interface("Component", Property("version", "u")
            + MethodXml("Contains", ["i", "i", "u"], ["b"])
            + MethodXml("GetAccessibleAtPoint", ["i", "i", "u"], ["(so)"])
            + MethodXml("GetExtents", ["u"], ["(iiii)"]) + MethodXml("GetPosition", ["u"], ["i", "i"])
            + MethodXml("GetSize", [], ["i", "i"]) + MethodXml("GetLayer", [], ["u"])
            + MethodXml("GetMDIZOrder", [], ["n"]) + MethodXml("GrabFocus", [], ["b"])
            + MethodXml("GetAlpha", [], ["d"]) + MethodXml("SetExtents", ["i", "i", "i", "i", "u"], ["b"])
            + MethodXml("SetPosition", ["i", "i", "u"], ["b"]) + MethodXml("SetSize", ["i", "i"], ["b"])
            + MethodXml("ScrollTo", ["u"], ["b"]) + MethodXml("ScrollToPoint", ["u", "i", "i"], ["b"]))
        + Interface("Application", Property("ToolkitName", "s") + Property("Version", "s")
            + Property("ToolkitVersion", "s") + Property("AtspiVersion", "s") + Property("InterfaceVersion", "u")
            + "<property name='Id' type='i' access='readwrite'/>"
            + MethodXml("GetLocale", ["u"], ["s"]) + MethodXml("GetApplicationBusAddress", [], ["s"]))
        + Interface("Cache", Property("version", "u")
            + MethodXml("GetItems", [], ["a((so)(so)(so)iiassusau)"])
            + "<signal name='AddAccessible'><arg type='((so)(so)(so)iiassusau)'/></signal>"
            + "<signal name='RemoveAccessible'><arg type='(so)'/></signal>")
        + "</node>";

    static string Interface(string name, string content) => $"<interface name='{Prefix}{name}'>{content}</interface>";
    static string Property(string name, string type) => $"<property name='{name}' type='{type}' access='read'/>";
    static string MethodXml(string name, string[] input, string[] output) => $"<method name='{name}'>"
        + string.Concat(input.Select(static t => $"<arg type='{t}' direction='in'/>"))
        + string.Concat(output.Select(static t => $"<arg type='{t}' direction='out'/>")) + "</method>";
}
