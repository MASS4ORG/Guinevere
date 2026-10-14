using Guinevere;

namespace Example73PanGuiMusicApp;

struct Popup
{
    public AnimationFloat Visibility;
    public LayoutNodeScope HeaderContainer;
    public LayoutNodeScope BodyContainer;
    public LayoutNodeScope FooterContainer;
    public readonly void Show()
    {
        Visibility.AnimateTo(1, 0.3f, Easing.SmoothStep);
    }
    public readonly void Hide()
    {
        Visibility.AnimateTo(0, 0.3f, Easing.SmoothStep);
    }
    public readonly bool IsVisible()
    {
        return Visibility > 0.001f;
    }
}
