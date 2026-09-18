using Guinevere;

namespace Example_73_PanGui_MusicApp;

static class Icons
{
    public static ImFont IconFont = null!;
    public const char MenuBurger = '\uf0c9';
    public const char ChartSimpleHorizontal = '\ue474';
    public const char FloppyDisk = '\uf0c7';
    public const char LocationCrosshairSlash = '\uf603';
    public const char LocationSlash = '\uf603';
    public const char NavIcon = '\uf0c9';
    public const char PianoKeyboard = '\uf8d5';
    public const char PlusSquare = '\uf0fe';
    public const char PowerOff = '\uf011';
    public const char Save = '\uf0c7';
    public const char SlidersVSquare = '\uf3f2';
    public const char SquarePlus = '\uf0fe';
    public const char SquareSlidersVertical = '\uf3f2';
    public const char Waveform = '\uf8f1';
    public static LayoutNode DrawIcon(this Gui gui, char icon, float size = 40)
    {
        return gui.DrawGlyph(IconFont, icon, size);
    }
}
