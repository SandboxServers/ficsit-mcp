namespace FicsitMcp.Domain.Http;

/// <summary>
/// Persistent store of trusted certificate thumbprints, keyed by host, backing the
/// trust-on-first-use (TOFU) policy. On first contact with a host the thumbprint is recorded;
/// on every later contact the presented thumbprint is checked against the stored one.
/// </summary>
public interface ICertificatePinStore
{
    /// <summary>Absolute path of the underlying store, surfaced in error messages.</summary>
    string PinFilePath { get; }

    /// <summary>Returns the pinned thumbprint for <paramref name="host"/>, or null if none yet.</summary>
    string? GetPinned(string host);

    /// <summary>
    /// Records <paramref name="thumbprint"/> as the pin for <paramref name="host"/> on first
    /// contact. Persists immediately so a restart keeps trusting the same certificate.
    /// </summary>
    void Pin(string host, string thumbprint);

    /// <summary>
    /// Atomically returns the EFFECTIVE pin for <paramref name="host"/>: the existing pin if one is
    /// already stored, otherwise stores <paramref name="thumbprint"/> and returns it. Unlike a
    /// check-then-<see cref="Pin"/> sequence, this is a single atomic operation, so two concurrent
    /// first-contact attempts presenting different certificates resolve deterministically — the
    /// first writer wins and the loser gets back the winner's value (which it can compare against
    /// what it presented and reject on mismatch). The returned value is normalized to the store's
    /// canonical thumbprint form.
    /// </summary>
    string GetOrPin(string host, string thumbprint);
}
