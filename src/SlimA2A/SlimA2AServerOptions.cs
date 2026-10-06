using A2A;

namespace SlimA2A;

/// <summary>Options for <see cref="SlimA2AConnection.StartServerAsync"/>.</summary>
public sealed class SlimA2AServerOptions
{
    /// <summary>The server's SLIM identity in <c>org/namespace/app</c> form; clients call the agent by this name.</summary>
    public required string Identity { get; init; }

    /// <summary>The shared secret authenticating this identity, at least 32 characters. Clients must use the same secret.</summary>
    public required string SharedSecret { get; init; }

    /// <summary>
    /// Optional source of the extended agent card returned by <c>GetExtendedAgentCard</c>. When null, the request handler's
    /// <see cref="IA2ARequestHandler.GetExtendedAgentCardAsync"/> answers. The request carries the caller's tenant.
    /// </summary>
    public Func<GetExtendedAgentCardRequest, CancellationToken, Task<AgentCard>>? ResolveExtendedAgentCard { get; init; }
}
