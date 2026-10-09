using System.Diagnostics;
using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Deadlines and cancellation over the wire: the client stops waiting when its caller cancels, a passed deadline is a
/// <see cref="TimeoutException"/>, and the server cancels the agent's work when the deadline passes or the server stops.
/// </summary>
public sealed class CancellationTests(SlimNodeFixture node)
{
    private static readonly TimeSpan Prompt = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task A_passed_deadline_raises_TimeoutException_and_cancels_the_agent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = node.CreateClient($"deadline_{node.RunId}", TimeSpan.FromSeconds(1));
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => client.SendMessageAsync(TestRequests.Text(TestAgent.Sleep), ct));

        Assert.True(watch.Elapsed < Prompt, $"took {watch.Elapsed}");
        Assert.True(await node.Agent.LastSleep.Task.WaitAsync(Prompt, ct), "the agent should be cancelled at the deadline");
    }

    [Fact]
    public async Task Cancelling_a_call_stops_waiting_for_it()
    {
        var ct = TestContext.Current.CancellationToken;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => node.Client.SendMessageAsync(TestRequests.Text(TestAgent.Sleep), cts.Token));

        Assert.True(watch.Elapsed < Prompt, $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task Cancelling_a_subscription_stops_waiting_for_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var task = await node.Client.CreateTaskAsync(TestAgent.InputRequired, cancellationToken: ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));
        var watch = Stopwatch.StartNew();

        // Nothing happens to the task after the first event, so only cancellation can end this.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => node.Client.SubscribeToTaskAsync(new SubscribeToTaskRequest { Id = task.Id }, cts.Token).ToListAsync());

        Assert.True(watch.Elapsed < Prompt, $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task Stopping_a_server_cancels_requests_in_flight()
    {
        var ct = TestContext.Current.CancellationToken;
        var identity = $"agntcy/slima2a_it/stopping_{node.RunId}";
        var agent = new TestAgent();
        var a2a = new A2AServer(agent, new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        await using var server = await node.Connection.StartServerAsync(
            new SlimA2AServerOptions { Identity = identity, SharedSecret = node.SharedSecret }, a2a, ct);
        await using var client = node.CreateClient($"stopping_client_{node.RunId}", TimeSpan.FromSeconds(30), identity);

        var before = agent.LastSleep;
        var call = client.SendMessageAsync(TestRequests.Text(TestAgent.Sleep), ct);
        await WaitUntilAsync(() => agent.LastSleep != before || call.IsCompleted, ct);
        Assert.NotSame(before, agent.LastSleep);
        var watch = Stopwatch.StartNew();
        await server.StopAsync();

        Assert.True(watch.Elapsed < Prompt, $"StopAsync took {watch.Elapsed}");
        Assert.True(await agent.LastSleep.Task.WaitAsync(Prompt, ct), "the agent should be cancelled when the server stops");
        await Assert.ThrowsAnyAsync<Exception>(() => call.WaitAsync(Prompt, ct));
        Assert.True(server.Completion.IsCompleted);
    }

    /// <summary>Polls until <paramref name="condition"/> holds, for up to 10 seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50, ct);
    }
}
