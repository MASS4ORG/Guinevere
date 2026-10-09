namespace Guinevere.Tests.Layout;

/// <summary>Verifies collision-safe interning and allocation-free identity lookup after warmup.</summary>
public class NodeIdentityRegistryTests
{
    readonly record struct CollidingKey(int Value)
    {
        public override int GetHashCode() => 0;
    }

    /// <summary>Equal hash codes do not turn different typed item values into the same identity.</summary>
    [Fact]
    public void HashCollisionsAndDifferentKeyTypesRemainDistinct()
    {
        var registry = new NodeIdentityRegistry();
        var root = registry.Explicit(0, 0, "0");
        var first = registry.EnterItem(root, 0, new CollidingKey(1));
        var second = registry.EnterItem(root, 0, new CollidingKey(2));
        var integer = registry.EnterItem(root, 0, 1);
        var text = registry.EnterItem(root, 0, "1");
        Assert.Equal(4, new[] { first, second, integer, text }.Distinct().Count());
        registry.BeginPass();
        Assert.Equal(second, registry.EnterItem(root, 0, new CollidingKey(2)));
        Assert.Equal(first, registry.EnterItem(root, 0, new CollidingKey(1)));
    }

    /// <summary>Automatic identity and typed item lookup allocate nothing once descriptors are cached.</summary>
    [Fact]
    public void WarmedIdentityLookupDoesNotAllocate()
    {
        var registry = new NodeIdentityRegistry();
        var root = registry.Explicit(0, 0, "0");
        void Lookup()
        {
            registry.BeginPass();
            for (var i = 0; i < 32; i++)
            {
                var scope = registry.EnterItem(root, 0, i);
                registry.Automatic(root, scope, "source.cs", 42);
                var control = registry.Automatic(root, scope, "source.cs", 43, control: true);
                registry.Name(control);
                registry.Explicit(root, scope, "semantic");
                registry.At(root, scope, "source.cs", 44, 0);
            }
        }
        for (var i = 0; i < 100; i++) Lookup();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Lookup();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    /// <summary>Duplicate and null item keys are rejected, while different parents and outer scopes are independent.</summary>
    [Fact]
    public void ItemKeyValidationUsesParentAndOuterScope()
    {
        var registry = new NodeIdentityRegistry();
        var root = registry.Explicit(0, 0, "0");
        var scope = registry.EnterItem(root, 0, 7);
        Assert.Throws<InvalidOperationException>(() => registry.EnterItem(root, 0, 7));
        Assert.NotEqual(scope, registry.EnterItem(root, scope, 7));
        Assert.NotEqual(scope, registry.EnterItem(root + 1, 0, 7));
        Assert.Throws<ArgumentNullException>(() => registry.EnterItem<string>(root, 0, null!));
    }

    /// <summary>String compatibility is cached and preserves explicit names and the deterministic NodeId format.</summary>
    [Fact]
    public void NamesAreLazyCachedAndCompatible()
    {
        var registry = new NodeIdentityRegistry();
        var root = registry.Explicit(0, 0, "0");
        var key = registry.At(root, 0, "source.cs", 12, 3);
        Assert.Equal("0source.cs:12 3", registry.Name(key));
        Assert.Same(registry.Name(key), registry.Name(key));
        Assert.Equal(key, registry.At(root, 0, "source.cs", 12, 3));
        Assert.Equal("panel", registry.Name(registry.Explicit(root, 0, "panel")));
        var scope = registry.EnterItem(root, 0, 1);
        Assert.StartsWith("@node:", registry.Name(registry.Explicit(root, scope, "panel")));
        Assert.Equal("", registry.Name(0));
    }
}
