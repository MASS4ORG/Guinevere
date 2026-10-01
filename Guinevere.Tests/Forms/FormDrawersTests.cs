using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Guinevere.Forms;
using MASS4.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Guinevere.Tests.Forms;

public class FormDrawersTests
{
    [Fact]
    public void DefaultHoldsBuiltinsAndIsReadOnly()
    {
        Assert.NotNull(FormDrawers.Default.TypeDrawer(typeof(Vector3)));
        Assert.NotNull(FormDrawers.Default.TypeDrawer(typeof(Color)));
        Assert.NotNull(FormDrawers.Default.AttributeDrawer(typeof(TooltipAttribute)));
        Assert.Null(FormDrawers.Default.TypeDrawer(typeof(int)));

        Assert.Throws<InvalidOperationException>(() => FormDrawers.Default.Add(typeof(int), new Recording()));
        Assert.Throws<InvalidOperationException>(() => FormDrawers.Default.Add(_ => true, new Recording()));
        Assert.Throws<InvalidOperationException>(() => FormDrawers.Default.Add<RangeAttribute>(new Decorator()));
    }

    [Fact]
    public void ChildFallsBackToParent()
    {
        var parent = new FormDrawers();
        var drawer = new Recording();
        using var _ = parent.Add(typeof(Settings), drawer);
        var child = new FormDrawers(parent);

        Assert.Same(drawer, child.TypeDrawer(typeof(Settings)));
        Assert.NotNull(child.TypeDrawer(typeof(Vector2)));
    }

    [Fact]
    public void TypeDrawerServesDerivedInterfaceAndNullable()
    {
        var drawers = new FormDrawers();
        var forBase = new Recording();
        var forInterface = new Recording();
        var forStruct = new Recording();
        using var a = drawers.Add(typeof(Settings), forBase);
        using var b = drawers.Add(typeof(IMarker), forInterface);
        using var c = drawers.Add(typeof(Point), forStruct);

        Assert.Same(forBase, drawers.TypeDrawer(typeof(DerivedSettings)));
        Assert.Same(forInterface, drawers.TypeDrawer(typeof(Marked)));
        Assert.Same(forStruct, drawers.TypeDrawer(typeof(Point?)));
        Assert.Null(drawers.TypeDrawer(typeof(string)));
    }

    [Fact]
    public void LatestRegistrationWinsAndDisposeRestoresTheEarlierOne()
    {
        var drawers = new FormDrawers();
        var first = new Recording();
        var second = new Recording();
        using var a = drawers.Add(typeof(Settings), first);
        var b = drawers.Add(typeof(Settings), second);

        Assert.Same(second, drawers.TypeDrawer(typeof(Settings)));

        b.Dispose();
        b.Dispose();

        Assert.Same(first, drawers.TypeDrawer(typeof(Settings)));
    }

    [Fact]
    public void DisposingTheLastRegistrationRemovesTheType()
    {
        var drawers = new FormDrawers();
        drawers.Add(typeof(Settings), new Recording()).Dispose();

        Assert.Null(drawers.TypeDrawer(typeof(Settings)));
    }

    [Fact]
    public void PredicatesMatchByOrderThenInnerScopeFirst()
    {
        var parent = new FormDrawers();
        var child = new FormDrawers(parent);
        var parentEarly = new Recording();
        var childLate = new Recording();
        var parentLate = new Recording();
        var field = FormField.Display("x", new object(), () => 1);

        using var a = parent.Add(_ => true, parentLate, order: 1);
        using var b = child.Add(_ => true, childLate, order: 1);
        Assert.Same(childLate, child.PredicateDrawer(field));

        var c = parent.Add(_ => true, parentEarly, order: -1);
        Assert.Same(parentEarly, child.PredicateDrawer(field));

        c.Dispose();
        Assert.Same(childLate, child.PredicateDrawer(field));
    }

