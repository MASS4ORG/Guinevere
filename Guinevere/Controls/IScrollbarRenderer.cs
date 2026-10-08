namespace Guinevere;

/// <summary>Draws scrollbar parts while core owns their geometry, scrolling and pointer capture.</summary>
public interface IScrollbarRenderer
{
    /// <summary>Draws a track and thumb; returns false when core should draw its default appearance.</summary>
    bool Draw(Gui gui, LayoutNode container, Axis axis, Rect track, Rect thumb, StyleState state);
}
