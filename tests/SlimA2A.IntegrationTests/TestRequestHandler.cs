using System.Collections.Concurrent;
using A2A;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Delegates to <see cref="A2AServer"/>, and adds what tests need on the server side: push notification configs kept
/// in memory (the A2A package ships no store), the agent card, and a record of the tenant each RPC received.
/// </summary>
internal sealed class TestRequestHandler(IA2ARequestHandler inner, AgentCard card) : IA2ARequestHandler
{
    private readonly ConcurrentDictionary<(string TaskId, string Id), TaskPushNotificationConfig> _configs = new();

    /// <summary>The tenant each RPC last received, by handler method name.</summary>
    public ConcurrentDictionary<string, string?> LastTenant { get; } = new();

    /// <summary>The <see cref="SlimA2ACallContext.Current"/> each RPC last ran with, by handler method name.</summary>
    public ConcurrentDictionary<string, SlimA2ACallContext?> LastCallContext { get; } = new();

    /// <summary>How many extended-card requests reached the server, by tenant.</summary>
    public ConcurrentDictionary<string, int> CardRequests { get; } = new();

    public Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(SendMessageAsync)] = request.Tenant;
        LastCallContext[nameof(SendMessageAsync)] = SlimA2ACallContext.Current;
        return inner.SendMessageAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<StreamResponse> SendStreamingMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(SendStreamingMessageAsync)] = request.Tenant;
        LastCallContext[nameof(SendStreamingMessageAsync)] = SlimA2ACallContext.Current;
        return inner.SendStreamingMessageAsync(request, cancellationToken);
    }

    public Task<AgentTask> GetTaskAsync(GetTaskRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(GetTaskAsync)] = request.Tenant;
        LastCallContext[nameof(GetTaskAsync)] = SlimA2ACallContext.Current;
        return inner.GetTaskAsync(request, cancellationToken);
    }

    public Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(ListTasksAsync)] = request.Tenant;
        LastCallContext[nameof(ListTasksAsync)] = SlimA2ACallContext.Current;
        return inner.ListTasksAsync(request, cancellationToken);
    }

    public Task<AgentTask> CancelTaskAsync(CancelTaskRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(CancelTaskAsync)] = request.Tenant;
        LastCallContext[nameof(CancelTaskAsync)] = SlimA2ACallContext.Current;
        return inner.CancelTaskAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<StreamResponse> SubscribeToTaskAsync(SubscribeToTaskRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(SubscribeToTaskAsync)] = request.Tenant;
        LastCallContext[nameof(SubscribeToTaskAsync)] = SlimA2ACallContext.Current;
        return inner.SubscribeToTaskAsync(request, cancellationToken);
    }

    public Task<AgentCard> GetExtendedAgentCardAsync(GetExtendedAgentCardRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(GetExtendedAgentCardAsync)] = request.Tenant;
        LastCallContext[nameof(GetExtendedAgentCardAsync)] = SlimA2ACallContext.Current;
        CardRequests.AddOrUpdate(request.Tenant ?? string.Empty, 1, (_, n) => n + 1);
        return Task.FromResult(card);
    }

    public Task<TaskPushNotificationConfig> CreateTaskPushNotificationConfigAsync(
        CreateTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(CreateTaskPushNotificationConfigAsync)] = request.Tenant;
        LastCallContext[nameof(CreateTaskPushNotificationConfigAsync)] = SlimA2ACallContext.Current;
        var config = new TaskPushNotificationConfig
        {
            Id = request.ConfigId,
            TaskId = request.TaskId,
            PushNotificationConfig = request.Config,
            Tenant = request.Tenant,
        };
        _configs[(request.TaskId, request.ConfigId)] = config;
        return Task.FromResult(config);
    }

    public Task<TaskPushNotificationConfig> GetTaskPushNotificationConfigAsync(
        GetTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(GetTaskPushNotificationConfigAsync)] = request.Tenant;
        LastCallContext[nameof(GetTaskPushNotificationConfigAsync)] = SlimA2ACallContext.Current;
        return _configs.TryGetValue((request.TaskId, request.Id), out var config)
            ? Task.FromResult(config)
            : throw new A2AException($"Push notification config '{request.Id}' not found.", A2AErrorCode.InvalidParams);
    }

    public Task<ListTaskPushNotificationConfigResponse> ListTaskPushNotificationConfigAsync(
        ListTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(ListTaskPushNotificationConfigAsync)] = request.Tenant;
        LastCallContext[nameof(ListTaskPushNotificationConfigAsync)] = SlimA2ACallContext.Current;
        return Task.FromResult(new ListTaskPushNotificationConfigResponse
        {
            Configs = _configs.Values.Where(c => c.TaskId == request.TaskId).OrderBy(c => c.Id).ToList(),
        });
    }

    public Task DeleteTaskPushNotificationConfigAsync(
        DeleteTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        LastTenant[nameof(DeleteTaskPushNotificationConfigAsync)] = request.Tenant;
        LastCallContext[nameof(DeleteTaskPushNotificationConfigAsync)] = SlimA2ACallContext.Current;
        _configs.TryRemove((request.TaskId, request.Id), out _);
        return Task.CompletedTask;
    }
}
