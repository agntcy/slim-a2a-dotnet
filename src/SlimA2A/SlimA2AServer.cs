using Agntcy.Slim;

namespace SlimA2A;

/// <summary>An A2A agent served over SLIMRPC, created by <see cref="SlimA2AConnection.StartServerAsync"/>.</summary>
public sealed class SlimA2AServer : IAsyncDisposable
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(15);

    private readonly SlimA2AConnection _connection;
    private readonly SlimApp _app;
    private readonly uniffi.slim_rpc.Server _server;
    private readonly CancellationTokenSource _stoppingCts;
    private int _stopping;
    private int _disposed;

    internal SlimA2AServer(
        SlimA2AConnection connection, string identity, SlimApp app, uniffi.slim_rpc.Server server, CancellationTokenSource stopping)
    {
        _connection = connection;
        _app = app;
        _server = server;
        _stoppingCts = stopping;
        Identity = identity;
        Completion = ServeAsync();
    }

    /// <summary>The server's SLIM identity.</summary>
    public string Identity { get; }

    /// <summary>Completes when the server stops serving: after <see cref="StopAsync"/>, or with an exception if serving fails.</summary>
    public Task Completion { get; }

    /// <summary>
    /// Stops accepting requests, cancels the ones in flight (their callers get an <c>Unavailable</c> error), and waits up to
    /// 15 seconds for them to finish.
    /// </summary>
    /// <returns>A task that completes when the server has stopped.</returns>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 0)
        {
            // Cancel first: a long-lived stream such as SubscribeToTask would otherwise hold up the drain until its deadline.
            await _stoppingCts.CancelAsync().ConfigureAwait(false);
            await _server.ShutdownAsync().ConfigureAwait(false);
        }
        try
        {
            await Completion.WaitAsync(ShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Draining is best effort.
        }
    }

    /// <summary>Stops the server and releases its SLIM identity.</summary>
    /// <returns>A task that completes when the server is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _server.Dispose();
            _app.Dispose();
            _stoppingCts.Dispose();
            _connection.Release(this);
        }
    }

    private async Task ServeAsync()
    {
        try
        {
            await _server.ServeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _stopping) != 0)
        {
            // Stopped on request.
        }
    }
}
