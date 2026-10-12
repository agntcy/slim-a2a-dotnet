using A2A;
using uniffi.slim_bindings;

namespace SlimA2A;

/// <summary>Options for <see cref="SlimA2AConnection.StartServerAsync"/>.</summary>
public sealed class SlimA2AServerOptions
{
    /// <summary>The server's SLIM identity in <c>org/namespace/app</c> form; clients call the agent by this name.</summary>
    public required string Identity { get; init; }

    /// <summary>
    /// The shared secret authenticating this identity, at least 32 characters. Clients must use the same secret. Set either
    /// this or <see cref="IdentityProvider"/> and <see cref="IdentityVerifier"/>.
    /// </summary>
    public string? SharedSecret { get; init; }

    /// <summary>
    /// How the server proves its identity, for example a JWT signed with its own key or a SPIRE identity. Set together
    /// with <see cref="IdentityVerifier"/>, instead of <see cref="SharedSecret"/>.
    /// </summary>
    public IdentityProviderConfig? IdentityProvider { get; init; }

    /// <summary>
    /// How the server verifies its callers' identities, for example JWTs checked against a JWKS of trusted keys. The
    /// verified identity is available to request handling as <see cref="SlimA2ACallContext.PeerSubject"/> and
    /// <see cref="SlimA2ACallContext.PeerIssuer"/>. Set together with <see cref="IdentityProvider"/>.
    /// </summary>
    public IdentityVerifierConfig? IdentityVerifier { get; init; }

    /// <summary>
    /// Optional source of the extended agent card returned by <c>GetExtendedAgentCard</c>. When null, the request handler's
    /// <see cref="IA2ARequestHandler.GetExtendedAgentCardAsync"/> answers. The request carries the caller's tenant.
    /// </summary>
    public Func<GetExtendedAgentCardRequest, CancellationToken, Task<AgentCard>>? ResolveExtendedAgentCard { get; init; }
}
