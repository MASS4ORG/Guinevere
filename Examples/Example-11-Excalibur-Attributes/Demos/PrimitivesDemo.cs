namespace Example_76_Odin_Attributes;

public enum Difficulty { Easy, Normal, Hard }

// No attributes: every public read-write member gets its editor.
public sealed class PrimitivesDemo
{
    public bool Enabled { get; set; } = true;
    public string PlayerName { get; set; } = "Arthur";
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public int Lives { get; set; } = 3;
    public float Speed { get; set; } = 4.5f;
    public double Gravity { get; set; } = 9.81;
    public byte Opacity { get; set; } = 255;
    public long Score { get; set; } = 1_000_000;
}
