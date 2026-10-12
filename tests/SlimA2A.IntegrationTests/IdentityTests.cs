using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using A2A;
using Microsoft.Extensions.Logging.Abstractions;
using uniffi.slim_bindings;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// SLIM identities other than a shared secret: JWTs signed with each agent's own key and verified against a JWKS of the
/// trusted keys, and the verified caller identity reaching the agent through <see cref="SlimA2ACallContext"/>.
/// </summary>
public sealed class IdentityTests(SlimNodeFixture node)
{
    private const string Issuer = "slima2a-it";
    private const string Audience = "slima2a_it";

    /// <summary>An ES256 signing key whose JWTs carry <see cref="Subject"/> (also the key's <c>kid</c> in the JWKS).</summary>
    private sealed class JwtKey(string subject) : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public string Subject { get; } = subject;

        public IdentityProviderConfig Provider() => new IdentityProviderConfig.Jwt(new ClientJwtAuth(
            new JwtKeyType.Encoding(new JwtKeyConfig(JwtAlgorithm.Es256, JwtKeyFormat.Pem, new JwtKeyData.Data(_key.ExportPkcs8PrivateKeyPem()))),
            [Audience], Issuer, Subject, TimeSpan.FromHours(1)));

        public JsonObject Jwk()
        {
            var p = _key.ExportParameters(includePrivateParameters: false);
            return new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["alg"] = "ES256",
                ["use"] = "sig",
                ["kid"] = Subject,
                ["x"] = Base64Url(p.Q.X!),
                ["y"] = Base64Url(p.Q.Y!),
            };
        }

        public void Dispose() => _key.Dispose();

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Accepts JWTs from <paramref name="trusted"/> keys only, any subject. Every member of a session verifies every
    /// member, itself included, so each side trusts its own key as well as its peer's.
    /// </summary>
    private static IdentityVerifierConfig Verifier(params JwtKey[] trusted)
    {
        var jwks = new JsonObject { ["keys"] = new JsonArray(trusted.Select(k => (JsonNode)k.Jwk()).ToArray()) }.ToJsonString();
        return new IdentityVerifierConfig.Jwt(new JwtAuth(
            new JwtKeyType.Decoding(new JwtKeyConfig(JwtAlgorithm.Es256, JwtKeyFormat.Jwks, new JwtKeyData.Data(jwks))),
            [Audience], Issuer, null, TimeSpan.FromHours(1)));
    }

    private async Task<SlimA2AServer> StartJwtServerAsync(string identity, JwtKey key, IdentityVerifierConfig verifier)
    {
        var a2a = new A2AServer(new TestAgent(), new InMemoryTaskStore(), new ChannelEventNotifier(), NullLogger<A2AServer>.Instance);
        return await node.Connection.StartServerAsync(
            new SlimA2AServerOptions { Identity = identity, IdentityProvider = key.Provider(), IdentityVerifier = verifier }, a2a);
    }

    private SlimA2AClient JwtClient(string identity, string remote, JwtKey key, IdentityVerifierConfig verifier, TimeSpan timeout) =>
        node.Connection.CreateClient(new SlimA2AClientOptions
        {
            Identity = identity,
            IdentityProvider = key.Provider(),
            IdentityVerifier = verifier,
            Remote = remote,
            DefaultTimeout = timeout,
        });

    /// <summary>Sends until the new server answers: its subscription takes a moment to reach the node.</summary>
    private static async Task<SendMessageResponse> SendWhenReadyAsync(SlimA2AClient client, string text, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            try
            {
                return await client.SendMessageAsync(TestRequests.Text(text), ct);
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(250, ct);
            }
        }
    }

    private static JsonElement CallContextSeen(SendMessageResponse reply)
    {
        using var json = JsonDocument.Parse(reply.Message!.Parts![0].Text!);
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task With_a_shared_secret_the_agent_sees_the_callers_SLIM_name()
    {
        var ct = TestContext.Current.CancellationToken;

        var seen = CallContextSeen(await node.Client.SendMessageAsync(TestRequests.Text(TestAgent.CallContext), ct));

        // SLIM appends a random suffix to the name it uses as the shared-secret identity.
        Assert.StartsWith($"agntcy/slima2a_it/client_{node.RunId}_", seen.GetProperty("peerSubject").GetString());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("peerIssuer").ValueKind);
    }

    [Fact]
    public async Task Agents_with_JWT_identities_talk_and_the_agent_sees_the_verified_caller()
    {
        var ct = TestContext.Current.CancellationToken;
        using var serverKey = new JwtKey($"server-{node.RunId}");
        using var clientKey = new JwtKey($"client-{node.RunId}");
        var trusted = Verifier(serverKey, clientKey);
        var serverIdentity = $"agntcy/slima2a_it/jwt_server_{node.RunId}";
        await using var server = await StartJwtServerAsync(serverIdentity, serverKey, trusted);
        await using var client = JwtClient(
            $"agntcy/slima2a_it/jwt_client_{node.RunId}", serverIdentity, clientKey, trusted, TimeSpan.FromSeconds(5));

        var reply = await SendWhenReadyAsync(client, TestAgent.CallContext, ct);

        var seen = CallContextSeen(reply);
        Assert.Equal(clientKey.Subject, seen.GetProperty("peerSubject").GetString());
        Assert.Equal(Issuer, seen.GetProperty("peerIssuer").GetString());
    }

    [Fact]
    public async Task A_caller_whose_key_the_agent_does_not_trust_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var serverKey = new JwtKey($"server-{node.RunId}");
        using var trustedKey = new JwtKey($"trusted-{node.RunId}");
        using var untrustedKey = new JwtKey($"untrusted-{node.RunId}");
        var serverIdentity = $"agntcy/slima2a_it/jwt_strict_{node.RunId}";
        await using var server = await StartJwtServerAsync(serverIdentity, serverKey, Verifier(serverKey, trustedKey));

        // The trusted caller gets through, so the server is up when the untrusted one calls.
        await using (var trusted = JwtClient(
            $"agntcy/slima2a_it/jwt_trusted_{node.RunId}", serverIdentity, trustedKey, Verifier(serverKey, trustedKey), TimeSpan.FromSeconds(5)))
        {
            await SendWhenReadyAsync(trusted, TestAgent.MessageOnly, ct);
        }

        // It trusts the server and itself; only the server's verifier rejects it.
        await using var untrusted = JwtClient(
            $"agntcy/slima2a_it/jwt_untrusted_{node.RunId}", serverIdentity, untrustedKey, Verifier(serverKey, untrustedKey),
            TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<Exception>(() => untrusted.SendMessageAsync(TestRequests.Text(TestAgent.MessageOnly), ct));
    }
}
