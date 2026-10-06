using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>GetTask, ListTasks, CancelTask and SubscribeToTask over SLIMRPC.</summary>
public sealed class TaskTests(SlimNodeFixture node)
{
    [Fact]
    public async Task GetTask_returns_the_task_with_its_artifacts()
    {
        var created = await node.Client.CreateTaskAsync("fetch me");

        var fetched = await node.Client.GetTaskAsync(new GetTaskRequest { Id = created.Id });

        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.ContextId, fetched.ContextId);
        Assert.Equal(TaskState.Completed, fetched.Status.State);
        Assert.Equal("fetch me", Assert.Single(Assert.Single(fetched.Artifacts!).Parts).Text);
    }

    [Fact]
    public async Task Continuing_a_task_completes_it_and_GetTask_returns_its_history()
    {
        var task = await node.Client.CreateTaskAsync(TestAgent.InputRequired);
        Assert.Equal(TaskState.InputRequired, task.Status.State);

        var continued = await node.Client.SendMessageAsync(TestRequests.Text(TestAgent.Continue, task.ContextId, task.Id));

        Assert.Equal(task.Id, continued.Task!.Id);
        Assert.Equal(TaskState.Completed, continued.Task.Status.State);
        Assert.Equal(TestAgent.ContinueReply, Assert.Single(Assert.Single(continued.Task.Artifacts!).Parts).Text);

        // A2AServer records the turn's input-required prompt in history once the task moves on.
        var fetched = await node.Client.GetTaskAsync(new GetTaskRequest { Id = task.Id });
        Assert.Contains(fetched.History!, m => m.Role == Role.Agent && m.Parts?.FirstOrDefault()?.Text == TestAgent.InputPrompt);
    }

    [Fact]
    public async Task ListTasks_filters_by_context_and_pages_with_a_token()
    {
        var contextId = TestRequests.NewId();
        var first = await node.Client.CreateTaskAsync("list a", contextId);
        var second = await node.Client.CreateTaskAsync("list b", contextId);
        await node.Client.CreateTaskAsync("other context", TestRequests.NewId());

        var all = await node.Client.ListTasksAsync(new ListTasksRequest { ContextId = contextId });
        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            all.Tasks.Select(t => t.Id).Order());

        var page1 = await node.Client.ListTasksAsync(new ListTasksRequest { ContextId = contextId, PageSize = 1 });
        Assert.Single(page1.Tasks);
        Assert.False(string.IsNullOrEmpty(page1.NextPageToken));

        var page2 = await node.Client.ListTasksAsync(
            new ListTasksRequest { ContextId = contextId, PageSize = 1, PageToken = page1.NextPageToken });
        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            page1.Tasks.Concat(page2.Tasks).Select(t => t.Id).Order());
    }

    [Fact]
    public async Task CancelTask_cancels_an_input_required_task()
    {
        var task = await node.Client.CreateTaskAsync(TestAgent.InputRequired);
        Assert.Equal(TaskState.InputRequired, task.Status.State);

        var canceled = await node.Client.CancelTaskAsync(new CancelTaskRequest { Id = task.Id });

        Assert.Equal(task.Id, canceled.Id);
        Assert.Equal(TaskState.Canceled, canceled.Status.State);
        Assert.Equal(TaskState.Canceled, (await node.Client.GetTaskAsync(new GetTaskRequest { Id = task.Id })).Status.State);
    }

    [Fact]
    public async Task SubscribeToTask_streams_live_updates_until_the_task_is_canceled()
    {
        var task = await node.Client.CreateTaskAsync(TestAgent.InputRequired);

        var events = new List<StreamResponse>();
        await using var subscription = node.Client.SubscribeToTaskAsync(new SubscribeToTaskRequest { Id = task.Id }).GetAsyncEnumerator();

        // The subscription opens with the task's current state...
        Assert.True(await subscription.MoveNextAsync());
        events.Add(subscription.Current);
        Assert.Equal(task.Id, MessagingTests.TaskIdOf(subscription.Current));

        // ...then delivers live updates: canceling ends the stream with a canceled status.
        await node.Client.CancelTaskAsync(new CancelTaskRequest { Id = task.Id });
        while (await subscription.MoveNextAsync())
            events.Add(subscription.Current);

        var last = events[^1];
        Assert.Equal(StreamResponseCase.StatusUpdate, last.PayloadCase);
        Assert.Equal(TaskState.Canceled, last.StatusUpdate!.Status.State);
    }
}