    [Fact]
    public void PredicateThatRejectsIsSkipped()
    {
        var drawers = new FormDrawers();
        using var _ = drawers.Add(field => field.ValueType == typeof(string), new Recording());

        Assert.Null(drawers.PredicateDrawer(FormField.Display("x", new object(), () => 1)));
    }

    [Fact]
    public void AttributeDrawerServesDerivedAttributes()
    {
        var drawers = new FormDrawers();
        var decorator = new Decorator();
        using var _ = drawers.Add<HideInEditorAttribute>(decorator);

        Assert.Same(decorator, drawers.AttributeDrawer(typeof(DerivedHideAttribute)));
        Assert.Null(drawers.AttributeDrawer(typeof(RangeAttribute)));
    }

    [Fact]
    public void AddCustomEditorsRegistersAnnotatedDrawersUntilDisposed()
    {
        var drawers = new FormDrawers();
        var registration = drawers.AddCustomEditors(typeof(FormDrawersTests).Assembly);

        Assert.IsType<SettingsEditor>(drawers.TypeDrawer(typeof(Settings)));
        Assert.IsType<SettingsEditor>(drawers.TypeDrawer(typeof(Point)));

        registration.Dispose();

        Assert.Null(drawers.TypeDrawer(typeof(Settings)));
        Assert.Null(drawers.TypeDrawer(typeof(Point)));
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var drawers = new FormDrawers();

        Assert.Throws<ArgumentNullException>(() => drawers.Add((Type)null!, new Recording()));
        Assert.Throws<ArgumentNullException>(() => drawers.Add(typeof(int), null!));
        Assert.Throws<ArgumentNullException>(() => drawers.Add((Func<FormField, bool>)null!, new Recording()));
        Assert.Throws<ArgumentNullException>(() => drawers.Add(_ => true, null!));
        Assert.Throws<ArgumentNullException>(() => drawers.Add<RangeAttribute>(null!));
        Assert.Throws<ArgumentNullException>(() => drawers.AddCustomEditors(null!));
    }

    [Fact]
    public void DisposedPluginDrawersLetTheirAssemblyUnload()
    {
        var drawers = new FormDrawers();
        var context = LoadAndUseCollectiblePlugin(drawers);

        for (var i = 0; i < 10 && context.IsAlive; i++)
        {
            Assert.Null(drawers.TypeDrawer(typeof(int)));
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(context.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference LoadAndUseCollectiblePlugin(FormDrawers drawers)
    {
        var context = new AssemblyLoadContext("forms-plugin", isCollectible: true);
        var plugin = context.LoadFromStream(CompilePlugin());
        var edited = plugin.GetType("Plugin.Edited")!;

        var registration = drawers.AddCustomEditors(plugin);
        Assert.NotNull(drawers.TypeDrawer(edited));
        Assert.NotNull(drawers.TypeDrawer(typeof(int)));
        registration.Dispose();

        context.Unload();
        return new WeakReference(context);
    }

    static MemoryStream CompilePlugin()
    {
        const string source = """
            using Guinevere;
            using Guinevere.Forms;
            using MASS4.Attributes;
            namespace Plugin;
            public sealed class Edited;
            [CustomEditor(typeof(Edited)), CustomEditor(typeof(int))]
            public sealed class EditedDrawer : IPropertyDrawer
            {
                public void Draw(Gui gui, FormField field, string id, FormRenderContext context) { }
                public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
            }
            """;
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && assembly.Location.Length > 0)
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create("FormsPlugin", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        stream.Position = 0;
        return stream;
    }

    interface IMarker;

    class Settings;

    sealed class DerivedSettings : Settings;

    sealed class Marked : IMarker;

    struct Point;

    sealed class DerivedHideAttribute : HideInEditorAttribute;

    sealed class Recording : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context) { }
        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }

    sealed class Decorator : IAttributeDrawer
    {
        public void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context,
            Action next) => next();
    }

    [CustomEditor(typeof(Settings)), CustomEditor(typeof(Point))]
    sealed class SettingsEditor : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context) { }
        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }
}
