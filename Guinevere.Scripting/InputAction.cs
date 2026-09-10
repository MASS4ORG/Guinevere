namespace Guinevere;

/// <summary>What a single <see cref="InputScriptStep"/> does.</summary>
public enum InputAction
{
    /// <summary>Moves the pointer to <see cref="InputScriptStep.X"/>, <see cref="InputScriptStep.Y"/>.</summary>
    Move,

    /// <summary>Presses <see cref="InputScriptStep.Button"/> and holds it.</summary>
    Press,

    /// <summary>Releases <see cref="InputScriptStep.Button"/>.</summary>
    Release,

    /// <summary>Presses and releases <see cref="InputScriptStep.Button"/> over two frames.</summary>
    Click,

    /// <summary>Sets the wheel movement for one frame from <see cref="InputScriptStep.Delta"/>.</summary>
    Scroll,

    /// <summary>Presses and releases <see cref="InputScriptStep.Key"/> over two frames.</summary>
    Key,

    /// <summary>Presses <see cref="InputScriptStep.Key"/> and holds it.</summary>
    Hold,

    /// <summary>Releases <see cref="InputScriptStep.Key"/>.</summary>
    Unhold,

    /// <summary>Queues <see cref="InputScriptStep.Text"/> as typed characters for one frame.</summary>
    Type,

    /// <summary>Advances <see cref="InputScriptStep.Count"/> frames without changing input.</summary>
    Frames,

    /// <summary>Writes the current frame to <see cref="InputScriptStep.Path"/> as a PNG.</summary>
    Dump,

    /// <summary>Reports the layout tree with each node's rect.</summary>
    Tree,

    /// <summary>Releases everything held.</summary>
    Reset,

    /// <summary>
    /// Checks the pointer capture: <see cref="InputScriptStep.Value"/> of <c>none</c> expects none,
    /// anything else must be contained in the capturing node's id.
    /// </summary>
    ExpectCapture,

    /// <summary>Checks that a node whose id contains <see cref="InputScriptStep.Value"/> exists.</summary>
    ExpectNode,

    /// <summary>Checks that no node whose id contains <see cref="InputScriptStep.Value"/> exists.</summary>
    ExpectNoNode
}
