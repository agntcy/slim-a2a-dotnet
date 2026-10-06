using System.Diagnostics;
using A2A;
using Agntcy.Slim;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>The lifecycle of <see cref="SlimA2AConnection"/> and the servers and clients created from it.</summary>
public sealed class ConnectionTests(SlimNodeFixture node)
{
    [Fact]
    public async Task ConnectAsync_to_an_unreachable_node_fails_within_the_connect_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(3);
        var watch = Stopwatch.StartNew();

        // Nothing listens on port 1: before the connect timeout existed, this retried forever.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => SlimA2AConnection.ConnectAsync(
            new SlimA2AConnectionOptions { Endpoint = "http://127.0.0.1:1", ConnectTimeout = timeout }, ct));

        Assert.True(ex is TimeoutException or SlimException, $"unexpected {ex.GetType()}: {ex.Message}");
        Assert.True(watch.Elapsed < timeout + TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task A_second_connection_to_the_same_endpoint_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = node.Connection;

        await Assert.ThrowsAsync<SlimException>(() => SlimA2AConnection.ConnectAsync(
            new SlimA2AConnectionOptions { Endpoint = node.Endpoint, ConnectTimeout = TimeSpan.FromSeconds(5) }, ct));
    }

    [Fact]
    public async Task A_client_can_address_the_agent_by_its_agent_card_url()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = node.CreateClient($"card_url_{node.RunId}", remote: $"slim://{node.ServerIdentity}");

        var response = await client.SendMessageAsync(TestRequests.Text(TestAgent.MessageOnly), ct);

        Assert.Equal(node.ServerIdentity, client.Remote);
        Assert.Equal(TestAgent.MessageOnlyReply, Assert.Single(response.Message!.Parts!).Text);
    }

    [Fact]
    public async Task A_disposed_client_rejects_calls()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = node.CreateClient($"disposed_{node.RunId}");
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetTaskAsync(new GetTaskRequest { Id = "t" }, ct));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendStreamingMessageAsync(TestRequests.Text("x"), ct).ToListAsync());
    }

    [Fact]
    public async Task One_connection_serves_several_agents_and_the_card_resolver_sees_the_tenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var identity = $"agntcy/slima2a_it/cards_{node.RunId}";
        var a2a = new A2AServer(new TestAgent(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        await using var server = await node.Connection.StartServerAsync(new SlimA2AServerOptions
        {
            Identity = identity,
            SharedSecret = node.SharedSecret,
            ResolveExtendedAgentCard = (request, _) => Task.FromResult(new AgentCard
            {
                Name = $"card for {request.Tenant}",
                Description = "per-tenant card",
                Version = "1",
                SupportedInterfaces = [new AgentInterface { Url = $"slim://{identity}", ProtocolBinding = "SLIMRPC", ProtocolVersion = "1.0" }],
                DefaultInputModes = ["text/plain"],
                DefaultOutputModes = ["text/plain"],
                Capabilities = new AgentCapabilities(),
                Skills = [],
            }),
        }, a2a, ct);
        await using var client = node.CreateClient($"cards_client_{node.RunId}", remote: identity);

        var card = await client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest { Tenant = "acme" }, ct);

        Assert.Equal(identity, server.Identity);
        Assert.Equal("card for acme", card.Name);
    }

    [Fact]
    public async Task Disposing_a_connection_stops_its_servers_and_disposes_its_clients()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = node.Connection;
        // A second connection to the node needs a different endpoint string; a trailing slash keeps the same address.
        var alias = node.Endpoint.EndsWith('/') ? node.Endpoint.TrimEnd('/') : node.Endpoint + "/";

        var connection = await SlimA2AConnection.ConnectAsync(new SlimA2AConnectionOptions { Endpoint = alias }, ct);
        var identity = $"agntcy/slima2a_it/owned_{node.RunId}";
        var a2a = new A2AServer(new TestAgent(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        var server = await connection.StartServerAsync(new SlimA2AServerOptions { Identity = identity, SharedSecret = node.SharedSecret }, a2a, ct);
        var client = connection.CreateClient(new SlimA2AClientOptions
        {
            Identity = $"agntcy/slima2a_it/owned_client_{node.RunId}",
            SharedSecret = node.SharedSecret,
            Remote = identity,
            DefaultTimeout = TimeSpan.FromSeconds(15),
        });
        var reply = await client.SendMessageAsync(TestRequests.Text(TestAgent.MessageOnly), ct);
        Assert.Equal(TestAgent.MessageOnlyReply, Assert.Single(reply.Message!.Parts!).Text);

        await connection.DisposeAsync();

        await server.Completion.WaitAsync(TimeSpan.FromSeconds(15), ct);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetTaskAsync(new GetTaskRequest { Id = "t" }, ct));
        Assert.Throws<ObjectDisposedException>(() => connection.CreateClient(new SlimA2AClientOptions
        {
            Identity = "agntcy/slima2a_it/late",
            SharedSecret = node.SharedSecret,
            Remote = identity,
        }));
    }
}
