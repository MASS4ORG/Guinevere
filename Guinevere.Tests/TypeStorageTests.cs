namespace Guinevere.Tests;

public class TypeStorageTests
{
    /// <summary>Confirms default and singular TextEffects values need no heap allocation.</summary>
    [Fact]
    public void TextEffectsDefaultAndSingleShadowConstructionDoNotAllocate()
    {
        Assert.True(typeof(TextEffects).IsValueType);

        _ = CreateEffects();
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = CreateEffects();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        static TextEffects CreateEffects()
        {
            _ = new TextEffects();
            return new TextEffects
            {
                DropShadow = new TextEffects.TextShadow(Color.Black, Vector2.Zero, 1f),
            };
        }
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
