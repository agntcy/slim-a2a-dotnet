using System.Net.Sockets;
using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

// Tests run one at a time (xunit.runner.json) against one shared server, client and node.
[assembly: AssemblyFixture(typeof(SlimA2A.IntegrationTests.SlimNodeFixture))]

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Hosts an A2A server (<see cref="TestAgent"/> behind <see cref="TestRequestHandler"/>) and a <see cref="SlimA2AClient"/>
/// on one <see cref="SlimA2AConnection"/> to a real SLIM node, for the whole test run. Configured through the same
/// variables as the EchoAgent example: <c>SLIM_SERVER</c> (default <c>http://127.0.0.1:46357</c>) and <c>SLIM_SHARED_SECRET</c>.
/// </summary>
public sealed class SlimNodeFixture : IAsyncLifetime
{
    /// <summary>Set to <c>required</c> to fail, rather than skip, when no node is reachable.</summary>
    public const string RequiredVariable = "SLIM_A2A_INTEGRATION";

    private const string DefaultEndpoint = "http://127.0.0.1:46357";
    private const string DefaultSecret = "integration-test-shared-secret-32+chars";
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

    private SlimA2AConnection? _connection;
    private SlimA2AClient? _client;
    private TestRequestHandler? _handler;
    private readonly TestAgent _agent = new();

    /// <summary>Why the node is unusable, or null when tests can run.</summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>The node's endpoint.</summary>
    public string Endpoint { get; } = Environment.GetEnvironmentVariable("SLIM_SERVER") ?? DefaultEndpoint;

    /// <summary>The shared secret of every identity in the test run.</summary>
    public string SharedSecret { get; } = Environment.GetEnvironmentVariable("SLIM_SHARED_SECRET") ?? DefaultSecret;

    /// <summary>Unique per run, so a lingering server from an earlier run can't answer for this one.</summary>
    public string RunId { get; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>The test server's SLIM identity.</summary>
    public string ServerIdentity => $"agntcy/slima2a_it/server_{RunId}";

    /// <summary>Client bound to the test server; skips the calling test when no node is reachable.</summary>
    public SlimA2AClient Client
    {
        get
        {
            SkipIfUnavailable();
            return _client!;
        }
    }

    /// <summary>The connection the test server and client share; skips the calling test when no node is reachable.</summary>
    public SlimA2AConnection Connection
    {
        get
        {
            SkipIfUnavailable();
            return _connection!;
        }
    }

    /// <summary>The test server's agent, for asserting on how a request ended on the server.</summary>
    internal TestAgent Agent => _agent;

    /// <summary>The server-side request handler, for asserting on what reached the server.</summary>
    internal TestRequestHandler Handler => _handler!;

    /// <summary>Card served by <c>GetExtendedAgentCard</c>.</summary>
    public static AgentCard Card { get; } = new()
    {
        Name = "SlimA2A integration agent",
        Description = "Drives every A2A RPC over SLIMRPC for the integration tests.",
        Version = "1.2.3",
        SupportedInterfaces =
        [
            new AgentInterface { Url = "slim://agntcy/slima2a_it/server", ProtocolBinding = "SLIMRPC", ProtocolVersion = "1.0", Tenant = "slima2a_it" },
        ],
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain", "application/json"],
        Capabilities = new AgentCapabilities { Streaming = true, PushNotifications = true },
        Skills =
        [
            new AgentSkill { Id = "echo", Name = "Echo", Description = "Echoes the request parts.", Tags = ["echo", "test"] },
        ],
    };

    public async ValueTask InitializeAsync()
    {
        var required = string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "required", StringComparison.OrdinalIgnoreCase);

        // Fail fast with a clear reason instead of waiting out the connect timeout.
        if (!await IsReachableAsync(Endpoint).ConfigureAwait(false))
        {
            UnavailableReason = $"No SLIM node reachable at {Endpoint} (set SLIM_SERVER, or start one as described in README.md).";
            if (required)
                throw new InvalidOperationException($"{UnavailableReason} {RequiredVariable}=required.");
            return;
        }

        _connection = await SlimA2AConnection.ConnectAsync(new SlimA2AConnectionOptions { Endpoint = Endpoint }).ConfigureAwait(false);

        var a2a = new A2AServer(_agent, new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        _handler = new TestRequestHandler(a2a, Card);
        await _connection.StartServerAsync(
            new SlimA2AServerOptions { Identity = ServerIdentity, SharedSecret = SharedSecret }, _handler).ConfigureAwait(false);

        // A default timeout bounds every RPC, so a broken call fails the test instead of hanging the run.
        _client = CreateClient($"client_{RunId}", TimeSpan.FromSeconds(15));

        await WaitUntilServingAsync().ConfigureAwait(false);
    }

    /// <summary>Disconnects, which also stops the test server and disposes every client created from the connection.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>A client of the test server with its own identity; dispose it after use.</summary>
    public SlimA2AClient CreateClient(string name, TimeSpan? timeout = null, string? remote = null) =>
        Connection.CreateClient(new SlimA2AClientOptions
        {
            Identity = $"agntcy/slima2a_it/{name}",
            SharedSecret = SharedSecret,
            Remote = remote ?? ServerIdentity,
            DefaultTimeout = timeout ?? TimeSpan.FromSeconds(15),
        });

    private void SkipIfUnavailable()
    {
        if (UnavailableReason is not null)
            Assert.Skip(UnavailableReason);
    }

    private static async Task<bool> IsReachableAsync(string endpoint)
    {
        var uri = new Uri(endpoint);
        using var tcp = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await tcp.ConnectAsync(uri.Host, uri.Port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The server is ready once an RPC reaches the agent: a lookup of an unknown task answers TaskNotFound.</summary>
    private async Task WaitUntilServingAsync()
    {
        await using var probe = CreateClient($"probe_{RunId}", TimeSpan.FromSeconds(2));
        var deadline = DateTime.UtcNow + ReadyTimeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await probe.GetTaskAsync(new GetTaskRequest { Id = "readiness-probe" }).ConfigureAwait(false);
                return;
            }
            catch (A2AException ex) when (ex.ErrorCode == A2AErrorCode.TaskNotFound)
            {
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(250).ConfigureAwait(false);
            }
        }
        throw new TimeoutException($"SLIMRPC server did not become ready within {ReadyTimeout}.", last);
    }
}
