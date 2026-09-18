using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Guinevere;

public partial class Gui
{
    readonly Dictionary<Type, IDictionary> _typedStores = new();

    /// <summary>
    /// Retrieves a reference to a value of type <typeparamref name="T"/> associated with the specified identifier.
    /// If the identifier does not exist in the store, the default value is added and returned.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve or add.</typeparam>
    /// <param name="defaultValue">The default value to insert if the identifier does not exist.</param>
    /// <param name="id">The identifier associated with the value. By default, this is the name of the variable passed as <paramref name="defaultValue"/>.</param>
    /// <returns>A reference to the value of type <typeparamref name="T"/> associated with the specified identifier.</returns>
    public ref T GetValue<T>(T defaultValue, [CallerArgumentExpression(nameof(defaultValue))] string id = "")
    {
        var dict = GetStore<T>();
        dict.TryAdd(id, defaultValue);
        return ref CollectionsMarshal.GetValueRefOrAddDefault(dict, id, out _)!;
    }

    /// <summary>
    /// Sets the value of type <typeparamref name="T"/> associated with the specified identifier.
    /// If the identifier already exists in the store, the value is updated.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="value">The value to associate with the specified identifier.</param>
    /// <param name="id">The identifier associated with the value. By default, this is the name of the variable passed as <paramref name="value"/>.</param>
    public void SetValue<T>(T value, [CallerArgumentExpression(nameof(value))] string id = "")
    {
        GetStore<T>()[id] = value!;
    }

    /// <summary>
    /// Retrieves a reference to a float value associated with the specified identifier.
    /// If the identifier does not exist in the store, the default value is added and returned.
    /// </summary>
    /// <param name="defaultValue">The default float value to insert if the identifier does not exist.</param>
    /// <param name="id">The identifier associated with the float value. This is typically the name of the variable passed as <paramref name="defaultValue"/>.</param>
    /// <returns>A reference to the float value associated with the specified identifier.</returns>
    public ref float GetFloat(float defaultValue, [CallerArgumentExpression(nameof(defaultValue))] string id = "")
    {
        return ref GetValue(defaultValue, id);
    }

    /// <summary>
    /// Sets the value of type <see cref="float"/> associated with the specified identifier.
    /// If the identifier already exists in the store, the value is updated.
    /// </summary>
    /// <param name="value">The value to associate with the specified identifier.</param>
    /// <param name="id">The identifier associated with the value. By default, this is the name of the variable passed as <paramref name="value"/>.</param>
    public void SetFloat(float value, [CallerArgumentExpression(nameof(value))] string id = "")
    {
        SetValue(value, id);
    }

    Dictionary<string, T> GetStore<T>()
    {
        var type = typeof(T);
        if (!_typedStores.TryGetValue(type, out var dict))
        {
            dict = new Dictionary<string, T>();
            _typedStores[type] = dict;
        }

        return (Dictionary<string, T>)dict;
    }
}
