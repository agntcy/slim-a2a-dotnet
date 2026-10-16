using System.Security.Cryptography;
using A2A;
using Agntcy.Slim;
using Microsoft.Extensions.Logging.Abstractions;
using uniffi.slim_bindings;
using Xunit;

namespace SlimA2A.Tests;

/// <summary>
/// How servers and clients authenticate (a shared secret, or an identity provider and verifier), and the verified caller
/// identity reaching request handling through <see cref="SlimA2ACallContext"/>.
/// </summary>
public sealed class IdentityTests
{
    private const string Secret = "unit-test-shared-secret-of-32-chars+";

    private static readonly IdentityProviderConfig Provider = new IdentityProviderConfig.SharedSecret("id", Secret);
    private static readonly IdentityVerifierConfig Verifier = new IdentityVerifierConfig.SharedSecret("id", Secret);

    private sealed class ObservingAgent : IAgentHandler
    {
        public SlimA2ACallContext? Seen { get; private set; }

        public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
        {
            Seen = SlimA2ACallContext.Current;
            await new MessageResponder(eventQueue, context.ContextId).ReplyAsync("ok", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public Task CancelAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Lf.A2a.V1.SendMessageRequest ProtoRequest() =>
        ProtoConverter.ToProto(new SendMessageRequest
        {
            Message = new Message { Role = Role.User, MessageId = Guid.NewGuid().ToString("N"), Parts = [Part.FromText("hi")] },
        });

    private static SlimA2AHandler NewHandler(IAgentHandler agent) =>
        new(new A2AServer(agent, new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance));

    [Fact]
    public void A_shared_secret_alone_is_valid() =>
        SlimA2AIdentity.Validate(Secret, null, null, "options");

    [Fact]
    public void An_identity_provider_and_verifier_without_a_shared_secret_are_valid() =>
        SlimA2AIdentity.Validate(null, Provider, Verifier, "options");

    public static TheoryData<string, string?, bool, bool, string> Invalid() => new()
    {
        { "nothing", null, false, false, "Set SharedSecret, or IdentityProvider and IdentityVerifier." },
        { "empty secret", "", false, false, "SharedSecret must not be empty." },
        { "secret and provider", Secret, true, false, "not both" },
        { "secret and verifier", Secret, false, true, "not both" },
        { "secret, provider and verifier", Secret, true, true, "not both" },
        { "provider only", null, true, false, "must be set together" },
        { "verifier only", null, false, true, "must be set together" },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Anything_but_exactly_one_way_to_authenticate_is_rejected(
        string variant, string? secret, bool provider, bool verifier, string message)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SlimA2AIdentity.Validate(secret, provider ? Provider : null, verifier ? Verifier : null, "options"));

        Assert.Contains(message, ex.Message);
        Assert.Equal("options", ex.ParamName);
        Assert.NotNull(variant);
    }

    [Fact]
    public void Options_without_a_shared_secret_compile_and_carry_the_identity_configs()
    {
        var server = new SlimA2AServerOptions { Identity = "org/ns/server", IdentityProvider = Provider, IdentityVerifier = Verifier };
        var client = new SlimA2AClientOptions { Identity = "org/ns/client", Remote = "org/ns/server", IdentityProvider = Provider, IdentityVerifier = Verifier };

        Assert.Null(server.SharedSecret);
        Assert.Same(Provider, client.IdentityProvider);
        Assert.Same(Verifier, client.IdentityVerifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-slim-name")]
    public void An_invalid_SLIM_name_is_an_argument_error(string name) =>
        Assert.Throws<ArgumentException>(() => SlimA2AIdentity.ParseName(name, "options"));

    [Fact]
    public void An_app_is_created_with_a_JWT_identity_provider_and_a_JWKS_verifier()
    {
        Slim.Initialize();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: false);
        var jwks = $$"""{"keys":[{"kty":"EC","crv":"P-256","alg":"ES256","kid":"me","x":"{{B64(p.Q.X!)}}","y":"{{B64(p.Q.Y!)}}"}]}""";
        var provider = new IdentityProviderConfig.Jwt(new ClientJwtAuth(
            new JwtKeyType.Encoding(new JwtKeyConfig(JwtAlgorithm.Es256, JwtKeyFormat.Pem, new JwtKeyData.Data(key.ExportPkcs8PrivateKeyPem()))),
            ["aud"], "iss", "me", TimeSpan.FromHours(1)));
        var verifier = new IdentityVerifierConfig.Jwt(new JwtAuth(
            new JwtKeyType.Decoding(new JwtKeyConfig(JwtAlgorithm.Es256, JwtKeyFormat.Jwks, new JwtKeyData.Data(jwks))),
            ["aud"], "iss", null, TimeSpan.FromHours(1)));
        using var service = SlimBindingsMethods.GetGlobalService();
        using var name = SlimA2AIdentity.ParseName($"agntcy/slima2a_unit/jwt_{Guid.NewGuid():N}", "options");

        using var app = SlimA2AIdentity.CreateApp(service, name, null, provider, verifier);

        Assert.Equal(name.Components(), app.Name().Components());

        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public void Every_generated_A2A_handler_is_registered_with_peer_capture()
    {
        var generated = typeof(Lf.A2a.V1.IA2AServiceServer).Assembly.GetTypes()
            .Where(t => t.Namespace == "Lf.A2a.V1" && t.Name.StartsWith("A2AService_") && t.Name.EndsWith("_Handler"))
            .Select(t => t.Name)
            .Order();

        var registered = A2AServiceRegistration.Handlers(NewHandler(new ObservingAgent()));

        Assert.Equal(generated, registered.Select(h => h.Handler.GetType().Name).Order());
        Assert.All(registered, h => Assert.Equal($"A2AService_{h.Method}_Handler", h.Handler.GetType().Name));
    }

    [Fact]
    public void The_call_context_exposes_the_peer_identity()
    {
        var call = new SlimA2ACallContext(null, "did:key:z6Mk", "https://issuer.example");

        Assert.Equal("did:key:z6Mk", call.PeerSubject);
        Assert.Equal("https://issuer.example", call.PeerIssuer);
        Assert.Null(new SlimA2ACallContext(null).PeerSubject);
    }

    [Fact]
    public async Task Agent_code_sees_the_peer_captured_for_the_call()
    {
        var agent = new ObservingAgent();
        var handler = NewHandler(agent);

        // As the registration's wrapper does before running the generated handler.
        await Task.Run(async () =>
        {
            A2AServiceRegistration.Peer = new PeerIdentity("client-sub", "client-iss");
            await handler.SendMessage(ProtoRequest(), null!);
        }, TestContext.Current.CancellationToken);

        Assert.Equal("client-sub", agent.Seen?.PeerSubject);
        Assert.Equal("client-iss", agent.Seen?.PeerIssuer);
        Assert.Null(A2AServiceRegistration.Peer);
    }

    [Fact]
    public async Task Stream_code_sees_the_peer_captured_for_the_call()
    {
        var agent = new ObservingAgent();
        var handler = NewHandler(agent);

        await Task.Run(async () =>
        {
            A2AServiceRegistration.Peer = new PeerIdentity("stream-sub", null);
            await foreach (var _ in handler.SendStreamingMessage(ProtoRequest(), null!)) { }
        }, TestContext.Current.CancellationToken);

        Assert.Equal("stream-sub", agent.Seen?.PeerSubject);
        Assert.Null(agent.Seen?.PeerIssuer);
    }

    [Fact]
    public async Task Without_a_captured_peer_there_is_no_peer_identity()
    {
        var agent = new ObservingAgent();
        var handler = NewHandler(agent);

        await handler.SendMessage(ProtoRequest(), null!);

        Assert.NotNull(agent.Seen);
        Assert.Null(agent.Seen.PeerSubject);
        Assert.Null(agent.Seen.PeerIssuer);
    }
}
