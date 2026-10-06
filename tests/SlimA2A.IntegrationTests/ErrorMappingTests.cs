using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// A2A errors raised by the server reach the client as <see cref="A2AException"/> with the mapped code — over the wire,
/// for unary and streaming RPCs alike.
/// </summary>
public sealed class ErrorMappingTests(SlimNodeFixture node)
{
    [Fact]
    public async Task GetTask_for_an_unknown_task_raises_TaskNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var ex = await Assert.ThrowsAsync<A2AException>(
            () => node.Client.GetTaskAsync(new GetTaskRequest { Id = "no-such-task" }, ct));

        Assert.Equal(A2AErrorCode.TaskNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task CancelTask_for_an_unknown_task_raises_TaskNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var ex = await Assert.ThrowsAsync<A2AException>(
            () => node.Client.CancelTaskAsync(new CancelTaskRequest { Id = "no-such-task" }, ct));

        Assert.Equal(A2AErrorCode.TaskNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task CancelTask_for_a_completed_task_raises_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await node.Client.CreateTaskAsync("already done", cancellationToken: ct);

        var ex = await Assert.ThrowsAsync<A2AException>(
            () => node.Client.CancelTaskAsync(new CancelTaskRequest { Id = task.Id }, ct));

        // TaskNotCancelable travels as FailedPrecondition, which the client can only map back to UnsupportedOperation:
        // FailedPrecondition carries several A2A errors (see A2ARpcErrorMapping).
        Assert.Equal(A2AErrorCode.UnsupportedOperation, ex.ErrorCode);
    }

    [Fact]
    public async Task SendMessage_agent_error_raises_its_code_and_message()
    {
        var ct = TestContext.Current.CancellationToken;
        // Fails after submitting the task: A2AServer reports an agent that fails before emitting anything as
        // InvalidAgentResponse on the unary path, so that case would test A2AServer rather than this transport.
        var ex = await Assert.ThrowsAsync<A2AException>(
            () => node.Client.SendMessageAsync(TestRequests.Text(TestAgent.FailAfterSubmit), ct));

        Assert.Equal(A2AErrorCode.InvalidParams, ex.ErrorCode);
        Assert.Equal(TestAgent.FailureMessage, ex.Message);
    }

    [Fact]
    public async Task SendStreamingMessage_agent_error_before_any_event_raises_its_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var ex = await Assert.ThrowsAsync<A2AException>(
            () => node.Client.SendStreamingMessageAsync(TestRequests.Text(TestAgent.FailInvalidParams), ct).ToListAsync());

        Assert.Equal(A2AErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task SendStreamingMessage_agent_error_mid_stream_raises_its_code_after_earlier_events()
    {
        var ct = TestContext.Current.CancellationToken;
        var received = 0;

        var ex = await Assert.ThrowsAsync<A2AException>(async () =>
        {
            await foreach (var _ in node.Client.SendStreamingMessageAsync(TestRequests.Text(TestAgent.FailAfterSubmit), ct))
                received++;
        });

        Assert.True(received >= 1, "the submitted-task event should arrive before the error");
        Assert.Equal(A2AErrorCode.InvalidParams, ex.ErrorCode);
        Assert.Equal(TestAgent.FailureMessage, ex.Message);
    }

    [Fact]
    public async Task SubscribeToTask_for_an_unknown_task_raises_TaskNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var ex = await Assert.ThrowsAsync<A2AException>(
            () => node.Client.SubscribeToTaskAsync(new SubscribeToTaskRequest { Id = "no-such-task" }, ct).ToListAsync());

        Assert.Equal(A2AErrorCode.TaskNotFound, ex.ErrorCode);
    }
}
