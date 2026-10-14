namespace Guinevere;

public partial class Gui
{
    /// <summary>Queues a stateless fill in the current node; masks, shapes and sprites must survive frame rendering.</summary>
    public void DrawFill(Rect bounds, float value, FillOptions? options = null)
    {
        if (Pass != Pass.Pass2Render)
        {
            return;
        }
        options ??= new FillOptions();
        DrawNormalizedFill(bounds, options.Normalize(value), options);
    }

    /// <summary>Draws background, optional ghost and foreground layers using independent skins and raw values.</summary>
    public void DrawFill(Rect bounds, float value, FillLayers layers, float? ghostValue = null)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (Pass != Pass.Pass2Render)
        {
            return;
        }
        if (layers.Background is { } background)
        {
            DrawNormalizedFill(bounds, 1f, background);
        }
        if (layers.Ghost is { } ghost && ghostValue is { } delayed)
        {
            DrawFill(bounds, delayed, ghost);
        }
        DrawFill(bounds, value, layers.Fill);
    }

    void DrawNormalizedFill(Rect bounds, float fraction, FillOptions options) =>
        AddDraw(new FillDrawable(bounds, fraction, options.ResolveColor(fraction), options));

}
