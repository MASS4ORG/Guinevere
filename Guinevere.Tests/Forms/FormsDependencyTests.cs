using Guinevere.Forms;
using MASS4.Attributes;

namespace Guinevere.Tests.Forms;

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

    static void AssertNoForbiddenReferences(System.Reflection.Assembly assembly)
    {
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? "")
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbidden);
    }
}
