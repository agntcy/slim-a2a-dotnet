using System.Runtime.CompilerServices;
using A2A;
using uniffi.slim_rpc;

namespace SlimA2A;

/// <summary>
/// Maps <see cref="A2AException"/> to SLIM RPC error codes (gRPC-style).
/// Invalid params → InvalidArgument; task not found → NotFound; unsupported / push / extended card → FailedPrecondition; default → Internal.
/// </summary>
internal static class A2ARpcErrorMapping
{
    public static RpcException.Rpc ToRpc(A2AException ex) =>
        new(ToRpcCode(ex.ErrorCode), ex.Message, null);

    public static A2AException FromRpc(RpcException.Rpc rpc)
    {
        var code = FromRpcCode(rpc.code);
        return new A2AException(rpc.message ?? "RPC error.", code);
    }

    /// <summary>
    /// What a client call throws for an RPC error: <see cref="TimeoutException"/> when the deadline passed (a transport
    /// condition, not an A2A error), otherwise the mapped <see cref="A2AException"/>.
    /// </summary>
    public static Exception ToClientException(RpcException.Rpc rpc) => rpc.code == RpcCode.DeadlineExceeded
        ? new TimeoutException(string.IsNullOrEmpty(rpc.message) ? "The RPC deadline passed." : rpc.message, FromRpc(rpc))
        : FromRpc(rpc);

    public static RpcCode ToRpcCode(A2AErrorCode code) => code switch
    {
        A2AErrorCode.TaskNotFound => RpcCode.NotFound,
        A2AErrorCode.InvalidParams or A2AErrorCode.ParseError => RpcCode.InvalidArgument,
        A2AErrorCode.MethodNotFound or A2AErrorCode.VersionNotSupported => RpcCode.Unimplemented,
        A2AErrorCode.InvalidRequest => RpcCode.InvalidArgument,
        A2AErrorCode.TaskNotCancelable
            or A2AErrorCode.PushNotificationNotSupported
            or A2AErrorCode.UnsupportedOperation
            or A2AErrorCode.ContentTypeNotSupported
            or A2AErrorCode.InvalidAgentResponse
            or A2AErrorCode.ExtendedAgentCardNotConfigured
            or A2AErrorCode.ExtensionSupportRequired => RpcCode.FailedPrecondition,
        _ => RpcCode.Internal,
    };

    public static A2AErrorCode FromRpcCode(RpcCode code) => code switch
    {
        RpcCode.NotFound => A2AErrorCode.TaskNotFound,
        RpcCode.InvalidArgument => A2AErrorCode.InvalidParams,
        RpcCode.Unimplemented => A2AErrorCode.MethodNotFound,
        RpcCode.FailedPrecondition => A2AErrorCode.UnsupportedOperation,
        _ => A2AErrorCode.InternalError,
    };

    /// <summary>
    /// Server side: opens <paramref name="open"/> and translates errors it raises — when the stream is opened or while it
    /// is being enumerated — into <see cref="RpcException.Rpc"/>, so they reach the client with their mapped code instead of
    /// the generated handler's generic <see cref="RpcCode.Internal"/>: an <see cref="A2AException"/>, or a cancellation
    /// caused by <paramref name="call"/> (deadline passed, server stopping).
    /// </summary>
    internal static async IAsyncEnumerable<T> WithRpcErrors<T>(Func<IAsyncEnumerable<T>> open, RpcCallScope? call = null)
    {
        IAsyncEnumerator<T> e;
        try
        {
            e = open().GetAsyncEnumerator();
        }
        catch (A2AException ex)
        {
            throw ToRpc(ex);
        }
        await using (e.ConfigureAwait(false))
        {
            while (await MoveNextWithRpcErrorsAsync(e, call).ConfigureAwait(false))
                yield return e.Current;
        }
    }

    /// <summary>
    /// Client side: translates any <see cref="RpcException.Rpc"/> raised while enumerating <paramref name="source"/> as the
    /// unary client methods do (<see cref="ToClientException"/>), and stops waiting as soon as
    /// <paramref name="cancellationToken"/> is cancelled. Streaming RPCs are lazy, so errors only surface here.
    /// </summary>
    /// <remarks>
    /// SLIM can't cancel a pending read, so on cancellation the read is abandoned and the stream released once it finishes
    /// (an async iterator can't be disposed while a <c>MoveNextAsync</c> is still running). The server stops at the RPC
    /// deadline at the latest.
    /// </remarks>
    internal static async IAsyncEnumerable<T> WithA2AErrors<T>(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var e = source.GetAsyncEnumerator(cancellationToken);
        Task<bool>? pending = null;
        try
        {
            while (true)
            {
                pending = MoveNextWithA2AErrorsAsync(e);
                var more = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
                pending = null;
                if (!more)
                    yield break;
                yield return e.Current;
            }
        }
        finally
        {
            if (pending is null)
                await e.DisposeAsync().ConfigureAwait(false);
            else
                _ = DisposeWhenDoneAsync(e, pending);
        }
    }

    // C# forbids `yield return` inside a try block that has a catch clause, so the guarded MoveNextAsync lives here.
    private static async ValueTask<bool> MoveNextWithRpcErrorsAsync<T>(IAsyncEnumerator<T> e, RpcCallScope? call)
    {
        try
        {
            return await e.MoveNextAsync().ConfigureAwait(false);
        }
        catch (A2AException ex)
        {
            throw ToRpc(ex);
        }
        catch (OperationCanceledException) when (call is { IsCancellationRequested: true })
        {
            throw call.ToRpcError();
        }
    }

    private static async Task<bool> MoveNextWithA2AErrorsAsync<T>(IAsyncEnumerator<T> e)
    {
        try
        {
            return await e.MoveNextAsync().ConfigureAwait(false);
        }
        catch (RpcException.Rpc ex)
        {
            throw ToClientException(ex);
        }
    }

    /// <summary>Releases a stream whose last read the caller stopped waiting for, once that read finishes.</summary>
    private static async Task DisposeWhenDoneAsync<T>(IAsyncEnumerator<T> e, Task pending)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Nobody is waiting for this read any more; its outcome doesn't matter.
        }
        try
        {
            await e.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Background cleanup: there is no caller left to report to.
        }
    }
}
