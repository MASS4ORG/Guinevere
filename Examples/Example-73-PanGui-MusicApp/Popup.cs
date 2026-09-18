using Guinevere;

namespace Example_73_PanGui_MusicApp;

struct Popup
{
    public AnimationFloat Visibility;
    public LayoutNodeScope HeaderContainer;
    public LayoutNodeScope BodyContainer;
    public LayoutNodeScope FooterContainer;
    public void Show()
    {
        Visibility.AnimateTo(1, 0.3f, Easing.SmoothStep);
    }
    public void Hide()
    {
        Visibility.AnimateTo(0, 0.3f, Easing.SmoothStep);
    }
    public bool IsVisible()
    {
        return Visibility > 0.001f;
    }
}
