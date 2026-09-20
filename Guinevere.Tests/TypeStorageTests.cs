using System.Runtime.CompilerServices;

namespace Guinevere.Tests;

public class TypeStorageTests
{
    [Fact]
    public void TextEffectsIsAnAllocationFreeValue()
    {
        Assert.True(typeof(TextEffects).IsValueType);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TextEffects>());
    }

    [Fact]
    public void DrawListClearRetainsAReusableCommandBuffer()
    {
        var list = new DrawList();
        list.EnsureCapacity(32);
        list.Add(new NoopDrawable());
        list.AddClip(new Rect(0, 0, 10, 10));

        Assert.Equal(2, list.Count);
        list.Clear();
        Assert.Equal(0, list.Count);
    }

    sealed class NoopDrawable : IDrawable
    {
        public SKPaint? Paint => null;

        public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
        {
        }
    }
}
