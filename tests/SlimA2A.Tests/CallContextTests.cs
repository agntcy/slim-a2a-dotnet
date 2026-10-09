using System.Runtime.CompilerServices;
using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SlimA2A.Tests;

/// <summary><see cref="SlimA2ACallContext"/>: what it exposes, and that it is set exactly while a request is handled.</summary>
public sealed class CallContextTests
{
    /// <summary>Records <see cref="SlimA2ACallContext.Current"/> as seen by agent code.</summary>
    private sealed class ObservingAgent : IAgentHandler
    {
        public SlimA2ACallContext? Seen { get; private set; }

        public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
        {
            Seen = SlimA2ACallContext.Current;
            await new MessageResponder(eventQueue, context.ContextId).ReplyAsync("ok", cancellationToken).ConfigureAwait(false);
        }

        public Task CancelAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Lf.A2a.V1.SendMessageRequest ProtoRequest() =>
        ProtoConverter.ToProto(new SendMessageRequest
        {
            Message = new Message { Role = Role.User, MessageId = Guid.NewGuid().ToString("N"), Parts = [Part.FromText("hi")] },
        });

    [Theory]
    [InlineData("https://a.example/x", new[] { "https://a.example/x" })]
    [InlineData(" https://a.example/x ,https://b.example/y,, ", new[] { "https://a.example/x", "https://b.example/y" })]
    [InlineData("", new string[0])]
    public void Requested_extensions_are_parsed_from_the_metadata(string header, string[] expected)
    {
        var call = new SlimA2ACallContext(new Dictionary<string, string> { [SlimA2AMetadata.Extensions] = header });

        Assert.Equal(expected, call.RequestedExtensions);
    }

    [Fact]
    public void Without_metadata_there_are_no_extensions()
    {
        var call = new SlimA2ACallContext(null);

        Assert.Empty(call.Metadata);
        Assert.Empty(call.RequestedExtensions);
    }

    [Fact]
    public void SLIMRPC_routing_keys_are_left_out_of_the_metadata()
    {
        var call = new SlimA2ACallContext(new Dictionary<string, string>
        {
            ["x-trace-id"] = "t1",
            ["service"] = "lf.a2a.v1.A2AService",
            ["method"] = "SendMessage",
            ["rpc-id"] = "r1",
            ["slimrpc-dir"] = "req",
            ["slimrpc-timeout"] = "1000",
            ["slimrpc-code"] = "0",
        });

        Assert.Equal(["x-trace-id"], call.Metadata.Keys);
    }

    [Fact]
    public async Task Agent_code_sees_the_call_and_it_is_cleared_afterwards()
    {
        var agent = new ObservingAgent();
        var handler = new SlimA2AHandler(new A2AServer(agent, new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance));
        Assert.Null(SlimA2ACallContext.Current);

        await handler.SendMessage(ProtoRequest(), null!);

        Assert.NotNull(agent.Seen);
        Assert.Null(SlimA2ACallContext.Current);
    }

    [Fact]
    public async Task Stream_code_sees_the_call_on_every_read_not_only_the_first()
    {
        // Each read of an async iterator runs with its caller's execution context: a context set once, when the stream is
        // opened, is gone after the first yield. Record what the producer sees after each one.
        var seen = new List<SlimA2ACallContext?>();
        async IAsyncEnumerable<int> Producer([EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 0; i < 3; i++)
            {
                await Task.Yield();
                seen.Add(SlimA2ACallContext.Current);
                yield return i;
            }
        }
        using var call = new RpcCallScope(null, CancellationToken.None, new Dictionary<string, string> { ["k"] = "v" });

        // Read the way the generated handler does: from a context that never had the call set.
        await Task.Run(async () =>
        {
            await foreach (var _ in A2ARpcErrorMapping.WithRpcErrors(() => Producer(), call)) { }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(3, seen.Count);
        Assert.All(seen, c => Assert.Same(call.Context, c));
    }
}
