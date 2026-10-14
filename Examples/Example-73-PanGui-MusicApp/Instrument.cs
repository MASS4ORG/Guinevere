using Guinevere;

namespace Example73PanGuiMusicApp;

struct Instrument
{
    public string Name;
    public Color Color;
    public float Volume;
    public float Pan;
    public int KeyStart;
    public int KeyLength;
    public Effect[] Effects;
    public Adsr Adsr;
}
