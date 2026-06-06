using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FicsitMcp.Tests.Http;

/// <summary>
/// Generates throwaway self-signed certificates in memory so TOFU pinning tests run against real
/// <see cref="X509Certificate2"/> thumbprints (the production code path), not hand-faked strings.
/// </summary>
internal static class TestCertificates
{
    /// <summary>
    /// Creates a fresh self-signed certificate. Each call yields a DISTINCT certificate (new key),
    /// so two calls produce two different thumbprints — exactly what the "certificate changed"
    /// reject test needs.
    /// </summary>
    public static X509Certificate2 CreateSelfSigned(string commonName = "ficsit-test")
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(1));
    }
}
