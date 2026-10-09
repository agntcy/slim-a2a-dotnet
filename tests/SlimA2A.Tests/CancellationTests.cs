using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using uniffi.slim_rpc;
using Xunit;

namespace SlimA2A.Tests;

/// <summary>Deadlines and cancellation, without a SLIM node.</summary>
public sealed class CancellationTests
{
    private static readonly TimeSpan Prompt = TimeSpan.FromSeconds(5);

    /// <summary>Blocks until cancelled, and reports whether it was.</summary>
    private sealed class BlockingAgent : IAgentHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
        {
            var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
            if (context.UserText == "input")
            {
                await updater.SubmitAsync(cancellationToken).ConfigureAwait(false);
                await updater.RequireInputAsync(
                    new Message { Role = Role.Agent, MessageId = "p", Parts = [Part.FromText("?")] }, cancellationToken).ConfigureAwait(false);
                return;
            }
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }

        public Task CancelAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>A stream whose reads ignore cancellation, like SLIM's: each read completes only when the test says so.</summary>
    private sealed class UncancellableStream : IAsyncEnumerable<int>, IAsyncEnumerator<int>
    {
        public TaskCompletionSource<bool> Read { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Current => 0;
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => new(Read.Task);

        public ValueTask DisposeAsync()
        {
            Disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private static Lf.A2a.V1.SendMessageRequest ProtoRequest(string text) =>
        ProtoConverter.ToProto(new SendMessageRequest
        {
            Message = new Message { Role = Role.User, MessageId = Guid.NewGuid().ToString("N"), Parts = [Part.FromText(text)] },
        });

    [Fact]
    public async Task Call_scope_is_cancelled_at_the_deadline_and_reports_DeadlineExceeded()
    {
        using var scope = new RpcCallScope(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        await Task.Delay(Timeout.Infinite, scope.Token).ContinueWith(_ => { }, TaskScheduler.Default).WaitAsync(Prompt, TestContext.Current.CancellationToken);

        Assert.True(scope.IsCancellationRequested);
        Assert.Equal(RpcCode.DeadlineExceeded, scope.ToRpcError().code);
    }

    [Fact]
    public void Call_scope_is_cancelled_when_the_server_stops_and_reports_Unavailable()
    {
        using var stopping = new CancellationTokenSource();
        using var scope = new RpcCallScope(TimeSpan.FromHours(1), stopping.Token);

        stopping.Cancel();

        Assert.True(scope.IsCancellationRequested);
        Assert.Equal(RpcCode.Unavailable, scope.ToRpcError().code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(100.0 * 24)] // beyond a CancellationTokenSource timer's range
    public void Call_scope_without_a_reachable_deadline_is_never_cancelled(double? hours)
    {
        using var scope = new RpcCallScope(hours is { } h ? TimeSpan.FromHours(h) : null, CancellationToken.None);

        Assert.False(scope.IsCancellationRequested);
    }

    [Fact]
    public async Task Stopping_the_server_cancels_a_unary_request_and_reports_Unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new BlockingAgent();
        using var stopping = new CancellationTokenSource();
        var handler = new SlimA2AHandler(
            new A2AServer(agent, new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance),
            serverStopping: stopping.Token);

        var call = handler.SendMessage(ProtoRequest("block"), null!);
        await agent.Started.Task.WaitAsync(Prompt, ct);
        stopping.Cancel();

        var ex = await Assert.ThrowsAsync<RpcException.Rpc>(() => call.WaitAsync(Prompt, ct));
        Assert.Equal(RpcCode.Unavailable, ex.code);
        await agent.Cancelled.Task.WaitAsync(Prompt, ct);
    }

    [Fact]
    public async Task Stopping_the_server_ends_a_subscription_and_reports_Unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var a2a = new A2AServer(new BlockingAgent(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        using var stopping = new CancellationTokenSource();
        var handler = new SlimA2AHandler(a2a, serverStopping: stopping.Token);
        var task = ProtoConverter.FromProto(await handler.SendMessage(ProtoRequest("input"), null!)).Task!;

        var events = new List<Lf.A2a.V1.StreamResponse>();
        var subscription = Task.Run(async () =>
        {
            await foreach (var e in handler.SubscribeToTask(new Lf.A2a.V1.SubscribeToTaskRequest { Id = task.Id }, null!))
                events.Add(e);
        }, ct);
        while (events.Count == 0 && !subscription.IsCompleted)
            await Task.Delay(20, ct);
        stopping.Cancel();

        var ex = await Assert.ThrowsAsync<RpcException.Rpc>(() => subscription.WaitAsync(Prompt, ct));
        Assert.Equal(RpcCode.Unavailable, ex.code);
    }

    [Fact]
    public async Task Client_stream_stops_waiting_on_cancellation_and_releases_the_stream_once_the_read_finishes()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new UncancellableStream();
        using var cts = new CancellationTokenSource();
        var reading = A2ARpcErrorMapping.WithA2AErrors(source, cts.Token).ToListAsync();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(Prompt, ct));
        // Disposing an async iterator while its MoveNextAsync is still running is not allowed: wait for the read.
        Assert.False(source.Disposed.Task.IsCompleted);
        source.Read.SetResult(false);
        await source.Disposed.Task.WaitAsync(Prompt, ct);
    }

    [Fact]
    public void A_passed_deadline_is_a_TimeoutException_for_the_client()
    {
        var ex = A2ARpcErrorMapping.ToClientException(new RpcException.Rpc(RpcCode.DeadlineExceeded, "deadline exceeded", null));

        var timeout = Assert.IsType<TimeoutException>(ex);
        Assert.Equal("deadline exceeded", timeout.Message);
        Assert.IsType<A2AException>(timeout.InnerException);
    }

    [Fact]
    public void Other_RPC_errors_stay_A2A_errors_for_the_client()
    {
        var ex = A2ARpcErrorMapping.ToClientException(new RpcException.Rpc(RpcCode.NotFound, "gone", null));

        Assert.Equal(A2AErrorCode.TaskNotFound, Assert.IsType<A2AException>(ex).ErrorCode);
    }
}

internal static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source.ConfigureAwait(false))
            items.Add(item);
        return items;
    }
}
