using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>Create, Get, List and Delete TaskPushNotificationConfig over SLIMRPC.</summary>
public sealed class PushNotificationConfigTests(SlimNodeFixture node)
{
    [Fact]
    public async Task Push_notification_configs_round_trip_through_create_get_list_and_delete()
    {
        var task = await node.Client.CreateTaskAsync(TestAgent.InputRequired);

        var created = await node.Client.CreateTaskPushNotificationConfigAsync(new CreateTaskPushNotificationConfigRequest
        {
            TaskId = task.Id,
            ConfigId = "cfg-1",
            Config = new PushNotificationConfig
            {
                Url = "https://example.com/hooks/a2a",
                Token = "verify-token",
                Authentication = new AuthenticationInfo { Scheme = "Bearer", Credentials = "s3cret" },
            },
        });
        AssertConfig(created, task.Id, "cfg-1", "https://example.com/hooks/a2a");
        Assert.Equal("verify-token", created.PushNotificationConfig.Token);
        Assert.Equal("Bearer", created.PushNotificationConfig.Authentication!.Scheme);
        Assert.Equal("s3cret", created.PushNotificationConfig.Authentication.Credentials);

        var fetched = await node.Client.GetTaskPushNotificationConfigAsync(
            new GetTaskPushNotificationConfigRequest { TaskId = task.Id, Id = "cfg-1" });
        AssertConfig(fetched, task.Id, "cfg-1", "https://example.com/hooks/a2a");
        Assert.Equal("s3cret", fetched.PushNotificationConfig.Authentication!.Credentials);

        await node.Client.CreateTaskPushNotificationConfigAsync(new CreateTaskPushNotificationConfigRequest
        {
            TaskId = task.Id,
            ConfigId = "cfg-2",
            Config = new PushNotificationConfig { Url = "https://example.com/hooks/second" },
        });
        var listed = await node.Client.ListTaskPushNotificationConfigAsync(new ListTaskPushNotificationConfigRequest { TaskId = task.Id });
        Assert.Equal(["cfg-1", "cfg-2"], listed.Configs!.Select(c => c.Id));

        await node.Client.DeleteTaskPushNotificationConfigAsync(
            new DeleteTaskPushNotificationConfigRequest { TaskId = task.Id, Id = "cfg-1" });
        var remaining = await node.Client.ListTaskPushNotificationConfigAsync(new ListTaskPushNotificationConfigRequest { TaskId = task.Id });
        AssertConfig(Assert.Single(remaining.Configs!), task.Id, "cfg-2", "https://example.com/hooks/second");
    }

    private static void AssertConfig(TaskPushNotificationConfig config, string taskId, string id, string url)
    {
        Assert.Equal(taskId, config.TaskId);
        Assert.Equal(id, config.Id);
        Assert.Equal(url, config.PushNotificationConfig.Url);
    }
}
