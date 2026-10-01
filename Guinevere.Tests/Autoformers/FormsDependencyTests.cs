using MASS4.Attributes;
using MASS4.Autoformers;

namespace Guinevere.Tests.Autoformers;

public class FormsDependencyTests
{
    static readonly string[] ForbiddenPrefixes = ["Guinevere", "SkiaSharp", "Silk", "Gaya", "Turian"];

    [Fact]
    public void FormsReferencesOnlyAttributesAndTheRuntime()
    {
        AssertNoForbiddenReferences(typeof(FormBuilder).Assembly);
        Assert.Contains(typeof(FormBuilder).Assembly.GetReferencedAssemblies(), name => name.Name == "MASS4.Attributes");
    }

    [Fact]
    public void AttributesReferenceOnlyTheRuntime()
    {
        AssertNoForbiddenReferences(typeof(ButtonAttribute).Assembly);
        Assert.DoesNotContain(typeof(ButtonAttribute).Assembly.GetReferencedAssemblies(),
            name => name.Name == "JetBrains.Annotations");
    }

    [Fact]
    public void RendererReferencesNoGayaOrTurian()
    {
        var names = typeof(FormRenderer).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? "").ToList();

        Assert.DoesNotContain(names, name => name.StartsWith("Gaya", StringComparison.Ordinal)
                                             || name.StartsWith("Turian", StringComparison.Ordinal));
        Assert.Contains("MASS4.Autoformers", names);
    }

    static void AssertNoForbiddenReferences(System.Reflection.Assembly assembly)
    {
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? "")
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbidden);
    }
}
