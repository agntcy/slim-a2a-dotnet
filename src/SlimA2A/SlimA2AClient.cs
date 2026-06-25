using A2A;
using uniffi.slim_bindings;

namespace SlimA2A;

/// <summary><see cref="IA2AClient"/> over SLIMRPC (generated <see cref="Lf.A2a.V1.A2AServiceClient"/>).</summary>
public sealed class SlimA2AClient : IA2AClient
{
    private readonly Lf.A2a.V1.A2AServiceClient _client;
    private readonly TimeSpan? _defaultTimeout;
    private AgentCard? _cachedExtendedCard;

    public SlimA2AClient(uniffi.slim_bindings.Channel channel, TimeSpan? defaultTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _client = new Lf.A2a.V1.A2AServiceClient(channel);
        _defaultTimeout = defaultTimeout;
    }

    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.SendMessageAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async IAsyncEnumerable<StreamResponse> SendStreamingMessageAsync(
        SendMessageRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        IAsyncEnumerable<Lf.A2a.V1.StreamResponse> stream;
        try
        {
            stream = _client.SendStreamingMessageAsync(proto, _defaultTimeout, null, cancellationToken);
        }
        catch (RpcException.Rpc ex)
        {
            throw A2ARpcErrorMapping.FromRpc(ex);
        }
        await foreach (var p in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.FromProtoStream(p);
        }
    }

    public async Task<AgentTask> GetTaskAsync(GetTaskRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.GetTaskAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.ListTasksAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async Task<AgentTask> CancelTaskAsync(CancelTaskRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.CancelTaskAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async IAsyncEnumerable<StreamResponse> SubscribeToTaskAsync(
        SubscribeToTaskRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        IAsyncEnumerable<Lf.A2a.V1.StreamResponse> stream;
        try
        {
            stream = _client.SubscribeToTaskAsync(proto, _defaultTimeout, null, cancellationToken);
        }
        catch (RpcException.Rpc ex)
        {
            throw A2ARpcErrorMapping.FromRpc(ex);
        }
        await foreach (var p in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.FromProtoStream(p);
        }
    }

    public async Task<TaskPushNotificationConfig> CreateTaskPushNotificationConfigAsync(
        CreateTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.CreateTaskPushNotificationConfigAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async Task<TaskPushNotificationConfig> GetTaskPushNotificationConfigAsync(
        GetTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.GetTaskPushNotificationConfigAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async Task<ListTaskPushNotificationConfigResponse> ListTaskPushNotificationConfigAsync(
        ListTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        var resp = await InvokeAsync(
            () => _client.ListTaskPushNotificationConfigsAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return ProtoConverter.FromProto(resp);
    }

    public async Task DeleteTaskPushNotificationConfigAsync(
        DeleteTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var proto = ProtoConverter.ToProto(request);
        await InvokeAsync(
            () => _client.DeleteTaskPushNotificationConfigAsync(proto, _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentCard> GetExtendedAgentCardAsync(
        GetExtendedAgentCardRequest request, CancellationToken cancellationToken = default)
    {
        if (_cachedExtendedCard is not null)
            return _cachedExtendedCard;
        var resp = await InvokeAsync(
            () => _client.GetExtendedAgentCardAsync(new Lf.A2a.V1.GetExtendedAgentCardRequest(), _defaultTimeout, null, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        _cachedExtendedCard = ProtoConverter.FromProto(resp);
        return _cachedExtendedCard;
    }

    private async Task<T> InvokeAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
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
}
