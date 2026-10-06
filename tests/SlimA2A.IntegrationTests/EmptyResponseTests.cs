using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Responses that encode to zero bytes: an empty list, or <c>google.protobuf.Empty</c> (covered by
/// <see cref="PushNotificationConfigTests"/>). slim-rpc before 2.2.0 (Agntcy.Slim before 2.1.0) sent data frames with
/// the end-of-stream status, so the client took a zero-byte response for end-of-stream and failed with
/// "No response received".
/// </summary>
public sealed class EmptyResponseTests(SlimNodeFixture node)
{
    [Fact]
    public async Task ListTasks_for_a_context_without_tasks_returns_an_empty_list()
    {
        var response = await node.Client.ListTasksAsync(new ListTasksRequest { ContextId = TestRequests.NewId() });

        Assert.Empty(response.Tasks);
    }

    [Fact]
    public async Task ListTaskPushNotificationConfig_for_a_task_without_configs_returns_an_empty_list()
    {
        var task = await node.Client.CreateTaskAsync(TestAgent.InputRequired);

        var response = await node.Client.ListTaskPushNotificationConfigAsync(new ListTaskPushNotificationConfigRequest { TaskId = task.Id });

        Assert.Empty(response.Configs ?? []);
    }
}
