namespace SlimA2A;

/// <summary>Options for <see cref="SlimA2AConnection.CreateClient"/>.</summary>
public sealed class SlimA2AClientOptions
{
    private const string SlimScheme = "slim://";

    /// <summary>The client's own SLIM identity in <c>org/namespace/app</c> form. Use a different identity per client and server.</summary>
    public required string Identity { get; init; }

    /// <summary>The shared secret authenticating this identity, at least 32 characters. It must match the agent's.</summary>
    public required string SharedSecret { get; init; }

    /// <summary>
    /// The agent to call: its SLIM identity (<c>org/namespace/app</c>), or the <c>slim://org/namespace/app</c> URL its agent
    /// card lists in <c>SupportedInterfaces</c>.
    /// </summary>
    public required string Remote { get; init; }

    /// <summary>Deadline for each RPC. When null, the SLIM runtime's default applies.</summary>
    public TimeSpan? DefaultTimeout { get; init; }

    /// <summary>
    /// Metadata sent with every RPC, read on the server through <see cref="SlimA2ACallContext.Metadata"/>; for example the
    /// requested extensions under <see cref="SlimA2AMetadata.Extensions"/>. Captured when the client is created.
    /// </summary>
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>The SLIM name in <paramref name="remote"/>, without a <c>slim://</c> scheme.</summary>
    internal static string ToSlimName(string remote) =>
        remote is not null && remote.StartsWith(SlimScheme, StringComparison.OrdinalIgnoreCase)
            ? remote[SlimScheme.Length..].TrimEnd('/')
            : remote!;
}
