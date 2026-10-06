using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using SlimA2A;
using uniffi.slim_rpc;
using Xunit;

namespace SlimA2A.Tests;

/// <summary>
/// Streaming RPCs are lazy: errors surface while the stream is enumerated, not when it is opened.
/// These tests pin that A2A errors keep their code across both halves of a streaming call —
/// <see cref="A2AException"/> → <see cref="RpcException.Rpc"/> on the server, and back on the client.
/// </summary>
public sealed class StreamingErrorMappingTests
{
    /// <summary>Emits one event, then fails with a specific A2A error mid-stream.</summary>
    private sealed class FailingAgent : IAgentHandler
    {
        public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
        {
            await new MessageResponder(eventQueue, context.ContextId).ReplyAsync("first event", cancellationToken).ConfigureAwait(false);
            throw new A2AException("bad input from agent", A2AErrorCode.InvalidParams);
        }
    }

    private static SlimA2AHandler NewHandler() =>
        new(new A2AServer(new FailingAgent(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance));

    private static Lf.A2a.V1.SendMessageRequest BuildProtoRequest() =>
        ProtoConverter.ToProto(new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [Part.FromText("hi")],
            },
        });

    /// <summary>
    /// Mirrors the generated SLIMRPC stream handler: an <see cref="RpcException"/> crosses the wire as-is,
    /// anything else is reported as <see cref="RpcCode.Internal"/>.
    /// </summary>
    private static async IAsyncEnumerable<T> AcrossTheWire<T>(IAsyncEnumerable<T> serverStream)
    {
        await using var e = serverStream.GetAsyncEnumerator();
        while (await MoveNextAcrossTheWireAsync(e))
            yield return e.Current;
    }

    private static async ValueTask<bool> MoveNextAcrossTheWireAsync<T>(IAsyncEnumerator<T> e)
    {
        try
        {
            return await e.MoveNextAsync();
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RpcException.Rpc(RpcCode.Internal, ex.Message, null);
        }
    }

    private static async IAsyncEnumerable<int> YieldThenThrow(int count, Exception ex)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return i;
        }
        throw ex;
    }

    [Fact]
    public async Task Handler_SendStreamingMessage_agent_error_mid_stream_keeps_its_code()
    {
        var received = 0;

        var ex = await Assert.ThrowsAsync<RpcException.Rpc>(async () =>
        {
            await foreach (var _ in NewHandler().SendStreamingMessage(BuildProtoRequest(), null!))
                received++;
        });

        Assert.Equal(1, received);
        Assert.Equal(RpcCode.InvalidArgument, ex.code);
        Assert.Equal("bad input from agent", ex.message);
    }

    [Fact]
    public async Task Handler_SubscribeToTask_unknown_task_maps_to_NotFound()
    {
        var request = new Lf.A2a.V1.SubscribeToTaskRequest { Id = "no-such-task" };

        var ex = await Assert.ThrowsAsync<RpcException.Rpc>(async () =>
        {
            await foreach (var _ in NewHandler().SubscribeToTask(request, null!)) { }
        });

        Assert.Equal(RpcCode.NotFound, ex.code);
    }

    [Fact]
    public async Task WithRpcErrors_maps_error_thrown_when_opening_the_stream()
    {
        var stream = A2ARpcErrorMapping.WithRpcErrors<int>(
            () => throw new A2AException("gone", A2AErrorCode.TaskNotFound));

        var ex = await Assert.ThrowsAsync<RpcException.Rpc>(async () =>
        {
            await foreach (var _ in stream) { }
        });

        Assert.Equal(RpcCode.NotFound, ex.code);
    }

    [Fact]
    public async Task WithRpcErrors_leaves_non_A2A_exceptions_untouched()
    {
        // Left for the generated handler, which reports them as Internal.
        var stream = A2ARpcErrorMapping.WithRpcErrors(() => YieldThenThrow(1, new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in stream) { }
        });
    }

    [Fact]
    public async Task WithA2AErrors_maps_rpc_error_mid_stream()
    {
        var received = new List<int>();
        var stream = A2ARpcErrorMapping.WithA2AErrors(
            YieldThenThrow(2, new RpcException.Rpc(RpcCode.NotFound, "Task 'x' not found.", null)));

        var ex = await Assert.ThrowsAsync<A2AException>(async () =>
        {
            await foreach (var i in stream)
                received.Add(i);
        });

        Assert.Equal([0, 1], received);
        Assert.Equal(A2AErrorCode.TaskNotFound, ex.ErrorCode);
        Assert.Equal("Task 'x' not found.", ex.Message);
    }

    [Fact]
    public async Task WithA2AErrors_maps_rpc_error_before_first_item()
    {
        var stream = A2ARpcErrorMapping.WithA2AErrors(
            YieldThenThrow(0, new RpcException.Rpc(RpcCode.InvalidArgument, "bad", null)));

        var ex = await Assert.ThrowsAsync<A2AException>(async () =>
        {
            await foreach (var _ in stream) { }
        });

        Assert.Equal(A2AErrorCode.InvalidParams, ex.ErrorCode);
    }

    [Fact]
    public async Task Server_to_client_round_trip_preserves_A2A_error_code()
    {
        // The in-process equivalent of SendStreamingMessage over SLIMRPC: handler → generated wire handling → client wrapper.
        var received = 0;
        var stream = A2ARpcErrorMapping.WithA2AErrors(
            AcrossTheWire(NewHandler().SendStreamingMessage(BuildProtoRequest(), null!)));

        var ex = await Assert.ThrowsAsync<A2AException>(async () =>
        {
            await foreach (var _ in stream)
                received++;
        });

        Assert.Equal(1, received);
        Assert.Equal(A2AErrorCode.InvalidParams, ex.ErrorCode);
    }
}
