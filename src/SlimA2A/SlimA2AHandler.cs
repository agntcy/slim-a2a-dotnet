using A2A;
using Agntcy.Slim.SlimRpc;
using Google.Protobuf.WellKnownTypes;

namespace SlimA2A;

/// <summary>Adapts <see cref="IA2ARequestHandler"/> to the generated <see cref="A2a.V1.IA2AServiceServer"/> contract.</summary>
public sealed class SlimA2AHandler : A2a.V1.IA2AServiceServer
{
    private readonly IA2ARequestHandler _inner;
    private readonly Func<CancellationToken, System.Threading.Tasks.Task<AgentCard>>? _resolveAgentCard;

    /// <param name="inner">Task manager / agent pipeline (e.g. <see cref="A2AServer"/>).</param>
    /// <param name="resolveAgentCard">When set, <c>GetAgentCard</c> uses this instead of <see cref="IA2ARequestHandler.GetExtendedAgentCardAsync"/> (needed when the inner handler does not implement extended card).</param>
    public SlimA2AHandler(IA2ARequestHandler inner, Func<CancellationToken, System.Threading.Tasks.Task<AgentCard>>? resolveAgentCard = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _resolveAgentCard = resolveAgentCard;
    }

    public async System.Threading.Tasks.Task<A2a.V1.SendMessageResponse> SendMessage(A2a.V1.SendMessageRequest request, SlimRpcContext context)
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

    public async IAsyncEnumerable<A2a.V1.StreamResponse> SendStreamingMessage(A2a.V1.SendMessageRequest request, SlimRpcContext context)
    {
        IAsyncEnumerable<A2A.StreamResponse> stream;
        try
        {
            stream = _inner.SendStreamingMessageAsync(ProtoConverter.FromProto(request), CancellationToken.None);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
        await foreach (var item in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.ToProtoStream(item);
        }
    }

    public async System.Threading.Tasks.Task<A2a.V1.Task> GetTask(A2a.V1.GetTaskRequest request, SlimRpcContext context)
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

    public async System.Threading.Tasks.Task<A2a.V1.ListTasksResponse> ListTasks(A2a.V1.ListTasksRequest request, SlimRpcContext context)
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

    public async System.Threading.Tasks.Task<A2a.V1.Task> CancelTask(A2a.V1.CancelTaskRequest request, SlimRpcContext context)
    {
        try
        {
            var id = TaskResourceNames.FromResourceName(request.Name);
            var t = await _inner.CancelTaskAsync(new CancelTaskRequest { Id = id }, CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProto(t);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async IAsyncEnumerable<A2a.V1.StreamResponse> TaskSubscription(A2a.V1.TaskSubscriptionRequest request, SlimRpcContext context)
    {
        var sub = ProtoConverter.FromProto(request);
        IAsyncEnumerable<A2A.StreamResponse> stream;
        try
        {
            stream = _inner.SubscribeToTaskAsync(sub, CancellationToken.None);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
        await foreach (var item in stream.ConfigureAwait(false))
        {
            yield return ProtoConverter.ToProtoStream(item);
        }
    }

    public async System.Threading.Tasks.Task<A2a.V1.TaskPushNotificationConfig> CreateTaskPushNotificationConfig(
        A2a.V1.CreateTaskPushNotificationConfigRequest request, SlimRpcContext context)
    {
        try
        {
            var push = request.Config?.PushNotificationConfig is { } pn
                ? ProtoConverter.FromProto(pn)
                : new PushNotificationConfig();
            var r = await _inner.CreateTaskPushNotificationConfigAsync(
                new CreateTaskPushNotificationConfigRequest
                {
                    TaskId = TaskResourceNames.FromResourceName(request.Parent),
                    ConfigId = request.ConfigId,
                    Config = push,
                },
                CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProtoResource(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<A2a.V1.TaskPushNotificationConfig> GetTaskPushNotificationConfig(
        A2a.V1.GetTaskPushNotificationConfigRequest request, SlimRpcContext context)
    {
        try
        {
            var (taskId, configId) = TaskResourceNames.ParsePushConfigResourceName(request.Name);
            var r = await _inner.GetTaskPushNotificationConfigAsync(
                new GetTaskPushNotificationConfigRequest { TaskId = taskId, Id = configId },
                CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProtoResource(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<A2a.V1.ListTaskPushNotificationConfigResponse> ListTaskPushNotificationConfig(
        A2a.V1.ListTaskPushNotificationConfigRequest request, SlimRpcContext context)
    {
        try
        {
            var r = await _inner.ListTaskPushNotificationConfigAsync(
                new ListTaskPushNotificationConfigRequest
                {
                    TaskId = TaskResourceNames.FromResourceName(request.Parent),
                    PageSize = request.PageSize == 0 ? null : request.PageSize,
                    PageToken = string.IsNullOrEmpty(request.PageToken) ? null : request.PageToken,
                },
                CancellationToken.None).ConfigureAwait(false);
            return ToProtoListPush(r);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<A2a.V1.AgentCard> GetAgentCard(A2a.V1.GetAgentCardRequest request, SlimRpcContext context)
    {
        try
        {
            var card = _resolveAgentCard is not null
                ? await _resolveAgentCard(CancellationToken.None).ConfigureAwait(false)
                : await _inner.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest(), CancellationToken.None).ConfigureAwait(false);
            return ProtoConverter.ToProto(card);
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    public async System.Threading.Tasks.Task<Empty> DeleteTaskPushNotificationConfig(A2a.V1.DeleteTaskPushNotificationConfigRequest request, SlimRpcContext context)
    {
        try
        {
            var (taskId, configId) = TaskResourceNames.ParsePushConfigResourceName(request.Name);
            await _inner.DeleteTaskPushNotificationConfigAsync(
                new DeleteTaskPushNotificationConfigRequest { TaskId = taskId, Id = configId },
                CancellationToken.None).ConfigureAwait(false);
            return new Empty();
        }
        catch (A2AException ex)
        {
            throw A2ARpcErrorMapping.ToRpc(ex);
        }
    }

    private static A2a.V1.ListTasksResponse ToProtoListTasks(A2A.ListTasksResponse r)
    {
        var p = new A2a.V1.ListTasksResponse { NextPageToken = r.NextPageToken, TotalSize = r.TotalSize };
        foreach (var t in r.Tasks)
            p.Tasks.Add(ProtoConverter.ToProto(t));
        return p;
    }

    private static A2a.V1.ListTaskPushNotificationConfigResponse ToProtoListPush(A2A.ListTaskPushNotificationConfigResponse r)
    {
        var p = new A2a.V1.ListTaskPushNotificationConfigResponse();
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
