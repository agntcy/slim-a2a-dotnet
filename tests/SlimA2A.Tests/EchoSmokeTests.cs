using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using SlimA2A;
using Xunit;

namespace SlimA2A.Tests;

/// <summary>
/// In-process smoke test for the .NET↔.NET echo path: drives a real <see cref="A2AServer"/> echo
/// pipeline through the generated <c>SlimA2AHandler</c> adapter and <see cref="ProtoConverter"/>,
/// asserting the round-trip without a SLIM node (the SLIMRPC wire transport is covered by csit).
/// </summary>
public class EchoSmokeTests
{
    private sealed class EchoHandler : IAgentHandler
    {
        public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
        {
            var responder = new MessageResponder(eventQueue, context.ContextId);
            await responder.ReplyAsync($"Echo: {context.UserText}", cancellationToken).ConfigureAwait(false);
            await responder.ReplyAsync("[stream] second event (unary-stream demo).", cancellationToken).ConfigureAwait(false);
        }
    }

    private static SlimA2AHandler NewHandler()
    {
        var a2a = new A2AServer(
            new EchoHandler(),
            new InMemoryTaskStore(),
            new ChannelEventNotifier(),
            NullLogger<A2AServer>.Instance);
        return new SlimA2AHandler(a2a);
    }

    private static Lf.A2a.V1.SendMessageRequest BuildProtoRequest(string text) =>
        ProtoConverter.ToProto(new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [Part.FromText(text)],
            },
        });

    [Fact]
    public async Task SendMessage_unary_echoes_text()
    {
        var handler = NewHandler();

        var proto = await handler.SendMessage(BuildProtoRequest("Hello there!"), null!);
        var resp = ProtoConverter.FromProto(proto);

        var text = resp.Message?.Parts?.FirstOrDefault()?.Text
            ?? resp.Task?.Status?.Message?.Parts?.FirstOrDefault()?.Text;
        Assert.Equal("Echo: Hello there!", text);
    }

    [Fact]
    public async Task SendStreamingMessage_streams_echo_and_second_event()
    {
        var handler = NewHandler();

        var texts = new List<string?>();
        await foreach (var ev in handler.SendStreamingMessage(BuildProtoRequest("Hello there!"), null!))
        {
            var sr = ProtoConverter.FromProtoStream(ev);
            texts.Add(sr.Message?.Parts?.FirstOrDefault()?.Text);
        }

        Assert.Equal(2, texts.Count);
        Assert.Equal("Echo: Hello there!", texts[0]);
        Assert.Equal("[stream] second event (unary-stream demo).", texts[1]);
    }
}
