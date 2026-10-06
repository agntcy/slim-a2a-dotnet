using A2A;
using Agntcy.Slim.SlimRpc;
using Google.Protobuf.WellKnownTypes;

namespace SlimA2A;

/// <summary>Adapts <see cref="IA2ARequestHandler"/> to the generated <see cref="Lf.A2a.V1.IA2AServiceServer"/> contract.</summary>
internal sealed class SlimA2AHandler : Lf.A2a.V1.IA2AServiceServer
{
    private readonly IA2ARequestHandler _inner;
    private readonly Func<GetExtendedAgentCardRequest, CancellationToken, System.Threading.Tasks.Task<AgentCard>>? _resolveAgentCard;

    /// <param name="inner">Task manager / agent pipeline (e.g. <see cref="A2AServer"/>).</param>
    /// <param name="resolveAgentCard">When set, <c>GetExtendedAgentCard</c> uses this instead of <see cref="IA2ARequestHandler.GetExtendedAgentCardAsync"/> (needed when the inner handler does not implement extended card).</param>
    public SlimA2AHandler(
        IA2ARequestHandler inner,
        Func<GetExtendedAgentCardRequest, CancellationToken, System.Threading.Tasks.Task<AgentCard>>? resolveAgentCard = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _resolveAgentCard = resolveAgentCard;
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.SendMessageResponse> SendMessage(Lf.A2a.V1.SendMessageRequest request, SlimRpcContext context)
    {
        try
        {
            var r = await _inner.SendMessageAsync(ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProto(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async IAsyncEnumerable<Lf.A2a.V1.StreamResponse> SendStreamingMessage(Lf.A2a.V1.SendMessageRequest request, SlimRpcContext context)
    {
        var stream = A2ARpcErrorMapping.WithRpcErrors(
            () => _inner.SendStreamingMessageAsync(ProtoConverter.FromProto(request), CancellationToken.None));
        await foreach (var item in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.ToProtoStream(item);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.Task> GetTask(Lf.A2a.V1.GetTaskRequest request, SlimRpcContext context)
    {
        try
        {
            var t = await _inner.GetTaskAsync(ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProto(t);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.ListTasksResponse> ListTasks(Lf.A2a.V1.ListTasksRequest request, SlimRpcContext context)
    {
        try
        {
            var r = await _inner.ListTasksAsync(ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return ToProtoListTasks(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.Task> CancelTask(Lf.A2a.V1.CancelTaskRequest request, SlimRpcContext context)
    {
        try
        {
            var t = await _inner.CancelTaskAsync(ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProto(t);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async IAsyncEnumerable<Lf.A2a.V1.StreamResponse> SubscribeToTask(Lf.A2a.V1.SubscribeToTaskRequest request, SlimRpcContext context)
    {
        var stream = A2ARpcErrorMapping.WithRpcErrors(
            () => _inner.SubscribeToTaskAsync(ProtoConverter.FromProto(request), CancellationToken.None));
        await foreach (var item in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.ToProtoStream(item);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.TaskPushNotificationConfig> CreateTaskPushNotificationConfig(
        Lf.A2a.V1.TaskPushNotificationConfig request, SlimRpcContext context)
    {
        try
        {
            var r = await _inner.CreateTaskPushNotificationConfigAsync(
                ProtoConverter.FromProtoCreateRequest(request), CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProtoResource(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.TaskPushNotificationConfig> GetTaskPushNotificationConfig(
        Lf.A2a.V1.GetTaskPushNotificationConfigRequest request, SlimRpcContext context)
    {
        try
        {
            var r = await _inner.GetTaskPushNotificationConfigAsync(
                ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProtoResource(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.ListTaskPushNotificationConfigsResponse> ListTaskPushNotificationConfigs(
        Lf.A2a.V1.ListTaskPushNotificationConfigsRequest request, SlimRpcContext context)
    {
        try
        {
            var r = await _inner.ListTaskPushNotificationConfigAsync(
                ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return ToProtoListPush(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Lf.A2a.V1.AgentCard> GetExtendedAgentCard(Lf.A2a.V1.GetExtendedAgentCardRequest request, SlimRpcContext context)
    {
        try
        {
            var cardRequest = ProtoConverter.FromProto(request);
            var card = _resolveAgentCard is not null
                ? await _resolveAgentCard(cardRequest, CancellationToken.None).ConfigureAwait(false)
                : await _inner.GetExtendedAgentCardAsync(cardRequest, CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProto(card);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Empty> DeleteTaskPushNotificationConfig(Lf.A2a.V1.DeleteTaskPushNotificationConfigRequest request, SlimRpcContext context)
    {
        try
        {
            await _inner.DeleteTaskPushNotificationConfigAsync(
                ProtoConverter.FromProto(request), CancellationToken.None).ConfigureAwait(false);
            return new Empty();
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

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
