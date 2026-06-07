using System.ComponentModel.DataAnnotations;

namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Connection settings for the official Satisfactory Dedicated Server HTTPS API
/// (<c>https://host:7777/api/v1</c>, authenticated with an admin API token).
/// </summary>
/// <remarks>
/// Independently optional: leave <see cref="BaseUrl"/> unset and the surface stays dormant
/// without affecting the others. The server uses a self-signed certificate by default, hence
/// the trust-on-first-use option here; certificate handling itself lives in the HTTP plumbing.
/// </remarks>
public sealed class DedicatedServerOptions : IConfigurableSurface, IValidatableObject
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "DedicatedServer";

    /// <inheritdoc />
    public static string SurfaceName => "Dedicated Server HTTPS API";

    /// <inheritdoc />
    public static string ActivatingEnvVar => $"FICSITMCP_{SectionName}__{nameof(BaseUrl)}";

    /// <summary>
    /// Base URL of the dedicated-server API, for example <c>https://127.0.0.1:7777</c>.
    /// Unset means this surface is not configured.
    /// </summary>
    [Url]
    public string? BaseUrl
    {
        get;
        // Blank ("" or whitespace — e.g. the shipped appsettings.json placeholder or an empty
        // env var) means "unset": normalize to null so [Url] (which accepts null but rejects "")
        // treats a deliberately blank value as a dormant surface instead of a startup failure.
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Admin API token used as the bearer credential. Redacted in logs and tool output via
    /// <see cref="Secret"/>.
    /// </summary>
    public Secret AdminToken { get; set; }

    /// <summary>
    /// DEV ONLY. When true, the server's self-signed TLS certificate is accepted without any
    /// trust-on-first-use thumbprint pinning. The deliberately alarming name makes its danger
    /// self-evident; never enable it against a server you do not fully control.
    /// </summary>
    public bool DangerousAcceptAnyCert { get; set; }

    /// <summary>
    /// Optional override for where the trust-on-first-use certificate pin file lives. When unset,
    /// the default is <c>%LocalAppData%/ficsit-mcp/cert-pins.json</c>. Containers and read-only
    /// deployments should point this at a mounted, writable path so pins survive restarts; the
    /// per-user default may not be writable (or persistent) in those environments.
    /// </summary>
    public string? CertPinFilePath { get; set; }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Intentionally no AdminToken-required rule. The dedicated-server surface supports a
        // "no token yet" mode: the client can run with BaseUrl set but no AdminToken so a caller can
        // perform the initial ClaimServer / PasswordLogin workflow (which MINTS the first token) over
        // this same TLS+pinned channel. The client enforces auth PER FUNCTION at call time —
        // unauthenticated functions (HealthCheck, login, claim) work tokenless; an authenticated
        // function with no token yet fails fast with an actionable DedicatedServerAuthException naming
        // the remedies. Requiring the token here would block that bootstrap entirely.
        //
        // NB: this differs from the FIN bridge, whose SharedSecret IS required when configured — an
        // inbound open listener with no secret is an exposed attack surface, whereas this is an
        // OUTBOUND client whose missing credential merely defers auth to a later mint step.
        return [];
    }
}
