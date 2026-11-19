using System.Runtime.CompilerServices;

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
        // Exercise the exact measurement helper before the assertion; its counter/loop cannot tier or OSR midway.
        for (var i = 0; i < 32; i++)
        {
            MeasureLookups(registry, root, 1_000);
        }
        Assert.Equal(0, MeasureLookups(registry, root, 10_000));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    static long MeasureLookups(NodeIdentityRegistry registry, int root, int iterations)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            Lookup(registry, root);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Lookup(NodeIdentityRegistry registry, int root)
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

    /// <summary>Identities streamed through once are evicted, so the registry stays the size of what is in use.</summary>
    [Fact]
    public void IdentitiesNoLongerUsedAreEvicted()
    {
        var registry = new NodeIdentityRegistry { RetainPasses = 8 };
        var root = registry.Explicit(0, 0, "root");
        for (var pass = 0; pass < 2_000; pass++)
        {
            registry.BeginPass();
            registry.Explicit(0, 0, "root");
            for (var row = 0; row < 7; row++)
            {
                registry.Explicit(root, 0, $"row{pass}/{row}");
            }
        }

        Assert.InRange(registry.Count, 1, 7 * (8 + 64) + 1);
    }

    /// <summary>An evicted explicit or call-site identity comes back under the same name, so name-keyed state finds it.</summary>
    [Fact]
    public void AnEvictedIdentityComesBackUnderTheSameName()
    {
        var registry = new NodeIdentityRegistry { RetainPasses = 4 };
        var root = registry.Explicit(0, 0, "root");
        var name = registry.Name(registry.At(root, 0, "source.cs", 3, 0));
        var label = registry.Name(registry.Explicit(root, 0, "label"));

        Skip(registry, 200, keepAlive: "root");

        Assert.Equal(name, registry.Name(registry.At(root, 0, "source.cs", 3, 0)));
        Assert.Equal(label, registry.Name(registry.Explicit(root, 0, "label")));
    }

    /// <summary>Pinned identities and everything they depend on keep their keys while unused.</summary>
    [Fact]
    public void APinnedIdentityAndItsDependenciesKeepTheirKeys()
    {
        var registry = new NodeIdentityRegistry { RetainPasses = 4 };
        var root = registry.Explicit(0, 0, "root");
        var panel = registry.Explicit(root, 0, "panel");
        var scope = registry.EnterItem(panel, 0, "item");
        var data = registry.DataScope(0, scope, "key");
        var cell = registry.Automatic(panel, scope, "source.cs", 9, control: true, data: data);
        registry.Pin(cell);

        Skip(registry, 200, keepAlive: null);

        Assert.True(registry.IsPinned(panel));
        Assert.True(registry.IsPinned(root));
        Assert.Equal(panel, registry.Explicit(root, 0, "panel"));
        Assert.Equal(scope, registry.EnterItem(panel, 0, "item"));
        Assert.Equal(data, registry.DataScope(0, scope, "key"));
        Assert.Equal(cell, registry.Automatic(panel, scope, "source.cs", 9, control: true, data: data));
    }

    /// <summary>A serial name is never handed out twice, even when its key is reused by another identity.</summary>
    [Fact]
    public void SerialNamesAreNeverReused()
    {
        var registry = new NodeIdentityRegistry { RetainPasses = 4 };
        var root = registry.Explicit(0, 0, "root");
        var names = new HashSet<string>();
        for (var round = 0; round < 20; round++)
        {
            registry.BeginPass();
            registry.Explicit(0, 0, "root");
            var scope = registry.EnterItem(root, 0, round);
            Assert.True(names.Add(registry.Name(registry.Explicit(root, scope, "cell"))));
            Skip(registry, 100, keepAlive: "root");
        }
    }

    /// <summary>Item keys are released with their scopes, so the registry does not keep caller objects alive.</summary>
    [Fact]
    public void EvictedItemKeysDoNotKeepTheirObjectsAlive()
    {
        var registry = new NodeIdentityRegistry { RetainPasses = 4 };
        var root = registry.Explicit(0, 0, "root");
        var weak = EnterWithObject(registry, root);

        Skip(registry, 200, keepAlive: "root");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference EnterWithObject(NodeIdentityRegistry registry, int root)
    {
        var item = new object();
        registry.BeginPass();
        registry.EnterItem(root, 0, item);
        return new WeakReference(item);
    }

    static void Skip(NodeIdentityRegistry registry, int passes, string? keepAlive)
    {
        for (var i = 0; i < passes; i++)
        {
            registry.BeginPass();
            if (keepAlive is not null)
            {
                registry.Explicit(0, 0, keepAlive);
            }
        }
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
