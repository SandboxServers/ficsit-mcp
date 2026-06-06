using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FicsitMcp.Domain.Http;

/// <summary>
/// Trust-on-first-use (TOFU) certificate validation for the dedicated server, which ships a
/// self-signed certificate the default chain validation rejects. On first contact with a host the
/// presented certificate's thumbprint is pinned via <see cref="ICertificatePinStore"/>; on every
/// later contact the presented thumbprint must match the pin, or the connection is refused with
/// <see cref="CertificatePinMismatchException"/> (the signature of a man-in-the-middle).
/// </summary>
/// <remarks>
/// This validator is wired into the dedicated-server <c>HttpClientHandler</c> /
/// <c>SocketsHttpHandler</c> via its server-certificate-validation callback. When
/// <see cref="DedicatedServerOptions.DangerousAcceptAnyCert"/> is set (DEV ONLY) the validator
/// short-circuits to "accept everything" without pinning — the documented escape hatch.
/// </remarks>
public sealed class TofuCertificateValidator
{
    private readonly ICertificatePinStore _pinStore;
    private readonly bool _dangerouslyAcceptAnyCert;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a validator backed by <paramref name="pinStore"/>.
    /// </summary>
    /// <param name="pinStore">Persistent thumbprint store.</param>
    /// <param name="dangerouslyAcceptAnyCert">
    /// DEV ONLY. When true, every certificate is accepted with no pinning (wires
    /// <see cref="DedicatedServerOptions.DangerousAcceptAnyCert"/>).
    /// </param>
    /// <param name="logger">Optional logger; first-contact pinning is logged at Information.</param>
    public TofuCertificateValidator(
        ICertificatePinStore pinStore,
        bool dangerouslyAcceptAnyCert,
        ILogger<TofuCertificateValidator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pinStore);
        _pinStore = pinStore;
        _dangerouslyAcceptAnyCert = dangerouslyAcceptAnyCert;
        _logger = logger ?? NullLogger<TofuCertificateValidator>.Instance;
    }

    /// <summary>
    /// The callback to assign to a handler's server-certificate-validation hook. Signature matches
    /// <see cref="System.Net.Http.HttpClientHandler.ServerCertificateCustomValidationCallback"/>
    /// and <see cref="System.Net.Http.SocketsHttpHandler.SslOptions"/>'s
    /// <see cref="SslClientAuthenticationOptions.RemoteCertificateValidationCallback"/> (after
    /// adapting). Throws <see cref="CertificatePinMismatchException"/> on a changed certificate.
    /// </summary>
    public bool Validate(string host, X509Certificate2? certificate)
    {
        if (_dangerouslyAcceptAnyCert)
        {
            // Escape hatch: skip all validation AND skip pinning, so dev churn (a server that
            // regenerates its cert every boot) doesn't write throwaway pins. Deliberately loud name.
            _logger.LogWarning(
                "DangerousAcceptAnyCert is enabled for '{Host}': accepting the TLS certificate " +
                "without trust-on-first-use pinning. DEV ONLY — never use against a server you do " +
                "not fully control.",
                host);
            return true;
        }

        if (certificate is null)
        {
            // No certificate to pin or compare: cannot establish trust, so refuse.
            return false;
        }

        // Pin a SHA-256 hash of the certificate. SHA-1 (X509Certificate2.Thumbprint) is collision-
        // prone and unsuitable as a security anchor; SHA-256 gives a collision-resistant pin.
        string presented = certificate.GetCertHashString(HashAlgorithmName.SHA256);

        // Best-effort read purely to decide whether to LOG a first-contact pin (not part of the
        // trust decision — that stays atomic in GetOrPin below). A benign race here only affects the
        // log line, never whether we trust the certificate.
        bool looksLikeFirstContact = _pinStore.GetPinned(host) is null;

        // GetOrPin is atomic: on first contact it stores and returns the presented hash; if a pin
        // already exists (including a pin written by a racing first-contact) it returns that
        // existing value instead. So the returned "effective" pin is what we must match against —
        // a lost first-contact race becomes a deterministic mismatch below rather than two writers
        // clobbering each other.
        string effective = _pinStore.GetOrPin(host, presented);

        if (ThumbprintsMatch(effective, presented))
        {
            if (looksLikeFirstContact)
            {
                // First contact: trust and pin. This is the "use" in trust-on-first-use; the window
                // of exposure is exactly the first connection, the standard TOFU tradeoff.
                _logger.LogInformation(
                    "Pinned TLS certificate for '{Host}' on first contact (SHA-256 {Thumbprint}). " +
                    "Future connections must present the same certificate.",
                    host,
                    presented);
            }

            return true;
        }

        // Pinned thumbprint differs from what the server presented: refuse loudly rather than
        // silently trusting a new certificate (which would defeat the whole point of pinning).
        throw new CertificatePinMismatchException(host, effective, presented, _pinStore.PinFilePath);
    }

    // Thumbprints compare case-insensitively; the store already canonicalizes, but the presented
    // value comes straight from the cert, so normalize both ends here too.
    private static bool ThumbprintsMatch(string pinned, string presented) =>
        string.Equals(pinned, presented, StringComparison.OrdinalIgnoreCase);
}
