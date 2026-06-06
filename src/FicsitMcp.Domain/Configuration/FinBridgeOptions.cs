using System.ComponentModel.DataAnnotations;

namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Settings for the FicsIt-Networks (FIN) bridge: the listener the in-world Lua agent connects
/// to so the model can observe and control machines from inside the game.
/// </summary>
/// <remarks>
/// Independently optional: leave <see cref="ListenUrl"/> unset to keep the bridge off. The
/// <see cref="SharedSecret"/> authenticates the Lua agent and is redacted in logs and tool output.
/// </remarks>
public sealed class FinBridgeOptions : IConfigurableSurface, IValidatableObject
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "FinBridge";

    /// <inheritdoc />
    public static string SurfaceName => "FIN bridge";

    /// <inheritdoc />
    public static string ActivatingEnvVar => $"FICSITMCP_{SectionName}__{nameof(ListenUrl)}";

    /// <summary>
    /// URL the bridge listener binds, for example <c>http://0.0.0.0:8421</c>. Unset means the
    /// bridge is not configured.
    /// </summary>
    [Url]
    public string? ListenUrl
    {
        get;
        // Blank means "unset" — see DedicatedServerOptions.BaseUrl for the rationale.
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Shared secret the in-world Lua agent must present to connect. Redacted in logs and tool
    /// output via <see cref="Secret"/>.
    /// </summary>
    public Secret SharedSecret { get; set; }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ListenUrl);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // An open listener with no shared secret lets anything on the network drive machines,
        // so require the secret once the bridge is active (but never when it is dormant).
        if (IsConfigured && !SharedSecret.HasValue)
        {
            yield return new ValidationResult(
                $"{SurfaceName} is configured but has no shared secret; set FICSITMCP_{SectionName}__{nameof(SharedSecret)}.",
                [nameof(SharedSecret)]);
        }
    }
}
