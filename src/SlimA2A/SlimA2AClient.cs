using System.Collections.Concurrent;
using A2A;
using Agntcy.Slim;
using uniffi.slim_rpc;

namespace SlimA2A;

/// <summary>An <see cref="IA2AClient"/> that calls an A2A agent over SLIMRPC, created by <see cref="SlimA2AConnection.CreateClient"/>.</summary>
public sealed class SlimA2AClient : IA2AClient, IAsyncDisposable
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly SlimA2AConnection _connection;
    private readonly SlimApp _app;
    private readonly Channel _channel;
    private readonly Lf.A2a.V1.A2AServiceClient _client;
    private readonly TimeSpan? _defaultTimeout;
    private readonly ConcurrentDictionary<string, AgentCard> _extendedCards = new();
    private int _disposed;

    internal SlimA2AClient(SlimA2AConnection connection, SlimApp app, Channel channel, SlimA2AClientOptions options)
    {
        _connection = connection;
        _app = app;
        _channel = channel;
        _client = new Lf.A2a.V1.A2AServiceClient(channel);
        _defaultTimeout = options.DefaultTimeout;
        Identity = options.Identity;
        Remote = SlimA2AClientOptions.ToSlimName(options.Remote);
    }

    /// <summary>The client's own SLIM identity.</summary>
    public string Identity { get; }

    /// <summary>The SLIM identity of the agent this client calls.</summary>
    public string Remote { get; }

    /// <summary>Closes the client's SLIMRPC sessions and releases its SLIM identity.</summary>
    /// <returns>A task that completes when the client is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            await _channel.CloseAsync(CloseTimeout).ConfigureAwait(false);
        }
        catch (RpcException)
        {
            // Closing is best effort; the node may already be gone.
        }
        finally
        {
            _channel.Dispose();
            _app.Dispose();
            _connection.Release(this);
        }
    }

    /// <inheritdoc />
    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.SendMessageAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamResponse> SendStreamingMessageAsync(
        SendMessageRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var proto = ProtoConverter.ToProto(request);
        var stream = A2ARpcErrorMapping.WithA2AErrors(
            _client.SendStreamingMessageAsync(proto, _defaultTimeout, null, cancellationToken), cancellationToken);
        await foreach (var p in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.FromProtoStream(p);
        }
    }

    /// <inheritdoc />
    public async Task<AgentTask> GetTaskAsync(GetTaskRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.GetTaskAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.ListTasksAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async Task<AgentTask> CancelTaskAsync(CancelTaskRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.CancelTaskAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamResponse> SubscribeToTaskAsync(
        SubscribeToTaskRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var proto = ProtoConverter.ToProto(request);
        var stream = A2ARpcErrorMapping.WithA2AErrors(
            _client.SubscribeToTaskAsync(proto, _defaultTimeout, null, cancellationToken), cancellationToken);
        await foreach (var p in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.FromProtoStream(p);
        }
    }

    /// <inheritdoc />
    public async Task<TaskPushNotificationConfig> CreateTaskPushNotificationConfigAsync(
        CreateTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.CreateTaskPushNotificationConfigAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async Task<TaskPushNotificationConfig> GetTaskPushNotificationConfigAsync(
        GetTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.GetTaskPushNotificationConfigAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async Task<ListTaskPushNotificationConfigResponse> ListTaskPushNotificationConfigAsync(
        ListTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.ListTaskPushNotificationConfigsAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    /// <inheritdoc />
    public async Task DeleteTaskPushNotificationConfigAsync(
        DeleteTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        await InvokeAsync(
            () => _client.DeleteTaskPushNotificationConfigAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AgentCard> GetExtendedAgentCardAsync(
        GetExtendedAgentCardRequest request, CancellationToken cancellationToken = default)
    {
        // Cached per tenant: a multi-tenant agent serves a different card to each.
        var tenant = request.Tenant ?? string.Empty;
        if (_extendedCards.TryGetValue(tenant, out var cached))
            return cached;
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.GetExtendedAgentCardAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return _extendedCards.GetOrAdd(tenant, ProtoConverter.FromProto(resp));
    }

    private async Task<T> InvokeAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (RpcException.Rpc ex)
        {
            throw A2ARpcErrorMapping.FromRpc(ex);
        }
    }

    private async Task InvokeAsync(Func<Task> call, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await call().ConfigureAwait(false);
        }
        catch (RpcException.Rpc ex)
        {
            throw A2ARpcErrorMapping.FromRpc(ex);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
