using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using A2A;
using Agntcy.Slim;
using Agntcy.Slim.SlimRpc;

namespace SlimA2A;

/// <summary>
/// A connection to a SLIM node, shared by every A2A server and client of this process that uses that node: serve agents
/// with <see cref="StartServerAsync"/> and call them with <see cref="CreateClient"/>.
/// </summary>
/// <remarks>
/// The SLIM runtime allows one connection per node endpoint in a process, so create one <see cref="SlimA2AConnection"/>
/// per node and share it. Disposing the connection also disposes the servers and clients created from it.
/// </remarks>
public sealed class SlimA2AConnection : IAsyncDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxAttemptTimeout = TimeSpan.FromSeconds(10);

    private readonly SlimService _service;
    private readonly ulong _connectionId;
    private readonly ConcurrentDictionary<IAsyncDisposable, byte> _owned = new();
    private int _disposed;

    private SlimA2AConnection(SlimService service, ulong connectionId, string endpoint)
    {
        _service = service;
        _connectionId = connectionId;
        Endpoint = endpoint;
    }

    /// <summary>The SLIM node's endpoint.</summary>
    public string Endpoint { get; }

    /// <summary>Connects to a SLIM node.</summary>
    /// <param name="options">The node's endpoint, transport security and connect timeout.</param>
    /// <param name="cancellationToken">Stops waiting for the connection.</param>
    /// <returns>The open connection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The endpoint is not an absolute <c>http</c> or <c>https</c> URL, or the connect timeout is not positive.</exception>
    /// <exception cref="TimeoutException">The node could not be reached within <see cref="SlimA2AConnectionOptions.ConnectTimeout"/>.</exception>
    /// <exception cref="SlimException">The SLIM runtime rejected the connection, for example because this process is already connected to the endpoint.</exception>
    public static async Task<SlimA2AConnection> ConnectAsync(SlimA2AConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var json = CreateClientConfigJson(options);

        Slim.Initialize();
        var config = Slim.NewClientConfigFromJson(json);
        if (options.ConfigureClient is { } configure)
            config = configure(config) ?? throw new InvalidOperationException($"{nameof(options.ConfigureClient)} returned null.");

        var service = Slim.GetGlobalService();
        var connect = service.ConnectAsync(config, cancellationToken);
        try
        {
            var connectionId = await connect.WaitAsync(options.ConnectTimeout, cancellationToken).ConfigureAwait(false);
            return new SlimA2AConnection(service, connectionId, options.Endpoint);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            ReleaseWhenDone(service, connect);
            if (ex is TimeoutException)
                throw new TimeoutException($"Could not connect to the SLIM node at {options.Endpoint} within {options.ConnectTimeout}.", ex);
            throw;
        }
        catch
        {
            service.Dispose();
            throw;
        }
    }

    /// <summary>Starts serving an A2A agent under a SLIM identity.</summary>
    /// <param name="options">The server's identity and shared secret, and optionally how to resolve the extended agent card.</param>
    /// <param name="handler">Handles the A2A requests, typically an <see cref="A2AServer"/>.</param>
    /// <param name="cancellationToken">Cancels starting the server.</param>
    /// <returns>The running server, its identity already subscribed on the node. Dispose it, or the connection, to stop serving.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="handler"/> is null.</exception>
    /// <exception cref="ArgumentException">The identity is not a valid <c>org/namespace/app</c> name, or the shared secret is empty.</exception>
    /// <exception cref="ObjectDisposedException">The connection is disposed.</exception>
    public Task<SlimA2AServer> StartServerAsync(
        SlimA2AServerOptions options, IA2ARequestHandler handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrEmpty(options.SharedSecret, nameof(options));
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var name = SlimName.Parse(options.Identity);
        var app = _service.CreateApp(name, options.SharedSecret);
        var stopping = new CancellationTokenSource();
        try
        {
            app.Subscribe(name, _connectionId);
            var rpcServer = SlimRpcServerFactory.CreateServer(app, name, _connectionId);
            Lf.A2a.V1.A2AServiceServerRegistration.RegisterA2AServiceServer(
                rpcServer, new SlimA2AHandler(handler, options.ResolveExtendedAgentCard, stopping.Token));
            var server = new SlimA2AServer(this, options.Identity, app, rpcServer, stopping);
            _owned.TryAdd(server, 0);
            return Task.FromResult(server);
        }
        catch
        {
            stopping.Dispose();
            app.Dispose();
            throw;
        }
    }

    /// <summary>Creates a client that calls an A2A agent over SLIMRPC.</summary>
    /// <param name="options">The client's own identity and shared secret, and the agent to call.</param>
    /// <returns>The client. Dispose it, or the connection, when done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">An identity is not a valid <c>org/namespace/app</c> name, the shared secret is empty, or <see cref="SlimA2AClientOptions.Metadata"/> uses a key SLIMRPC reserves for routing.</exception>
    /// <exception cref="ObjectDisposedException">The connection is disposed.</exception>
    public SlimA2AClient CreateClient(SlimA2AClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.SharedSecret, nameof(options));
        if (options.Metadata?.Keys.FirstOrDefault(SlimA2AMetadata.Reserved.Contains) is { } reserved)
            throw new ArgumentException($"Metadata key '{reserved}' is reserved by SLIMRPC.", nameof(options));
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        using var name = SlimName.Parse(options.Identity);
        using var remote = SlimName.Parse(SlimA2AClientOptions.ToSlimName(options.Remote));
        var app = _service.CreateApp(name, options.SharedSecret);
        try
        {
            app.Subscribe(name, _connectionId);
            var channel = SlimRpcChannelFactory.CreateChannel(app, remote, _connectionId);
            var client = new SlimA2AClient(this, app, channel, options);
            _owned.TryAdd(client, 0);
            return client;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Disposes the servers and clients created from this connection, then disconnects from the node.</summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        foreach (var owned in _owned.Keys)
            await owned.DisposeAsync().ConfigureAwait(false);
        try
        {
            _service.Disconnect(_connectionId);
        }
        catch (SlimException)
        {
            // Already disconnected, e.g. the node went away.
        }
        _service.Dispose();
    }

    internal void Release(IAsyncDisposable owned) => _owned.TryRemove(owned, out _);

    /// <summary>The SLIM client configuration for <paramref name="options"/> (slim's client-config JSON schema).</summary>
    internal static string CreateClientConfigJson(SlimA2AConnectionOptions options)
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException($"Endpoint must be an absolute http:// or https:// URL, but was '{options.Endpoint}'.", nameof(options));
        if (options.ConnectTimeout <= TimeSpan.Zero)
            throw new ArgumentException($"ConnectTimeout must be positive, but was {options.ConnectTimeout}.", nameof(options));

        var tls = options.Tls ?? (uri.Scheme == "https" ? SlimA2ATls.SystemRoots : SlimA2ATls.Insecure);
        var attemptTimeout = options.ConnectTimeout < MaxAttemptTimeout ? options.ConnectTimeout : MaxAttemptTimeout;
        var attempts = Math.Max(1, (long)Math.Ceiling(options.ConnectTimeout / RetryInterval));
        return new JsonObject
        {
            ["endpoint"] = options.Endpoint,
            ["tls"] = tls.ToJson(),
            ["connect_timeout"] = ToDuration(attemptTimeout),
            // SLIM's default backoff retries forever; bound it so a connect attempt ends around the connect timeout.
            ["backoff"] = new JsonObject
            {
                ["type"] = "fixed_interval",
                ["interval"] = ToDuration(RetryInterval),
                ["max_attempts"] = attempts,
            },
        }.ToJsonString();
    }

    private static string ToDuration(TimeSpan value) => $"{(long)Math.Ceiling(value.TotalMilliseconds)}ms";

    /// <summary>The native connect can't be cancelled: if it still succeeds after the caller gave up, disconnect it.</summary>
    private static void ReleaseWhenDone(SlimService service, Task<ulong> connect) =>
        _ = connect.ContinueWith(
            t =>
            {
                try
                {
                    if (t.IsCompletedSuccessfully)
                        service.Disconnect(t.Result);
                }
                catch (SlimException)
                {
                    // Nothing left to release.
                }
                finally
                {
                    service.Dispose();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
