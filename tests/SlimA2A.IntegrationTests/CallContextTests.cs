using System.Text.Json;
using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>Client metadata reaches the server, where <see cref="SlimA2ACallContext.Current"/> exposes it to agent code.</summary>
public sealed class CallContextTests(SlimNodeFixture node)
{
    private static readonly Dictionary<string, string> Sent = new()
    {
        [SlimA2AMetadata.Extensions] = "https://example.com/ext/a, https://example.com/ext/b",
        ["x-trace-id"] = "trace-123",
    };

    private SlimA2AClient ClientWithMetadata(string name) =>
        node.Connection.CreateClient(new SlimA2AClientOptions
        {
            Identity = $"agntcy/slima2a_it/{name}_{node.RunId}",
            SharedSecret = node.SharedSecret,
            Remote = node.ServerIdentity,
            DefaultTimeout = TimeSpan.FromSeconds(15),
            Metadata = new Dictionary<string, string>(Sent),
        });

    private static void AssertSeen(string? observed)
    {
        Assert.NotNull(observed);
        Assert.NotEqual("null", observed);
        using var json = JsonDocument.Parse(observed);
        var metadata = json.RootElement.GetProperty("metadata").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        Assert.Equal(Sent.OrderBy(kv => kv.Key), metadata.OrderBy(kv => kv.Key)!);
        Assert.Equal(
            ["https://example.com/ext/a", "https://example.com/ext/b"],
            json.RootElement.GetProperty("extensions").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Agent_sees_the_client_metadata_on_a_unary_call()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = ClientWithMetadata("ctx_unary");

        var reply = await client.SendMessageAsync(TestRequests.Text(TestAgent.CallContext), ct);

        Assert.All(reply.Message!.Parts!, part => AssertSeen(part.Text));
    }

    [Fact]
    public async Task Agent_sees_the_client_metadata_on_a_streaming_call()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = ClientWithMetadata("ctx_stream");

        var events = await client.SendStreamingMessageAsync(TestRequests.Text(TestAgent.CallContext), ct).ToListAsync();

        var parts = events.Where(e => e.Message is not null).SelectMany(e => e.Message!.Parts!).ToList();
        Assert.NotEmpty(parts);
        Assert.All(parts, part => AssertSeen(part.Text));
    }

    [Fact]
    public async Task Request_handler_sees_the_client_metadata_on_every_RPC()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = ClientWithMetadata("ctx_handler");

        var task = (await client.SendMessageAsync(TestRequests.Text(TestAgent.InputRequired), ct)).Task!;
        await client.GetTaskAsync(new GetTaskRequest { Id = task.Id }, ct);
        await client.ListTasksAsync(new ListTasksRequest { ContextId = task.ContextId }, ct);
        await client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest { Tenant = $"ctx-{node.RunId}" }, ct);
        await client.CancelTaskAsync(new CancelTaskRequest { Id = task.Id }, ct);

        string[] rpcs =
        [
            nameof(IA2ARequestHandler.SendMessageAsync),
            nameof(IA2ARequestHandler.GetTaskAsync),
            nameof(IA2ARequestHandler.ListTasksAsync),
            nameof(IA2ARequestHandler.GetExtendedAgentCardAsync),
            nameof(IA2ARequestHandler.CancelTaskAsync),
        ];
        Assert.All(rpcs, rpc =>
        {
            var seen = node.Handler.LastCallContext.GetValueOrDefault(rpc);
            Assert.NotNull(seen);
            Assert.Equal(Sent.OrderBy(kv => kv.Key), seen.Metadata.OrderBy(kv => kv.Key));
        });
    }

    [Fact]
    public async Task A_client_without_metadata_sends_none()
    {
        var ct = TestContext.Current.CancellationToken;

        var reply = await node.Client.SendMessageAsync(TestRequests.Text(TestAgent.CallContext), ct);

        using var json = JsonDocument.Parse(reply.Message!.Parts![0].Text!);
        Assert.Empty(json.RootElement.GetProperty("metadata").EnumerateObject());
        Assert.Empty(json.RootElement.GetProperty("extensions").EnumerateArray());
    }

    [Theory]
    [InlineData("method")]
    [InlineData("service")]
    [InlineData("rpc-id")]
    public void A_client_cannot_send_a_key_SLIMRPC_reserves_for_routing(string key)
    {
        var ex = Assert.Throws<ArgumentException>(() => node.Connection.CreateClient(new SlimA2AClientOptions
        {
            Identity = $"agntcy/slima2a_it/reserved_{node.RunId}",
            SharedSecret = node.SharedSecret,
            Remote = node.ServerIdentity,
            Metadata = new Dictionary<string, string> { [key] = "x" },
        }));

        Assert.Contains($"'{key}'", ex.Message);
    }
}
