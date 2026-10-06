using Agntcy.Slim;

namespace SlimA2A;

/// <summary>Options for <see cref="SlimA2AConnection.ConnectAsync"/>.</summary>
public sealed class SlimA2AConnectionOptions
{
    /// <summary>The SLIM node's endpoint, for example <c>http://127.0.0.1:46357</c> or <c>https://slim.example.com:46357</c>.</summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Transport security for the connection. When null, it follows the endpoint's scheme: <c>http</c> connects without
    /// TLS (<see cref="SlimA2ATls.Insecure"/>), <c>https</c> with TLS against the system's root CAs
    /// (<see cref="SlimA2ATls.SystemRoots"/>).
    /// </summary>
    public SlimA2ATls? Tls { get; init; }

    /// <summary>
    /// How long <see cref="SlimA2AConnection.ConnectAsync"/> keeps trying to reach the node before it fails with
    /// <see cref="TimeoutException"/>. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional hook to adjust the SLIM client configuration before connecting, for settings these options don't cover,
    /// such as authenticating to the node with <see cref="SlimClientConfig.WithOidc"/>.
    /// </summary>
    public Func<SlimClientConfig, SlimClientConfig>? ConfigureClient { get; init; }
}
