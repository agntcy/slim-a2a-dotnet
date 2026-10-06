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
    var a2a = new A2AServer(new EchoAgent.EchoHandler(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);

    await using var slim = await SlimA2AConnection.ConnectAsync(new SlimA2AConnectionOptions { Endpoint = endpoint }).ConfigureAwait(false);
    await using var server = await slim.StartServerAsync(
        new SlimA2AServerOptions
        {
            Identity = serverName,
            SharedSecret = secret,
            ResolveExtendedAgentCard = (_, _) => Task.FromResult(card),
        },
        a2a).ConfigureAwait(false);

    Console.WriteLine($"[EchoAgent] server: SLIM endpoint {endpoint}, identity '{serverName}'. Ctrl+C to stop.");
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        _ = server.StopAsync();
    };

    await server.Completion.ConfigureAwait(false);
    Console.WriteLine("Server stopped.");
    return;
}

if (mode == "client")
{
    await using var slim = await SlimA2AConnection.ConnectAsync(new SlimA2AConnectionOptions { Endpoint = endpoint }).ConfigureAwait(false);
    await using var client = slim.CreateClient(new SlimA2AClientOptions
    {
        Identity = clientName,
        SharedSecret = secret,
        Remote = serverName,
    });

    Console.WriteLine($"[EchoAgent] client: connected to {endpoint}, calling remote '{serverName}'.");

    var outboundText = "Hello there!";
    var msg = new Message
    {
        Role = Role.User,
        MessageId = Guid.NewGuid().ToString("N"),
        Parts = [Part.FromText(outboundText)],
    };
    var sendReq = new SendMessageRequest { Message = msg };

    Console.WriteLine();
    Console.WriteLine("=== 1) A2A unary-unary (SendMessage → single SendMessageResponse) ===");
    Console.WriteLine($"[EchoAgent] client: SendMessage messageId={msg.MessageId} text={outboundText}");
    var unaryResp = await client.SendMessageAsync(sendReq).ConfigureAwait(false);
    var uText = unaryResp.Message?.Parts?.FirstOrDefault()?.Text
        ?? unaryResp.Task?.Status?.Message?.Parts?.FirstOrDefault()?.Text;
    Console.WriteLine($"[EchoAgent] client: unary-unary response text: {uText ?? "(no text)"}");

    Console.WriteLine();
    Console.WriteLine("=== 2) A2A unary-stream (SendStreamingMessage → stream of StreamResponse) ===");
    var stream = client.SendStreamingMessageAsync(sendReq);
    var i = 0;
    await foreach (var ev in stream.ConfigureAwait(false))
    {
        i++;
        switch (ev)
        {
            case { Message: { } m }:
            {
                var text = m.Parts?.FirstOrDefault()?.Text;
                Console.WriteLine($"[EchoAgent] client: unary-stream event #{i} message: {text ?? "(no text)"}");
                break;
            }
            case { Task: { } t }:
                Console.WriteLine($"[EchoAgent] client: unary-stream event #{i} task id={t.Id} state={t.Status?.State}");
                break;
            case { StatusUpdate: not null }:
                Console.WriteLine($"[EchoAgent] client: unary-stream event #{i} statusUpdate");
                break;
            default:
                Console.WriteLine($"[EchoAgent] client: unary-stream event #{i} (other payload)");
                break;
        }
    }

    Console.WriteLine($"[EchoAgent] client: unary-stream finished ({i} events).");

    return;
}

Console.Error.WriteLine("Usage: EchoAgent [server|client] [--server <url>] [--shared-secret <secret>]");
