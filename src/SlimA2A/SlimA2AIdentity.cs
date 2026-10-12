using uniffi.slim_bindings;

namespace SlimA2A;

/// <summary>Creates the SLIM app behind a server or client from its identity options.</summary>
internal static class SlimA2AIdentity
{
    /// <summary>
    /// Checks that exactly one way of authenticating is configured: <paramref name="sharedSecret"/>, or both
    /// <paramref name="provider"/> and <paramref name="verifier"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Neither or both are set, only one of provider and verifier is set, or the shared secret is empty.</exception>
    internal static void Validate(
        string? sharedSecret, IdentityProviderConfig? provider, IdentityVerifierConfig? verifier, string paramName)
    {
        if (sharedSecret is not null)
        {
            if (provider is not null || verifier is not null)
                throw new ArgumentException(
                    "Set either SharedSecret, or IdentityProvider and IdentityVerifier, not both.", paramName);
            if (sharedSecret.Length == 0)
                throw new ArgumentException("SharedSecret must not be empty.", paramName);
            return;
        }
        if (provider is null && verifier is null)
            throw new ArgumentException("Set SharedSecret, or IdentityProvider and IdentityVerifier.", paramName);
        if (provider is null || verifier is null)
            throw new ArgumentException("IdentityProvider and IdentityVerifier must be set together.", paramName);
    }

    /// <summary>Parses an <c>org/namespace/app</c> SLIM name.</summary>
    /// <exception cref="ArgumentException">The name is empty or not in <c>org/namespace/app</c> form.</exception>
    internal static Name ParseName(string name, string paramName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A SLIM name must not be empty.", paramName);
        try
        {
            return Name.FromString(name);
        }
        catch (UniffiException ex)
        {
            throw new ArgumentException(ex.Message, paramName, ex);
        }
    }

    /// <summary>Creates the app for <paramref name="name"/>, authenticated as already checked by <see cref="Validate"/>.</summary>
    internal static App CreateApp(
        Service service, Name name, string? sharedSecret, IdentityProviderConfig? provider, IdentityVerifierConfig? verifier) =>
        sharedSecret is not null
            ? service.CreateAppWithSecret(name, sharedSecret)
            : service.CreateApp(name, provider!, verifier!);
}
