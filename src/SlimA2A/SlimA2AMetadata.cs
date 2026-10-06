namespace SlimA2A;

/// <summary>Well-known keys of SLIMRPC call metadata, shared with the other A2A SLIMRPC SDKs.</summary>
public static class SlimA2AMetadata
{
    /// <summary>
    /// The A2A extensions the client requests, as a comma-separated list of extension URIs: the SLIMRPC counterpart of the
    /// <c>A2A-Extensions</c> HTTP header. Keys are case-sensitive; other SDKs read exactly this spelling.
    /// </summary>
    public const string Extensions = "A2A-Extensions";

    /// <summary>
    /// Keys SLIMRPC uses for its own routing (slim-rpc's <c>*_KEY</c> constants). They are left out of
    /// <see cref="SlimA2ACallContext.Metadata"/>, and a client can't send them.
    /// </summary>
    internal static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "service",
        "method",
        "rpc-id",
        "slimrpc-dir",
        "slimrpc-timeout",
        "slimrpc-code",
    };
}
