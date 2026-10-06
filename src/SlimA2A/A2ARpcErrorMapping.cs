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
    /// Server side: opens <paramref name="open"/> and translates any <see cref="A2AException"/> it raises — when the
    /// stream is opened or while it is being enumerated — into <see cref="RpcException.Rpc"/>, so the error reaches the
    /// client with its mapped code instead of the generated handler's generic <see cref="RpcCode.Internal"/>.
    /// </summary>
    internal static async IAsyncEnumerable<T> WithRpcErrors<T>(Func<IAsyncEnumerable<T>> open)
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
            while (await MoveNextWithRpcErrorsAsync(e).ConfigureAwait(false))
                yield return e.Current;
        }
    }

    /// <summary>
    /// Client side: translates any <see cref="RpcException.Rpc"/> raised while enumerating <paramref name="source"/> into
    /// <see cref="A2AException"/>, matching the unary client methods. Streaming RPCs are lazy, so errors only surface here.
    /// </summary>
    internal static async IAsyncEnumerable<T> WithA2AErrors<T>(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var e = source.GetAsyncEnumerator(cancellationToken);
        await using (e.ConfigureAwait(false))
        {
            while (await MoveNextWithA2AErrorsAsync(e).ConfigureAwait(false))
                yield return e.Current;
        }
    }

    // C# forbids `yield return` inside a try block that has a catch clause, so the guarded MoveNextAsync lives here.
    private static async ValueTask<bool> MoveNextWithRpcErrorsAsync<T>(IAsyncEnumerator<T> e)
    {
        try
        {
            return await e.MoveNextAsync().ConfigureAwait(false);
        }
        catch (A2AException ex)
        {
            throw ToRpc(ex);
        }
    }

    private static async ValueTask<bool> MoveNextWithA2AErrorsAsync<T>(IAsyncEnumerator<T> e)
    {
        try
        {
            return await e.MoveNextAsync().ConfigureAwait(false);
        }
        catch (RpcException.Rpc ex)
        {
            throw FromRpc(ex);
        }
    }
}
