namespace SlimA2A;

/// <summary>
/// The SLIMRPC call an A2A request arrived on, available as <see cref="Current"/> to the request handler and the agent code
/// it runs while the request is handled.
/// </summary>
public sealed class SlimA2ACallContext
{
    private static readonly AsyncLocal<SlimA2ACallContext?> CurrentContext = new();
    private static readonly IReadOnlyDictionary<string, string> NoMetadata = new Dictionary<string, string>();

    internal SlimA2ACallContext(IReadOnlyDictionary<string, string>? metadata, string? peerSubject = null, string? peerIssuer = null)
    {
        PeerSubject = peerSubject;
        PeerIssuer = peerIssuer;
        Metadata = metadata is null
            ? NoMetadata
            : metadata.Where(kv => !SlimA2AMetadata.Reserved.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        RequestedExtensions = Metadata.TryGetValue(SlimA2AMetadata.Extensions, out var extensions)
            ? extensions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [];
    }

    /// <summary>The call being handled, or null outside of request handling.</summary>
    public static SlimA2ACallContext? Current
    {
        get => CurrentContext.Value;
        internal set => CurrentContext.Value = value;
    }

    /// <summary>The metadata the client sent with the call (<see cref="SlimA2AClientOptions.Metadata"/> on a .NET client).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>The extension URIs listed in the <see cref="SlimA2AMetadata.Extensions"/> metadata, if any.</summary>
    public IReadOnlyList<string> RequestedExtensions { get; }

    /// <summary>
    /// The caller's subject (<c>sub</c>), as verified by the server's identity verifier when the caller's SLIMRPC session
    /// was set up, or null when its identity has none. With JWT identities this is the token's subject; with a shared
    /// secret it is the caller's SLIM name plus a random suffix, which only proves the caller holds the secret.
    /// </summary>
    public string? PeerSubject { get; }

    /// <summary>The issuer (<c>iss</c>) of the caller's verified identity, or null when it has none, as with a shared secret.</summary>
    public string? PeerIssuer { get; }
}
