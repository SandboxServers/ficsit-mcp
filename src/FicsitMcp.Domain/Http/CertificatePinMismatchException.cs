namespace FicsitMcp.Domain.Http;

/// <summary>
/// Thrown by the trust-on-first-use certificate validator when a surface presents a certificate
/// whose thumbprint differs from the one pinned on first contact. A silent thumbprint change is
/// the signature of a man-in-the-middle attack, so the connection is refused rather than trusted.
/// The message is actionable: it names the pin file the operator deletes to re-pin (e.g. after a
/// legitimate certificate rotation).
/// </summary>
public sealed class CertificatePinMismatchException : Exception
{
    /// <summary>The host whose certificate changed.</summary>
    public string Host { get; }

    /// <summary>The thumbprint pinned on first contact.</summary>
    public string PinnedThumbprint { get; }

    /// <summary>The thumbprint just presented, which did not match the pin.</summary>
    public string PresentedThumbprint { get; }

    /// <summary>Absolute path to the pin store, surfaced so the operator can re-pin deliberately.</summary>
    public string PinFilePath { get; }

    /// <summary>Builds the standard pin-mismatch message.</summary>
    public CertificatePinMismatchException(
        string host,
        string pinnedThumbprint,
        string presentedThumbprint,
        string pinFilePath)
        : base(BuildMessage(host, pinnedThumbprint, presentedThumbprint, pinFilePath))
    {
        Host = host;
        PinnedThumbprint = pinnedThumbprint;
        PresentedThumbprint = presentedThumbprint;
        PinFilePath = pinFilePath;
    }

    private static string BuildMessage(
        string host,
        string pinnedThumbprint,
        string presentedThumbprint,
        string pinFilePath) =>
        $"TLS certificate for '{host}' changed since first contact: pinned {pinnedThumbprint} " +
        $"but server presented {presentedThumbprint}. This can mean a man-in-the-middle attack. " +
        $"If the server's certificate was rotated deliberately, remove the entry for '{host}' " +
        $"from the pin store at {pinFilePath} (or delete the file) to re-pin on next contact.";
}
