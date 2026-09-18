namespace Example_73_PanGui_MusicApp;

public partial class Program
{

    string[] _trackLibrary =
    [
        "Into the Wild", "Midnight Serenade", "Echoes of Eternity", "Dreamscape", "Whispering Shadows",
        "Enchanted Journey",
        "Mystic Melodies", "Celestial Symphony", "Lost in Time", "Harmony of the Spheres", "Serenity Falls",
        "Eternal Bliss", "Twilight Reverie", "Melancholy Memories", "Dancing Fireflies", "Starry Night",
        "Whispers in the Wind", "Moonlit Sonata", "Sunset Serenade", "Ocean's Embrace"
    ];

    string _selectedTrack = "Lost in Time";
    float _modWheel = 0.5f;
    int _numOctaves = 8;
    float[] _keyPressures = new float[12 * _numOctaves];
    PadPlayer _padPlayer = new();
    Snapshot[] _snapshots = GenerateSnapshots();
    Snapshot _selectedSnapshot = _snapshots[0];

    (float left, float right) GetSimulatedOutputVolume()
    {
        float maxPianoKeyPressure = _gui.Smoothdamp(_keyPressures.Max());
        float mono = MathF.Cos(_gui.Time.Elapsed * 4.5f);
        float tLeft = mono + MathF.Cos(_gui.Time.Elapsed * 7.5f) * 0.5f;
        float tRight = mono + MathF.Cos(_gui.Time.Elapsed * 8.5f) * 0.5f;
        float left = maxPianoKeyPressure * (0.8f + tLeft * 0.1f);
        float right = maxPianoKeyPressure * (0.8f + tRight * 0.1f);
        return (left, right);
    }

    Snapshot[] GenerateSnapshots()
    {
        var rnd = new Random(200);

        return
        [
            new Snapshot { Name = "Snapshot 1", Instruments = GenerateInstruments() },
            new Snapshot { Name = "Snapshot 2", Instruments = GenerateInstruments() },
            new Snapshot { Name = "Snapshot 3", Instruments = GenerateInstruments() },
            new Snapshot { Name = "Snapshot 4", Instruments = GenerateInstruments() },
            new Snapshot { Name = "Snapshot 5", Instruments = GenerateInstruments() },
            new Snapshot { Name = "Snapshot 6", Instruments = GenerateInstruments() },
        ];

        Instrument[] GenerateInstruments() =>
        [
            new()
            {
                Name = "Piano",
                Color = 0xF0A948FF,
                Volume = (float)rnd.NextDouble(),
                Effects = GenerateEffects(),
                KeyStart = rnd.Next(0, 20),
                KeyLength = rnd.Next(50, 60),
                Adsr = new Adsr(),
                Pan = 0.5f
            },
            new()
            {
                Name = "Bass",
                Color = 0x3BBE69FF,
                Volume = (float)rnd.NextDouble(),
                Effects = GenerateEffects(),
                KeyStart = rnd.Next(0, 20),
                KeyLength = rnd.Next(50, 60),
                Adsr = new Adsr(),
                Pan = 0.3f
            },
            new()
            {
                Name = "Drums",
                Color = 0x3961E6FF,
                Volume = (float)rnd.NextDouble(),
                Effects = GenerateEffects(),
                KeyStart = rnd.Next(0, 20),
                KeyLength = rnd.Next(50, 60),
                Adsr = new Adsr(),
                Pan = 0.6f
            },
            new()
            {
                Name = "Guitar",
                Color = 0x8F59E2FF,
                Volume = (float)rnd.NextDouble(),
                Effects = GenerateEffects(),
                KeyStart = rnd.Next(0, 20),
                KeyLength = rnd.Next(50, 60),
                Adsr = new Adsr(),
                Pan = 0.5f
            },
            new()
            {
                Name = "Synth",
                Color = 0xE673C4FF,
                Volume = (float)rnd.NextDouble(),
                Effects = GenerateEffects(),
                KeyStart = rnd.Next(0, 20),
                KeyLength = rnd.Next(50, 60),
                Adsr = new Adsr(),
                Pan = 0.5f
            },
        ];

        Effect[] GenerateEffects() =>
        [
            new()
            {
                Name = "Reverb",
                Type = "Midi",
                IsOn = rnd.NextDouble() > 0.1,
                KnobValues =
                [
                    (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(),
                    (float)rnd.NextDouble(), (float)rnd.NextDouble()
                ]
            },
            new()
            {
                Name = "Delay",
                Type = "Midi",
                IsOn = rnd.NextDouble() > 0.1,
                KnobValues =
                [
                    (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(),
                    (float)rnd.NextDouble(), (float)rnd.NextDouble()
                ]
            },
            new()
            {
                Name = "Chorus",
                Type = "Midi",
                IsOn = rnd.NextDouble() > 0.1,
                KnobValues =
                [
                    (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(),
                    (float)rnd.NextDouble(), (float)rnd.NextDouble()
                ]
            },
            new()
            {
                Name = "EQ",
                Type = "Midi",
                IsOn = rnd.NextDouble() > 0.1,
                KnobValues =
                [
                    (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(),
                    (float)rnd.NextDouble(), (float)rnd.NextDouble()
                ]
            },
            new()
            {
                Name = "Compressor",
                Type = "Midi",
                IsOn = rnd.NextDouble() > 0.1,
                KnobValues =
                [
                    (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(),
                    (float)rnd.NextDouble(), (float)rnd.NextDouble()
                ]
            }
        ];
    }
}
