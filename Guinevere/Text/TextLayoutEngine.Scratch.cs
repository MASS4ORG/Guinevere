namespace Guinevere;

static partial class TextLayoutEngine
{
    /// <summary>
    /// Working lists and buffers one build uses and discards, kept per thread so a new layout allocates only what the
    /// layout retains. Oversized buffers are not kept, and a build started while another is running gets its own.
    /// </summary>
    sealed class Scratch
    {
        const int MaxKept = 4096;

        [ThreadStatic]
        static Scratch? _shared;

        readonly StyledRange[] _plain = new StyledRange[1];
        bool _busy;

        internal List<Piece> Pieces = [];
        internal List<int> Tabs = [];
        internal List<FontRange> FontRanges = [];
        internal List<LineRange> Lines = [];
        internal List<TextLayoutRun> Runs = [];
        internal Font[] RangeFonts = [];
        internal PlacedLine[] Placed = [];
        internal float[] Advance = [];
        internal float[] Prefix = [];

        /// <summary>The thread's scratch state, or a private one when it is already in use.</summary>
        internal static Scratch Acquire()
        {
            var shared = _shared ??= new Scratch();
            if (shared._busy)
            {
                return new Scratch { _busy = true };
            }

            shared._busy = true;
            return shared;
        }

        /// <summary>
        /// A zeroed array of at least <paramref name="length"/> items, reusing <paramref name="buffer"/> when it fits.
        /// </summary>
        internal static T[] Sized<T>(ref T[] buffer, int length)
        {
            if (length > MaxKept)
            {
                return new T[length];
            }

            if (buffer.Length < length)
            {
                buffer = new T[Math.Max(length, 16)];
            }
            else
            {
                Array.Clear(buffer, 0, length);
            }

            return buffer;
        }

        /// <summary>The single range of unstyled text, reusing one array.</summary>
        internal StyledRange[] PlainRange(int length)
        {
            _plain[0] = new StyledRange(0, length, default, -1, null);
            return _plain;
        }

        /// <summary>Drops every reference the build held, so the scratch state keeps nothing alive.</summary>
        internal void Release()
        {
            Pieces = Cleared(Pieces);
            Tabs = Cleared(Tabs);
            FontRanges = Cleared(FontRanges);
            Lines = Cleared(Lines);
            Runs = Cleared(Runs);
            Array.Clear(RangeFonts);
            Array.Clear(Placed);
            _plain[0] = default;
            _busy = false;
        }

        static List<T> Cleared<T>(List<T> list)
        {
            list.Clear();
            return list.Capacity > MaxKept ? [] : list;
        }
    }
}
