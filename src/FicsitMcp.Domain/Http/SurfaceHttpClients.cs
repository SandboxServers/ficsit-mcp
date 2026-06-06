namespace FicsitMcp.Domain.Http;

/// <summary>
/// Canonical names of the per-surface typed/named <c>HttpClient</c>s registered with
/// <c>IHttpClientFactory</c>. Surface clients resolve their <c>HttpClient</c> by these names so a
/// rename can never silently desync the registration from a consumer.
/// </summary>
public static class SurfaceHttpClients
{
    /// <summary>The official Satisfactory Dedicated Server HTTPS API client (self-signed TLS, TOFU).</summary>
    public const string DedicatedServer = "DedicatedServer";

    /// <summary>The Ficsit Remote Monitoring (FRM) client (plain HTTP on the LAN).</summary>
    public const string Frm = "Frm";
}
