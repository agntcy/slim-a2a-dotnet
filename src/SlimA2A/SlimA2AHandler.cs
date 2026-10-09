using A2A;
using Agntcy.Slim.SlimRpc;
using Google.Protobuf.WellKnownTypes;

namespace SlimA2A;

/// <summary>Adapts <see cref="IA2ARequestHandler"/> to the generated <see cref="Lf.A2a.V1.IA2AServiceServer"/> contract.</summary>
/// <remarks>
/// Each RPC runs with a token cancelled when the caller's deadline passes or the server stops (<see cref="RpcCallScope"/>),
/// with <see cref="SlimA2ACallContext.Current"/> set to the call, and A2A errors and those cancellations reach the client with
/// their RPC codes.
/// </remarks>
internal sealed class SlimA2AHandler : Lf.A2a.V1.IA2AServiceServer
{
    private readonly IA2ARequestHandler _inner;
    private readonly Func<GetExtendedAgentCardRequest, CancellationToken, System.Threading.Tasks.Task<AgentCard>>? _resolveAgentCard;
    private readonly CancellationToken _serverStopping;

    /// <param name="inner">Task manager / agent pipeline (e.g. <see cref="A2AServer"/>).</param>
    /// <param name="resolveAgentCard">When set, <c>GetExtendedAgentCard</c> uses this instead of <see cref="IA2ARequestHandler.GetExtendedAgentCardAsync"/> (needed when the inner handler does not implement extended card).</param>
    /// <param name="serverStopping">Cancelled when the server stops, cancelling in-flight requests.</param>
    public SlimA2AHandler(
        IA2ARequestHandler inner,
        Func<GetExtendedAgentCardRequest, CancellationToken, System.Threading.Tasks.Task<AgentCard>>? resolveAgentCard = null,
        CancellationToken serverStopping = default)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _resolveAgentCard = resolveAgentCard;
        _serverStopping = serverStopping;
    }

    public System.Threading.Tasks.Task<Lf.A2a.V1.SendMessageResponse> SendMessage(Lf.A2a.V1.SendMessageRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct =>
            ProtoConverter.ToProto(await _inner.SendMessageAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false)));

    public IAsyncEnumerable<Lf.A2a.V1.StreamResponse> SendStreamingMessage(Lf.A2a.V1.SendMessageRequest request, SlimRpcContext context) =>
        StreamAsync(context, ct => _inner.SendStreamingMessageAsync(ProtoConverter.FromProto(request), ct));

    public System.Threading.Tasks.Task<Lf.A2a.V1.Task> GetTask(Lf.A2a.V1.GetTaskRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct =>
            ProtoConverter.ToProto(await _inner.GetTaskAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false)));

    public System.Threading.Tasks.Task<Lf.A2a.V1.ListTasksResponse> ListTasks(Lf.A2a.V1.ListTasksRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct =>
            ToProtoListTasks(await _inner.ListTasksAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false)));

    public System.Threading.Tasks.Task<Lf.A2a.V1.Task> CancelTask(Lf.A2a.V1.CancelTaskRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct =>
            ProtoConverter.ToProto(await _inner.CancelTaskAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false)));

    public IAsyncEnumerable<Lf.A2a.V1.StreamResponse> SubscribeToTask(Lf.A2a.V1.SubscribeToTaskRequest request, SlimRpcContext context) =>
        StreamAsync(context, ct => _inner.SubscribeToTaskAsync(ProtoConverter.FromProto(request), ct));

    public System.Threading.Tasks.Task<Lf.A2a.V1.TaskPushNotificationConfig> CreateTaskPushNotificationConfig(
        Lf.A2a.V1.TaskPushNotificationConfig request, SlimRpcContext context) =>
        UnaryAsync(context, async ct => ProtoConverter.ToProtoResource(
            await _inner.CreateTaskPushNotificationConfigAsync(ProtoConverter.FromProtoCreateRequest(request), ct).ConfigureAwait(false)));

    public System.Threading.Tasks.Task<Lf.A2a.V1.TaskPushNotificationConfig> GetTaskPushNotificationConfig(
        Lf.A2a.V1.GetTaskPushNotificationConfigRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct => ProtoConverter.ToProtoResource(
            await _inner.GetTaskPushNotificationConfigAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false)));

    public System.Threading.Tasks.Task<Lf.A2a.V1.ListTaskPushNotificationConfigsResponse> ListTaskPushNotificationConfigs(
        Lf.A2a.V1.ListTaskPushNotificationConfigsRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct => ToProtoListPush(
            await _inner.ListTaskPushNotificationConfigAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false)));

    public System.Threading.Tasks.Task<Lf.A2a.V1.AgentCard> GetExtendedAgentCard(Lf.A2a.V1.GetExtendedAgentCardRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct =>
        {
            var cardRequest = ProtoConverter.FromProto(request);
            var card = _resolveAgentCard is not null
                ? await _resolveAgentCard(cardRequest, ct).ConfigureAwait(false)
                : await _inner.GetExtendedAgentCardAsync(cardRequest, ct).ConfigureAwait(false);
            return ProtoConverter.ToProto(card);
        });

    public System.Threading.Tasks.Task<Empty> DeleteTaskPushNotificationConfig(Lf.A2a.V1.DeleteTaskPushNotificationConfigRequest request, SlimRpcContext context) =>
        UnaryAsync(context, async ct =>
        {
            await _inner.DeleteTaskPushNotificationConfigAsync(ProtoConverter.FromProto(request), ct).ConfigureAwait(false);
            return new Empty();
        });

    /// <summary>Runs one unary RPC within its call scope, reporting A2A errors and scope cancellations with their RPC codes.</summary>
    private async System.Threading.Tasks.Task<T> UnaryAsync<T>(
        SlimRpcContext? context, Func<CancellationToken, System.Threading.Tasks.Task<T>> handle)
    {
        using var call = NewCall(context);
        SlimA2ACallContext.Current = call.Context;
        try
        {
            return await handle(call.Token).ConfigureAwait(false);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
        catch (OperationCanceledException) when (call.IsCancellationRequested)
        {
            throw call.ToRpcError();
        }
    }

    /// <summary>Runs one streaming RPC within its call scope; the scope lives until the stream is fully read or disposed.</summary>
    private async IAsyncEnumerable<Lf.A2a.V1.StreamResponse> StreamAsync(
        SlimRpcContext? context, Func<CancellationToken, IAsyncEnumerable<A2A.StreamResponse>> open)
    {
        using var call = NewCall(context);
        await foreach (var item in A2ARpcErrorMapping.WithRpcErrors(() => open(call.Token), call).ConfigureAwait(false))
            yield return ProtoConverter.ToProtoStream(item);
    }

    // The generated stubs always pass a context; in-process callers (tests) may not, and then there is no deadline or metadata.
    private RpcCallScope NewCall(SlimRpcContext? context) => new(context?.RemainingTime, _serverStopping, context?.Metadata);

    private static Lf.A2a.V1.ListTasksResponse ToProtoListTasks(A2A.ListTasksResponse r)
    {
        var p = new Lf.A2a.V1.ListTasksResponse { NextPageToken = r.NextPageToken, TotalSize = r.TotalSize };
        foreach (var t in r.Tasks)
            p.Tasks.Add(ProtoConverter.ToProto(t));
        return p;
    }

    private static Lf.A2a.V1.ListTaskPushNotificationConfigsResponse ToProtoListPush(A2A.ListTaskPushNotificationConfigResponse r)
    {
        var p = new Lf.A2a.V1.ListTaskPushNotificationConfigsResponse();
        if (r.Configs is not null)
        {
            foreach (var c in r.Configs)
                p.Configs.Add(ProtoConverter.ToProtoResource(c));
        }
        if (r.NextPageToken is { } t)
            p.NextPageToken = t;
        return p;
    }
}
