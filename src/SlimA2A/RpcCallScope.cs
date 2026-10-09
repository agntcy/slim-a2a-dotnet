using uniffi.slim_rpc;

namespace SlimA2A;

/// <summary>
/// Cancellation for one RPC on the server: cancelled when the caller's deadline passes or the server stops. SLIM doesn't
/// cancel .NET handlers on either, so without this an agent keeps working for a caller that has already given up.
/// </summary>
internal sealed class RpcCallScope : IDisposable
{
    // CancellationTokenSource timers are limited to int.MaxValue milliseconds (~24.8 days); SLIM's own cap is 10 hours.
    private static readonly TimeSpan MaxTimer = TimeSpan.FromMilliseconds(int.MaxValue - 1);

    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _serverStopping;

    /// <param name="remaining">Time left until the caller's deadline, or null when there is none.</param>
    /// <param name="serverStopping">Cancelled when the server stops.</param>
    /// <param name="metadata">The metadata the client sent with the call.</param>
    public RpcCallScope(TimeSpan? remaining, CancellationToken serverStopping, IReadOnlyDictionary<string, string>? metadata = null)
    {
        _serverStopping = serverStopping;
        Context = new SlimA2ACallContext(metadata);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(serverStopping);
        if (remaining is { } r && r < MaxTimer)
            _cts.CancelAfter(r);
    }

    /// <summary>Exposed as <see cref="SlimA2ACallContext.Current"/> while the request is handled.</summary>
    public SlimA2ACallContext Context { get; }

    /// <summary>Passed to the request handler.</summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>True once the deadline passed or the server stopped.</summary>
    public bool IsCancellationRequested => _cts.IsCancellationRequested;

    /// <summary>The RPC error reported for a cancellation this scope caused.</summary>
    public RpcException.Rpc ToRpcError() => _serverStopping.IsCancellationRequested
        ? new RpcException.Rpc(RpcCode.Unavailable, "The server is stopping.", null)
        : new RpcException.Rpc(RpcCode.DeadlineExceeded, "The deadline passed before the request completed.", null);

    public void Dispose() => _cts.Dispose();
}
