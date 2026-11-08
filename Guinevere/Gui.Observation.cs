using System.Collections;
using System.Globalization;

namespace Guinevere;

/// <summary>Opt-in frame instrumentation; implementations own copied values and release registrations on detach.</summary>
public interface IGuiFrameObserver
{
    /// <summary>Whether detailed values should be constructed for this frame.</summary>
    bool CaptureDetails { get; }
    /// <summary>Starts collection before input dispatch and application construction.</summary>
    void BeginFrame(Gui gui);
    /// <summary>Closes the previous phase and starts the named phase.</summary>
    void Phase(string name);
    /// <summary>Collects final state after target rendering and input finalization.</summary>
    void EndFrame(Gui gui);
    /// <summary>Receives a copied scalar property and optional layout geometry.</summary>
    void Inspect(string id, string parent, string category, string name, string value, Rect? bounds = null);
    /// <summary>Copies text shaping data while its frame resources are valid.</summary>
    void Text(LayoutNode node, TextLayout layout);
    /// <summary>Records why the target requested an immediate or scheduled frame.</summary>
    void FrameRequested(double delaySeconds);
    /// <summary>Copies a failure and cancels incomplete collection.</summary>
    void Failed(Exception exception);
}

public partial class Gui
{
    /// <summary>Optional observer; a null value performs no timing, snapshotting or metadata construction.</summary>
    public IGuiFrameObserver? FrameObserver { get; set; }

    internal int RenderedNodeCount => _renderNodes.Count;

    internal (string File, int Line) NodeSource(LayoutNode node) => _nodeIdentities.Source(node.Identity);

    internal void InspectState(IGuiFrameObserver observer)
    {
        foreach (var (type, store) in _typedStores)
        {
            foreach (DictionaryEntry entry in store)
            {
                observer.Inspect($"value/{type.FullName}/{entry.Key}", "", "state", type.Name,
                    ScalarValue(entry.Value));
            }
        }
        foreach (var (type, store) in _controlStates)
        {
            foreach (var (key, value) in store)
            {
                observer.Inspect($"control/{key.Scope}/{key.Id}", "", "state", type.Name,
                    ScalarValue(value));
            }
        }
        foreach (var (type, table) in _dataStores)
        {
            foreach (DictionaryEntry entry in (IDictionary)table)
            {
                observer.Inspect($"data/{entry.Key}", "", "state", type.Name,
                    ScalarValue(((IDataCell)entry.Value!).InspectValue));
            }
        }
    }

    static string ScalarValue(object? value) => value switch
    {
        null => "null",
        string text => text,
        bool flag => flag ? "true" : "false",
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        Enum enumeration => enumeration.ToString(),
        _ => $"{value.GetType().Name} (opaque; register a typed inspector for fields)"
    };

    interface IDataCell
    {
        object? InspectValue { get; }
    }
}
