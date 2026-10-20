namespace Guinevere;

/// <summary>
/// One outstanding request to an asynchronous provider. Starting a new request cancels the previous one, and a result
/// is only taken once, tagged with the document version and key it was asked for.
/// </summary>
sealed class ProviderRequest<TResult>
{
    Task<TResult>? _task;
    CancellationTokenSource? _cancellation;

    /// <summary>The document version of the request in flight or last finished.</summary>
    internal int Version { get; private set; } = -1;

    /// <summary>The caller's key of the request, such as the offset it is about.</summary>
    internal int Key { get; private set; } = -1;

    /// <summary>Whether a request is running.</summary>
    internal bool IsPending => _task is { IsCompleted: false };

    /// <summary>Cancels any running request and starts a new one.</summary>
    internal void Start(Func<CancellationToken, Task<TResult>> request, int version, int key = 0)
    {
        Cancel();
        _cancellation = new CancellationTokenSource();
        Version = version;
        Key = key;
        try
        {
            _task = request(_cancellation.Token);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _task = Task.FromException<TResult>(exception);
        }
    }

    /// <summary>Takes a successful result once; failed and cancelled requests are dropped.</summary>
    internal bool TryTake(out TResult result)
    {
        result = default!;
        if (_task is not { IsCompleted: true } task)
        {
            return false;
        }

        _task = null;
        if (!task.IsCompletedSuccessfully)
        {
            _ = task.Exception;
            return false;
        }

        result = task.Result;
        return true;
    }

    /// <summary>Cancels the running request, if any.</summary>
    internal void Cancel()
    {
        _cancellation?.Cancel();
        _cancellation = null;
        _task = null;
    }
}
