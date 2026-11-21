using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Evenly spaces source items across a band, reserving any unused slots on the left.</summary>
public readonly struct BarBand
{
    readonly Rect _area;
    readonly int _count;
    readonly double _left;
    readonly double _width;

    /// <summary>Creates a band for <paramref name="count"/> items within the given screen bounds.</summary>
    public BarBand(Rect area, int count, int minimumSlots = 0)
    {
        _area = area;
        _count = ChartArea.IsValid(area) ? Math.Max(0, count) : 0;
        var slots = Math.Max(_count, minimumSlots);
        _width = _count > 0 ? (double)area.W / slots : 0;
        _left = area.X + (slots - _count) * _width;
    }

    /// <summary>Returns the slot bounds for a source item, or an empty rectangle for an invalid index.</summary>
    public Rect Slot(int index) => (uint)index < (uint)_count
        ? new Rect((float)(_left + index * _width), _area.Y, (float)_width, _area.H)
        : default;

    /// <summary>Draws a bottom-aligned bar and returns its clipped bounds for overlays.</summary>
    public bool DrawBar(Gui gui, int index, double value, double maximum, Color color, out Rect bounds,
        float gap = 0, float topInset = 0)
    {
        bounds = default;
        if (gui.Pass != Pass.Pass2Render || !double.IsFinite(value) || value <= 0 ||
            !double.IsFinite(maximum) || maximum <= 0 || !float.IsFinite(gap) || gap < 0 ||
            !float.IsFinite(topInset) || topInset < 0)
        {
            return false;
        }
        var slot = Slot(index);
        if (slot.W <= 0 || slot.H <= topInset)
        {
            return false;
        }
        var inset = Math.Min(gap, Math.Max(0, slot.W - 1));
        var height = (float)(Math.Min(value, maximum) / maximum) * (slot.H - topInset);
        bounds = new Rect(slot.X + inset / 2, slot.Y + slot.H - height, slot.W - inset, height);
        gui.DrawRect(bounds, color);
        return true;
    }

    /// <summary>Returns the source index in the slot under the pointer, or -1.</summary>
    public int HitTest(Vector2 pointer)
    {
        if (_count == 0 || !ChartArea.Contains(_area, pointer) || pointer.X < _left)
        {
            return -1;
        }
        return (int)Math.Min(_count - 1, (pointer.X - _left) / _width);
    }
}
