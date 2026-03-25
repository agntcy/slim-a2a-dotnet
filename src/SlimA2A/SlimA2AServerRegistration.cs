namespace SlimA2A;

/// <summary>Registers the generated A2A SLIMRPC service on a SLIM <see cref="uniffi.slim_bindings.Server"/>.</summary>
public static class SlimA2AServerRegistration
{
    public static void RegisterA2AService(uniffi.slim_bindings.Server server, A2a.V1.IA2AServiceServer implementation) =>
        A2a.V1.A2AServiceServerRegistration.RegisterA2AServiceServer(server, implementation);
}
