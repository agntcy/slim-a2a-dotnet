using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>The tenant set on a client request reaches the server for every RPC.</summary>
public sealed class TenantTests(SlimNodeFixture node)
{
    [Fact]
    public async Task Every_RPC_delivers_the_tenant_to_the_server()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = $"tenant-{TestRequests.NewId()}";

        var task = (await node.Client.SendMessageAsync(TestRequests.Text(TestAgent.InputRequired, tenant: tenant), ct)).Task!;
        await node.Client.SendStreamingMessageAsync(TestRequests.Text(TestAgent.Streaming, tenant: tenant), ct).ToListAsync();
        await node.Client.GetTaskAsync(new GetTaskRequest { Id = task.Id, Tenant = tenant }, ct);
        await node.Client.ListTasksAsync(new ListTasksRequest { ContextId = task.ContextId, Tenant = tenant }, ct);
        await node.Client.CreateTaskPushNotificationConfigAsync(new CreateTaskPushNotificationConfigRequest
        {
            TaskId = task.Id,
            ConfigId = "cfg",
            Config = new PushNotificationConfig { Url = "https://example.com/hook" },
            Tenant = tenant,
        }, ct);
        await node.Client.GetTaskPushNotificationConfigAsync(new GetTaskPushNotificationConfigRequest { TaskId = task.Id, Id = "cfg", Tenant = tenant }, ct);
        await node.Client.ListTaskPushNotificationConfigAsync(new ListTaskPushNotificationConfigRequest { TaskId = task.Id, Tenant = tenant }, ct);
        await node.Client.DeleteTaskPushNotificationConfigAsync(new DeleteTaskPushNotificationConfigRequest { TaskId = task.Id, Id = "cfg", Tenant = tenant }, ct);
        await node.Client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest { Tenant = tenant }, ct);
        await node.Client.CancelTaskAsync(new CancelTaskRequest { Id = task.Id, Tenant = tenant }, ct);
        // An unknown task still reaches the server's handler (which then reports TaskNotFound).
        await Assert.ThrowsAsync<A2AException>(() =>
            node.Client.SubscribeToTaskAsync(new SubscribeToTaskRequest { Id = "no-such-task", Tenant = tenant }, ct).ToListAsync());

        string[] rpcs =
        [
            nameof(IA2ARequestHandler.SendMessageAsync),
            nameof(IA2ARequestHandler.SendStreamingMessageAsync),
            nameof(IA2ARequestHandler.GetTaskAsync),
            nameof(IA2ARequestHandler.ListTasksAsync),
            nameof(IA2ARequestHandler.CancelTaskAsync),
            nameof(IA2ARequestHandler.SubscribeToTaskAsync),
            nameof(IA2ARequestHandler.CreateTaskPushNotificationConfigAsync),
            nameof(IA2ARequestHandler.GetTaskPushNotificationConfigAsync),
            nameof(IA2ARequestHandler.ListTaskPushNotificationConfigAsync),
            nameof(IA2ARequestHandler.DeleteTaskPushNotificationConfigAsync),
            nameof(IA2ARequestHandler.GetExtendedAgentCardAsync),
        ];
        Assert.All(rpcs, rpc => Assert.Equal(tenant, node.Handler.LastTenant.GetValueOrDefault(rpc)));
    }

    [Fact]
    public async Task GetExtendedAgentCard_is_cached_per_tenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = $"tenant-a-{TestRequests.NewId()}";
        var b = $"tenant-b-{TestRequests.NewId()}";

        await node.Client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest { Tenant = a }, ct);
        await node.Client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest { Tenant = a }, ct);
        await node.Client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest { Tenant = b }, ct);

        Assert.Equal(1, node.Handler.CardRequests.GetValueOrDefault(a));
        Assert.Equal(1, node.Handler.CardRequests.GetValueOrDefault(b));
    }
}
