using A2A;
using uniffi.slim_bindings;

namespace SlimA2A;

/// <summary>
/// Maps <see cref="A2AException"/> to SLIM RPC error codes (gRPC-style).
/// Invalid params → InvalidArgument; task not found → NotFound; unsupported / push / extended card → FailedPrecondition; default → Internal.
/// </summary>
public static class A2ARpcErrorMapping
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
}
