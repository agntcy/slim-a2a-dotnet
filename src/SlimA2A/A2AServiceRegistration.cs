using uniffi.slim_rpc;

namespace SlimA2A;

/// <summary>
/// Registers the generated A2A service handlers like <see cref="Lf.A2a.V1.A2AServiceServerRegistration.RegisterA2AServiceServer"/>
/// does, but captures each call's verified peer identity first. The generated handlers pass only a
/// <see cref="Agntcy.Slim.SlimRpc.SlimRpcContext"/>, which doesn't expose it.
/// </summary>
internal static class A2AServiceRegistration
{
    private const string ServiceName = "lf.a2a.v1.A2AService";

    private static readonly AsyncLocal<PeerIdentity?> CurrentPeer = new();

    /// <summary>The verified identity of the peer whose call is being handled, or null outside a call.</summary>
    internal static PeerIdentity? Peer
    {
        get => CurrentPeer.Value;
        set => CurrentPeer.Value = value;
    }

    internal static void Register(Server server, Lf.A2a.V1.IA2AServiceServer impl)
    {
        foreach (var (method, handler) in Handlers(impl))
        {
            switch (handler)
            {
                case UnaryUnaryHandler unary:
                    server.RegisterUnaryUnary(ServiceName, method, new UnaryWithPeer(unary));
                    break;
                case UnaryStreamHandler stream:
                    server.RegisterUnaryStream(ServiceName, method, new StreamWithPeer(stream));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported handler type {handler.GetType()} for {method}.");
            }
        }
    }

    /// <summary>The generated handler of every A2A method, as registered by the generated code.</summary>
    internal static IReadOnlyList<(string Method, object Handler)> Handlers(Lf.A2a.V1.IA2AServiceServer impl) =>
    [
        ("SendMessage", new Lf.A2a.V1.A2AService_SendMessage_Handler(impl)),
        ("SendStreamingMessage", new Lf.A2a.V1.A2AService_SendStreamingMessage_Handler(impl)),
        ("GetTask", new Lf.A2a.V1.A2AService_GetTask_Handler(impl)),
        ("ListTasks", new Lf.A2a.V1.A2AService_ListTasks_Handler(impl)),
        ("CancelTask", new Lf.A2a.V1.A2AService_CancelTask_Handler(impl)),
        ("SubscribeToTask", new Lf.A2a.V1.A2AService_SubscribeToTask_Handler(impl)),
        ("CreateTaskPushNotificationConfig", new Lf.A2a.V1.A2AService_CreateTaskPushNotificationConfig_Handler(impl)),
        ("GetTaskPushNotificationConfig", new Lf.A2a.V1.A2AService_GetTaskPushNotificationConfig_Handler(impl)),
        ("ListTaskPushNotificationConfigs", new Lf.A2a.V1.A2AService_ListTaskPushNotificationConfigs_Handler(impl)),
        ("GetExtendedAgentCard", new Lf.A2a.V1.A2AService_GetExtendedAgentCard_Handler(impl)),
        ("DeleteTaskPushNotificationConfig", new Lf.A2a.V1.A2AService_DeleteTaskPushNotificationConfig_Handler(impl)),
    ];

    private static PeerIdentity PeerOf(Context context) => new(context.PeerSubject(), context.PeerIssuer());

    // Async, so the peer set here flows into the generated handler but not back out to the caller of Handle.
    private sealed class UnaryWithPeer(UnaryUnaryHandler inner) : UnaryUnaryHandler
    {
        public async Task<byte[]> Handle(byte[] request, Context context)
        {
            Peer = PeerOf(context);
            return await inner.Handle(request, context).ConfigureAwait(false);
        }
    }

    private sealed class StreamWithPeer(UnaryStreamHandler inner) : UnaryStreamHandler
    {
        public async Task Handle(byte[] request, Context context, ResponseSink sink)
        {
            Peer = PeerOf(context);
            await inner.Handle(request, context, sink).ConfigureAwait(false);
        }
    }
}

/// <summary>The verified identity claims of the peer that set up a SLIMRPC session.</summary>
internal sealed record PeerIdentity(string? Subject, string? Issuer);
