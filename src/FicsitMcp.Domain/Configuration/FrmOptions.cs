using System.ComponentModel.DataAnnotations;

namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Connection settings for Ficsit Remote Monitoring (FRM), the mod web server that exposes
/// live factory telemetry (power, production, logistics).
/// </summary>
/// <remarks>
/// Independently optional: HTTPS-API-only operation must work with FRM unconfigured, so leave
/// <see cref="BaseUrl"/> unset to keep this surface dormant. <see cref="TransportMode"/> lets
/// the FRM client choose between hitting the mod directly or routing through the dedicated-server
/// API (see <see cref="FrmTransportMode"/>).
/// </remarks>
public sealed class FrmOptions : IConfigurableSurface, IValidatableObject
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Frm";

    /// <inheritdoc />
    public static string SurfaceName => "FRM endpoint";

    /// <inheritdoc />
    public static string ActivatingEnvVar => $"FICSITMCP_{SectionName}__{nameof(BaseUrl)}";

    /// <summary>
    /// Base URL of the FRM mod web server, for example <c>http://127.0.0.1:8080</c>.
    /// Unset means this surface is not configured.
    /// </summary>
    [Url]
    public string? BaseUrl
    {
        get;
        // Blank means "unset" — see DedicatedServerOptions.BaseUrl for the rationale.
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// How to reach FRM. Defaults to <see cref="FrmTransportMode.Direct"/>; the passthrough
    /// mode reuses the dedicated-server API and therefore requires that surface to be configured.
    /// </summary>
    public FrmTransportMode TransportMode { get; set; } = FrmTransportMode.Direct;

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(TransportMode))
        {
            yield return new ValidationResult(
                $"Unknown FRM transport mode '{TransportMode}'; set FICSITMCP_{SectionName}__{nameof(TransportMode)} to Direct or DedicatedApiPassthrough.",
                [nameof(TransportMode)]);
        }
    }
}
