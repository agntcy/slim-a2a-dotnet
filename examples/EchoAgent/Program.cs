using Agntcy.Slim;
using Agntcy.Slim.SlimRpc;
using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using SlimA2A;

// Demo-only default (same pattern as slim .NET examples). Use SLIM_SHARED_SECRET in real setups.
const string DemoSharedSecret = "demo-shared-secret-min-32-chars!!";

static AgentCard BuildCard(string localSlimName) =>
    new()
    {
        Name = "Echo Agent (SLIM)",
        Description = "Echoes messages over A2A on SLIMRPC.",
        Version = "1.0.0",
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = $"slim://{localSlimName}",
                ProtocolBinding = "SLIMRPC",
                ProtocolVersion = "1.0",
            },
        ],
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities { Streaming = true, PushNotifications = false },
        Skills =
        [
            new AgentSkill
            {
                Id = "echo",
                Name = "Echo",
                Description = "Echoes back the user message.",
                Tags = ["echo", "test"],
            },
        ],
    };

static string? Opt(string[] a, string longName)
{
    for (var i = 0; i < a.Length - 1; i++)
    {
        if (a[i] == longName)
            return a[i + 1];
    }
    return null;
}

static string FirstPositional(string[] a)
{
    for (var i = 0; i < a.Length; i++)
        if (a[i] is { } x && !x.StartsWith("-", StringComparison.Ordinal))
            return x;
    return "server";
}

var mode = FirstPositional(args).ToLowerInvariant();
var endpoint = Opt(args, "--server")
    ?? Environment.GetEnvironmentVariable("SLIM_SERVER")
    ?? "http://localhost:46357";
var secret = Opt(args, "--shared-secret")
    ?? Environment.GetEnvironmentVariable("SLIM_SHARED_SECRET")
    ?? DemoSharedSecret;
var serverName = Environment.GetEnvironmentVariable("SLIM_A2A_SERVER_NAME") ?? "agntcy/a2a/echo";
var clientName = Environment.GetEnvironmentVariable("SLIM_A2A_CLIENT_NAME") ?? "agntcy/a2a/client";

if (mode == "server")
{
    var card = BuildCard(serverName);
    var store = new InMemoryTaskStore();
    var notifier = new ChannelEventNotifier();
    var a2a = new A2AServer(new EchoAgent.EchoHandler(), store, notifier, NullLogger<A2AServer>.Instance);
    var slimHandler = new SlimA2AHandler(a2a, _ => Task.FromResult(card));

    var (app, connId) = await SlimHelper.ConnectAndSubscribeAsync(serverName, secret, endpoint).ConfigureAwait(false);
    using (app)
    {
        using var localName = SlimName.Parse(serverName);
        using var slimServer = SlimRpcServerFactory.CreateServer(app, localName, connId);
        SlimA2AServerRegistration.RegisterA2AService(slimServer, slimHandler);

        Console.WriteLine($"[EchoAgent] server: SLIM endpoint {endpoint}, identity '{serverName}'. Ctrl+C to stop.");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _ = slimServer.ShutdownAsync();
        };

        try
        {
            await slimServer.ServeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopped.");
        }
    }

    return;
}

if (mode == "client")
{
    var (app, connId) = await SlimHelper.ConnectAndSubscribeAsync(clientName, secret, endpoint).ConfigureAwait(false);
    using (app)
    {
        using var remote = SlimName.Parse(serverName);
        using var channel = SlimRpcChannelFactory.CreateChannel(app, remote, connId);
        var client = new SlimA2AClient(channel);

        Console.WriteLine($"[EchoAgent] client: connected to {endpoint}, calling remote '{serverName}'.");

        var outboundText = "Hello there!";
        var msg = new Message
        {
            Role = Role.User,
            MessageId = Guid.NewGuid().ToString("N"),
            Parts = [Part.FromText(outboundText)],
        };

        Console.WriteLine($"[EchoAgent] client: SendMessage messageId={msg.MessageId} text={outboundText}");
        var response = await client.SendMessageAsync(new SendMessageRequest { Message = msg }).ConfigureAwait(false);
        var taskId = response.Task?.Id;
        var taskState = response.Task?.Status?.State.ToString();
        if (taskId is not null || taskState is not null)
            Console.WriteLine($"[EchoAgent] client: response task id={taskId} state={taskState}");
        var text = response.Message?.Parts?.FirstOrDefault()?.Text ?? response.Task?.Status.Message?.Parts?.FirstOrDefault()?.Text;
        Console.WriteLine($"[EchoAgent] client: response text: {text ?? "(no text in response)"} General Kenobi!");
    }

    return;
}

Console.Error.WriteLine("Usage: EchoAgent [server|client] [--server <url>] [--shared-secret <secret>]");
