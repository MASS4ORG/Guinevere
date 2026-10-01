namespace MASS4.Autoformers;

/// <summary>An object that names itself in the inspector instead of showing its type name as the form's title.</summary>
public interface ITitled
{
    /// <summary>The heading above the object's members.</summary>
    string Title { get; }
}
