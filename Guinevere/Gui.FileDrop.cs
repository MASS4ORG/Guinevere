namespace Guinevere;

public partial class Gui
{
    void DispatchFileDrops()
    {
        if (!Platform.TryGet<IFileDropCapability>(out var source) || source is null) return;
        while (source.TryDequeue(out var drop))
        {
            if (RootNode is not { } root) continue;
            if (HitTest(root, drop.Position) is not { } target) continue;
            Dispatch(new FileDropEvent { Position = drop.Position, Paths = drop.Paths }, target);
        }
    }
}
