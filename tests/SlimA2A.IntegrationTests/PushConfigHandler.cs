using System.Collections.Concurrent;
using A2A;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Delegates to <see cref="A2AServer"/> but keeps push notification configs in memory. The A2A package ships no push
/// config store, and what's under test here is the SLIMRPC transport and proto conversion of these RPCs.
/// </summary>
internal sealed class PushConfigHandler(IA2ARequestHandler inner) : IA2ARequestHandler
{
    private readonly ConcurrentDictionary<(string TaskId, string Id), TaskPushNotificationConfig> _configs = new();

    public Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default) =>
        inner.SendMessageAsync(request, cancellationToken);

    public IAsyncEnumerable<StreamResponse> SendStreamingMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default) =>
        inner.SendStreamingMessageAsync(request, cancellationToken);

    public Task<AgentTask> GetTaskAsync(GetTaskRequest request, CancellationToken cancellationToken = default) =>
        inner.GetTaskAsync(request, cancellationToken);

    public Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default) =>
        inner.ListTasksAsync(request, cancellationToken);

    public Task<AgentTask> CancelTaskAsync(CancelTaskRequest request, CancellationToken cancellationToken = default) =>
        inner.CancelTaskAsync(request, cancellationToken);

    public IAsyncEnumerable<StreamResponse> SubscribeToTaskAsync(SubscribeToTaskRequest request, CancellationToken cancellationToken = default) =>
        inner.SubscribeToTaskAsync(request, cancellationToken);

    public Task<AgentCard> GetExtendedAgentCardAsync(GetExtendedAgentCardRequest request, CancellationToken cancellationToken = default) =>
        inner.GetExtendedAgentCardAsync(request, cancellationToken);

    public Task<TaskPushNotificationConfig> CreateTaskPushNotificationConfigAsync(
        CreateTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        var config = new TaskPushNotificationConfig
        {
            Id = request.ConfigId,
            TaskId = request.TaskId,
            PushNotificationConfig = request.Config,
        };
        _configs[(request.TaskId, request.ConfigId)] = config;
        return Task.FromResult(config);
    }

    public Task<TaskPushNotificationConfig> GetTaskPushNotificationConfigAsync(
        GetTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default) =>
        _configs.TryGetValue((request.TaskId, request.Id), out var config)
            ? Task.FromResult(config)
            : throw new A2AException($"Push notification config '{request.Id}' not found.", A2AErrorCode.InvalidParams);

    public Task<ListTaskPushNotificationConfigResponse> ListTaskPushNotificationConfigAsync(
        ListTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ListTaskPushNotificationConfigResponse
        {
            Configs = _configs.Values.Where(c => c.TaskId == request.TaskId).OrderBy(c => c.Id).ToList(),
        });

    public Task DeleteTaskPushNotificationConfigAsync(
        DeleteTaskPushNotificationConfigRequest request, CancellationToken cancellationToken = default)
    {
        _configs.TryRemove((request.TaskId, request.Id), out _);
        return Task.CompletedTask;
    }
}
