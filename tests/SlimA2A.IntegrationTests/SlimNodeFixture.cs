using System.Net.Sockets;
using A2A;
using Agntcy.Slim;
using Agntcy.Slim.SlimRpc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

// Tests run one at a time (xunit.runner.json) against one shared server, client and node.
[assembly: AssemblyFixture(typeof(SlimA2A.IntegrationTests.SlimNodeFixture))]

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Hosts an A2A server (<see cref="TestAgent"/> behind <see cref="SlimA2AHandler"/>) and a <see cref="SlimA2AClient"/>
/// on a real SLIM node for the whole test run. Configured through the same variables as the EchoAgent example:
/// <c>SLIM_SERVER</c> (default <c>http://127.0.0.1:46357</c>) and <c>SLIM_SHARED_SECRET</c>.
/// </summary>
public sealed class SlimNodeFixture : IAsyncLifetime
{
    /// <summary>Set to <c>required</c> to fail, rather than skip, when no node is reachable.</summary>
    public const string RequiredVariable = "SLIM_A2A_INTEGRATION";

    private const string DefaultEndpoint = "http://127.0.0.1:46357";
    private const string DefaultSecret = "integration-test-shared-secret-32+chars";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(15);

    private SlimApp? _serverApp;
    private SlimApp? _clientApp;
    private SlimName? _serverName;
    private uniffi.slim_rpc.Server? _server;
    private uniffi.slim_rpc.Channel? _channel;
    private Task? _serveTask;
    private SlimA2AClient? _client;

    /// <summary>Why the node is unusable, or null when tests can run.</summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>Client bound to the test server; skips the calling test when no node is reachable.</summary>
    public SlimA2AClient Client
    {
        get
        {
            if (UnavailableReason is not null)
                Assert.Skip(UnavailableReason);
            return _client!;
        }
    }

    /// <summary>Card served by <c>GetExtendedAgentCard</c>.</summary>
    public static AgentCard Card { get; } = new()
    {
        Name = "SlimA2A integration agent",
        Description = "Drives every A2A RPC over SLIMRPC for the integration tests.",
        Version = "1.2.3",
        SupportedInterfaces =
        [
            new AgentInterface { Url = "slim://agntcy/slima2a_it/server", ProtocolBinding = "SLIMRPC", ProtocolVersion = "1.0" },
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
        var endpoint = Environment.GetEnvironmentVariable("SLIM_SERVER") ?? DefaultEndpoint;
        var secret = Environment.GetEnvironmentVariable("SLIM_SHARED_SECRET") ?? DefaultSecret;
        var required = string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "required", StringComparison.OrdinalIgnoreCase);

        // Probe first: connecting to an unreachable node retries forever instead of failing.
        if (!await IsReachableAsync(endpoint).ConfigureAwait(false))
        {
            UnavailableReason = $"No SLIM node reachable at {endpoint} (set SLIM_SERVER, or start one as described in README.md).";
            if (required)
                throw new InvalidOperationException($"{UnavailableReason} {RequiredVariable}=required.");
            return;
        }

        // Unique names per run, so a lingering server from an earlier run can't answer for this one.
        var runId = Guid.NewGuid().ToString("N")[..12];
        var serverIdentity = $"agntcy/slima2a_it/server_{runId}";
        var (serverApp, connId) = await SlimHelper.ConnectAndSubscribeAsync(serverIdentity, secret, endpoint)
            .WaitAsync(ConnectTimeout).ConfigureAwait(false);
        _serverApp = serverApp;

        // SlimHelper opens a new connection on every call and the global service allows only one per endpoint,
        // so the client app reuses the server's connection.
        using (var clientName = SlimName.Parse($"agntcy/slima2a_it/client_{runId}"))
        using (var service = Slim.GetGlobalService())
        {
            _clientApp = service.CreateApp(clientName, secret);
        }
        _clientApp.Subscribe(_clientApp.Name, connId);

        var a2a = new A2AServer(new TestAgent(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        _serverName = SlimName.Parse(serverIdentity);
        _server = SlimRpcServerFactory.CreateServer(_serverApp, _serverName, connId);
        SlimA2AServerRegistration.RegisterA2AService(_server, new SlimA2AHandler(new PushConfigHandler(a2a), _ => Task.FromResult(Card)));
        _serveTask = _server.ServeAsync();

        _channel = SlimRpcChannelFactory.CreateChannel(_clientApp, _serverName, connId);
        // A default timeout bounds every RPC, so a broken call fails the test instead of hanging the run.
        _client = new SlimA2AClient(_channel, TimeSpan.FromSeconds(15));

        await WaitUntilServingAsync(_channel).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.ShutdownAsync().ConfigureAwait(false);
            if (_serveTask is not null)
            {
                try
                {
                    await _serveTask.WaitAsync(ShutdownTimeout).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
                {
                    // Draining is best effort; the process is about to exit anyway.
                }
            }
            _server.Dispose();
        }
        _channel?.Dispose();
        _serverName?.Dispose();
        _clientApp?.Dispose();
        _serverApp?.Dispose();
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
    private static async Task WaitUntilServingAsync(uniffi.slim_rpc.Channel channel)
    {
        var probe = new SlimA2AClient(channel, TimeSpan.FromSeconds(2));
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
