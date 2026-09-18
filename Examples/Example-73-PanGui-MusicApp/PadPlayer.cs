namespace Example_73_PanGui_MusicApp;

class PadPlayer
{
    public string[] Pads = ["A", "Bb", "B", "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab"];
    public int SelectedPad;
    public float Volume;
    public float Brightness;
    public float Shimmer;
    public Effect Effect = new() { Name = "Reverb", Type = "Midi" };
}
