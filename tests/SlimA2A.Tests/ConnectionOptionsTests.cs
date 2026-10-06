using System.Text.Json.Nodes;
using Agntcy.Slim;
using Xunit;

namespace SlimA2A.Tests;

/// <summary>
/// The SLIM client configuration built from <see cref="SlimA2AConnectionOptions"/>. SLIM's own parser checks values and
/// structure (durations, <c>type</c> tags), but it silently ignores unknown keys, so the shape assertions are what catch
/// a misspelled key, which would otherwise quietly fall back to a default such as retrying forever.
/// </summary>
public sealed class ConnectionOptionsTests
{
    private static JsonNode Config(SlimA2AConnectionOptions options) =>
        JsonNode.Parse(SlimA2AConnection.CreateClientConfigJson(options))!;

    private static SlimA2AConnectionOptions Options(string endpoint = "http://127.0.0.1:46357", SlimA2ATls? tls = null) =>
        new() { Endpoint = endpoint, Tls = tls };

    public static TheoryData<string> Variants() => new()
    {
        "http default",
        "https default",
        "insecure",
        "system roots",
        "skip verify",
        "trusted ca",
        "trusted ca + system roots",
        "mutual tls",
    };

    private static SlimA2AConnectionOptions Variant(string name) => name switch
    {
        "http default" => Options(),
        "https default" => Options("https://slim.example.com:46357"),
        "insecure" => Options(tls: SlimA2ATls.Insecure),
        "system roots" => Options("https://slim.example.com:46357", SlimA2ATls.SystemRoots),
        "skip verify" => Options("https://slim.example.com:46357", SlimA2ATls.InsecureSkipVerify),
        "trusted ca" => Options("https://slim.example.com:46357", SlimA2ATls.TrustedCa("/etc/slim/ca.pem")),
        "trusted ca + system roots" => Options("https://slim.example.com:46357", SlimA2ATls.TrustedCa("/etc/slim/ca.pem", includeSystemRoots: true)),
        "mutual tls" => Options("https://slim.example.com:46357",
            SlimA2ATls.TrustedCa("/etc/slim/ca.pem").WithClientCertificate("/etc/slim/client.pem", "/etc/slim/client.key")),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Variants))]
    public void Configuration_is_accepted_by_SLIM(string variant)
    {
        var config = Slim.NewClientConfigFromJson(SlimA2AConnection.CreateClientConfigJson(Variant(variant)));

        Assert.Equal(Variant(variant).Endpoint, config.Endpoint);
    }

    [Fact]
    public void Http_endpoint_defaults_to_no_tls()
    {
        var tls = Config(Options())["tls"]!;

        Assert.True(tls["insecure"]!.GetValue<bool>());
    }

    [Fact]
    public void Https_endpoint_defaults_to_tls_verified_against_system_roots()
    {
        var tls = Config(Options("https://slim.example.com:46357"))["tls"]!;

        Assert.False(tls["insecure"]!.GetValue<bool>());
        Assert.False(tls["insecure_skip_verify"]!.GetValue<bool>());
        Assert.True(tls["include_system_ca_certs_pool"]!.GetValue<bool>());
    }

    [Fact]
    public void InsecureSkipVerify_encrypts_without_verifying()
    {
        var tls = Config(Options("https://slim.example.com:46357", SlimA2ATls.InsecureSkipVerify))["tls"]!;

        Assert.False(tls["insecure"]!.GetValue<bool>());
        Assert.True(tls["insecure_skip_verify"]!.GetValue<bool>());
    }

    [Fact]
    public void TrustedCa_verifies_against_the_ca_file_only()
    {
        var tls = Config(Options("https://slim.example.com:46357", SlimA2ATls.TrustedCa("/etc/slim/ca.pem")))["tls"]!;

        Assert.Equal("file", tls["ca_source"]!["type"]!.GetValue<string>());
        Assert.Equal("/etc/slim/ca.pem", tls["ca_source"]!["path"]!.GetValue<string>());
        Assert.False(tls["include_system_ca_certs_pool"]!.GetValue<bool>());
    }

    [Fact]
    public void Client_certificate_adds_mutual_tls()
    {
        var tls = Config(Options("https://slim.example.com:46357",
            SlimA2ATls.SystemRoots.WithClientCertificate("/etc/slim/client.pem", "/etc/slim/client.key")))["tls"]!;

        Assert.Equal("file", tls["source"]!["type"]!.GetValue<string>());
        Assert.Equal("/etc/slim/client.pem", tls["source"]!["cert"]!.GetValue<string>());
        Assert.Equal("/etc/slim/client.key", tls["source"]!["key"]!.GetValue<string>());
    }

    [Fact]
    public void Client_certificate_requires_tls()
    {
        Assert.Throws<InvalidOperationException>(() => SlimA2ATls.Insecure.WithClientCertificate("c.pem", "c.key"));
    }

    [Theory]
    [InlineData(3, 3, "3000ms")]
    [InlineData(30, 30, "10000ms")]
    [InlineData(0.5, 1, "500ms")]
    public void Connect_retries_end_around_the_connect_timeout(double timeoutSeconds, long attempts, string attemptTimeout)
    {
        var config = Config(new SlimA2AConnectionOptions
        {
            Endpoint = "http://127.0.0.1:46357",
            ConnectTimeout = TimeSpan.FromSeconds(timeoutSeconds),
        });

        Assert.Equal("fixed_interval", config["backoff"]!["type"]!.GetValue<string>());
        Assert.Equal(attempts, config["backoff"]!["max_attempts"]!.GetValue<long>());
        Assert.Equal(attemptTimeout, config["connect_timeout"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("127.0.0.1:46357")]
    [InlineData("ftp://slim.example.com")]
    [InlineData("relative/path")]
    [InlineData("")]
    public void Endpoint_must_be_an_absolute_http_or_https_url(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => SlimA2AConnection.CreateClientConfigJson(Options(endpoint)));
    }

    [Fact]
    public void Connect_timeout_must_be_positive()
    {
        Assert.Throws<ArgumentException>(() => SlimA2AConnection.CreateClientConfigJson(
            new SlimA2AConnectionOptions { Endpoint = "http://127.0.0.1:46357", ConnectTimeout = TimeSpan.Zero }));
    }

    [Theory]
    [InlineData("agntcy/a2a/echo", "agntcy/a2a/echo")]
    [InlineData("slim://agntcy/a2a/echo", "agntcy/a2a/echo")]
    [InlineData("SLIM://agntcy/a2a/echo/", "agntcy/a2a/echo")]
    public void Remote_accepts_an_agent_card_slim_url(string remote, string name)
    {
        Assert.Equal(name, SlimA2AClientOptions.ToSlimName(remote));
    }
}
