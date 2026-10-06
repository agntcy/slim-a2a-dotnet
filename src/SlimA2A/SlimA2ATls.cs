using System.Text.Json.Nodes;

namespace SlimA2A;

/// <summary>
/// Transport security for the connection to a SLIM node. Start from <see cref="Insecure"/>, <see cref="SystemRoots"/>,
/// <see cref="InsecureSkipVerify"/> or <see cref="TrustedCa"/>, and add a client certificate for mutual TLS with
/// <see cref="WithClientCertificate"/>.
/// </summary>
public sealed class SlimA2ATls
{
    private readonly bool _plaintext;
    private readonly bool _skipVerify;
    private readonly string? _caFile;
    private readonly bool _includeSystemRoots;
    private readonly string? _certificateFile;
    private readonly string? _keyFile;

    private SlimA2ATls(
        bool plaintext,
        bool skipVerify = false,
        string? caFile = null,
        bool includeSystemRoots = true,
        string? certificateFile = null,
        string? keyFile = null)
    {
        _plaintext = plaintext;
        _skipVerify = skipVerify;
        _caFile = caFile;
        _includeSystemRoots = includeSystemRoots;
        _certificateFile = certificateFile;
        _keyFile = keyFile;
    }

    /// <summary>No TLS: traffic to the node is unencrypted. Only for local or test nodes.</summary>
    public static SlimA2ATls Insecure { get; } = new(plaintext: true);

    /// <summary>TLS, verifying the node's certificate against the system's trusted root CAs.</summary>
    public static SlimA2ATls SystemRoots { get; } = new(plaintext: false);

    /// <summary>
    /// TLS without verifying the node's certificate. Traffic is encrypted but the node is not authenticated, so this is
    /// only for development against nodes with self-signed certificates.
    /// </summary>
    public static SlimA2ATls InsecureSkipVerify { get; } = new(plaintext: false, skipVerify: true);

    /// <summary>TLS, verifying the node's certificate against the CA certificates in a PEM file.</summary>
    /// <param name="caFilePath">Path to a PEM file with one or more CA certificates.</param>
    /// <param name="includeSystemRoots">Whether the system's trusted root CAs are trusted as well.</param>
    /// <returns>The TLS settings.</returns>
    /// <exception cref="ArgumentException"><paramref name="caFilePath"/> is null or empty.</exception>
    public static SlimA2ATls TrustedCa(string caFilePath, bool includeSystemRoots = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(caFilePath);
        return new(plaintext: false, caFile: caFilePath, includeSystemRoots: includeSystemRoots);
    }

    /// <summary>Returns a copy of these settings that also presents a client certificate (mutual TLS).</summary>
    /// <param name="certificateFilePath">Path to the client certificate, in PEM format.</param>
    /// <param name="keyFilePath">Path to the client certificate's private key, in PEM format.</param>
    /// <returns>The TLS settings with the client certificate.</returns>
    /// <exception cref="ArgumentException">A path is null or empty.</exception>
    /// <exception cref="InvalidOperationException">These settings are <see cref="Insecure"/>, which has no TLS to add a certificate to.</exception>
    public SlimA2ATls WithClientCertificate(string certificateFilePath, string keyFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(certificateFilePath);
        ArgumentException.ThrowIfNullOrEmpty(keyFilePath);
        if (_plaintext)
            throw new InvalidOperationException("A client certificate requires TLS; start from SystemRoots or TrustedCa instead of Insecure.");
        return new(_plaintext, _skipVerify, _caFile, _includeSystemRoots, certificateFilePath, keyFilePath);
    }

    /// <summary>The <c>tls</c> object of the SLIM client configuration (slim's client-config JSON schema).</summary>
    internal JsonObject ToJson()
    {
        var tls = new JsonObject
        {
            ["insecure"] = _plaintext,
            ["insecure_skip_verify"] = _skipVerify,
            ["include_system_ca_certs_pool"] = _includeSystemRoots,
        };
        if (_caFile is not null)
            tls["ca_source"] = new JsonObject { ["type"] = "file", ["path"] = _caFile };
        if (_certificateFile is not null)
            tls["source"] = new JsonObject { ["type"] = "file", ["cert"] = _certificateFile, ["key"] = _keyFile };
        return tls;
    }
}
