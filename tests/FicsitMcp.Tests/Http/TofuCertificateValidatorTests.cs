using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using FicsitMcp.Domain.Http;

namespace FicsitMcp.Tests.Http;

public sealed class TofuCertificateValidatorTests
{
    // In-memory pin store so the validator's trust/pin logic is tested in isolation from the file
    // format (FileCertificatePinStore has its own persistence tests).
    private sealed class InMemoryPinStore : ICertificatePinStore
    {
        private readonly ConcurrentDictionary<string, string> _pins = new();

        public string PinFilePath => "(in-memory test store)";

        public string? GetPinned(string host) => _pins.GetValueOrDefault(host);

        public void Pin(string host, string thumbprint) => _pins[host] = thumbprint;

        // Atomic check-or-add mirroring FileCertificatePinStore: returns the existing pin if any,
        // otherwise stores and returns the offered thumbprint. GetOrAdd gives the atomicity.
        public string GetOrPin(string host, string thumbprint) =>
            _pins.GetOrAdd(host, thumbprint);
    }

    // The validator pins a SHA-256 hash of the certificate (not the SHA-1 Thumbprint).
    private static string Sha256(X509Certificate2 cert) =>
        cert.GetCertHashString(HashAlgorithmName.SHA256);

    [Fact]
    public void Validate_FirstContact_AcceptsAndPinsThumbprint()
    {
        // Arrange
        var store = new InMemoryPinStore();
        var validator = new TofuCertificateValidator(store, dangerouslyAcceptAnyCert: false);
        using X509Certificate2 cert = TestCertificates.CreateSelfSigned();

        // Act
        bool accepted = validator.Validate("host.local", cert);

        // Assert: trusted on first contact, and the SHA-256 hash is now pinned for the host.
        Assert.True(accepted);
        Assert.Equal(Sha256(cert), store.GetPinned("host.local"));
    }

    [Fact]
    public void Validate_SameCertOnLaterContact_Accepts()
    {
        // Arrange: pin on first contact...
        var store = new InMemoryPinStore();
        var validator = new TofuCertificateValidator(store, dangerouslyAcceptAnyCert: false);
        using X509Certificate2 cert = TestCertificates.CreateSelfSigned();
        validator.Validate("host.local", cert);

        // Act: ...then present the SAME certificate again.
        bool accepted = validator.Validate("host.local", cert);

        // Assert
        Assert.True(accepted);
    }

    [Fact]
    public void Validate_ChangedCert_ThrowsPinMismatch_WithActionableMessage()
    {
        // Arrange: pin the first certificate.
        var store = new InMemoryPinStore();
        var validator = new TofuCertificateValidator(store, dangerouslyAcceptAnyCert: false);
        using X509Certificate2 first = TestCertificates.CreateSelfSigned();
        using X509Certificate2 second = TestCertificates.CreateSelfSigned();
        validator.Validate("host.local", first);

        // Act: a DIFFERENT certificate appears for the same host (the MITM signature).
        CertificatePinMismatchException ex = Assert.Throws<CertificatePinMismatchException>(
            () => validator.Validate("host.local", second));

        // Assert: refused, and the message names the pin file so the operator can re-pin.
        Assert.Equal("host.local", ex.Host);
        Assert.Equal(Sha256(first), ex.PinnedThumbprint);
        Assert.Equal(Sha256(second), ex.PresentedThumbprint);
        Assert.Contains(store.PinFilePath, ex.Message, StringComparison.Ordinal);
        // The pin must NOT have been silently overwritten with the rogue thumbprint.
        Assert.Equal(Sha256(first), store.GetPinned("host.local"));
    }

    [Fact]
    public void Validate_DangerousAcceptAnyCert_AcceptsWithoutPinning()
    {
        // Arrange: the dev escape hatch is on.
        var store = new InMemoryPinStore();
        var validator = new TofuCertificateValidator(store, dangerouslyAcceptAnyCert: true);
        using X509Certificate2 cert = TestCertificates.CreateSelfSigned();

        // Act
        bool accepted = validator.Validate("host.local", cert);

        // Assert: accepted, and deliberately NOT pinned (dev servers may regen certs each boot).
        Assert.True(accepted);
        Assert.Null(store.GetPinned("host.local"));
    }

    [Fact]
    public void Validate_DangerousAcceptAnyCert_AcceptsEvenADifferentCertThanPinned()
    {
        // Arrange: a pin exists, but the escape hatch is on — it must override the pin check
        // entirely (so a dev can flip the flag to recover from a self-inflicted cert change).
        var store = new InMemoryPinStore();
        using X509Certificate2 pinned = TestCertificates.CreateSelfSigned();
        store.Pin("host.local", Sha256(pinned));
        var validator = new TofuCertificateValidator(store, dangerouslyAcceptAnyCert: true);
        using X509Certificate2 different = TestCertificates.CreateSelfSigned();

        // Act
        bool accepted = validator.Validate("host.local", different);

        // Assert
        Assert.True(accepted);
    }

    [Fact]
    public void Validate_NullCertificate_Rejects()
    {
        // Arrange
        var store = new InMemoryPinStore();
        var validator = new TofuCertificateValidator(store, dangerouslyAcceptAnyCert: false);

        // Act
        bool accepted = validator.Validate("host.local", certificate: null);

        // Assert: no certificate means no basis for trust.
        Assert.False(accepted);
    }
}
