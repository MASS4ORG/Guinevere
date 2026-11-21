using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Guinevere;

public partial class Gui
{
    readonly ConcurrentQueue<Action> _posted = new();

    /// <summary>
    /// Queues work from any thread, never inline, to run on the UI thread in posting order at the start of the next
    /// frame before input dispatch and building; the queue is unbounded. An exception is rethrown from
    /// <see cref="BeginFrame"/> after all queued actions ran, so that frame is not built and the host decides.
    /// </summary>
    /// <param name="work">The work to run on the UI thread.</param>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _posted.Enqueue(work);
        RequestFrame();
    }

    /// <summary>
    /// Runs the actions posted before this frame in order. Actions posted while draining wait for the next frame.
    /// </summary>
    internal void DrainPosted()
    {
        if (_posted.IsEmpty)
        {
            return;
        }

        List<Exception>? errors = null;
        var count = _posted.Count;
        for (var i = 0; i < count && _posted.TryDequeue(out var work); i++)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                errors ??= [];
                errors.Add(e);
            }
        }

        if (errors is null)
        {
            return;
        }

        if (errors.Count == 1)
        {
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }

        throw new AggregateException(errors);
    }
}
