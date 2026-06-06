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

    /// <summary>
    /// How long the server holds an agent long-poll open before returning an empty command list
    /// (ADR-001 <c>serverHoldMs</c>, default 25000). Must be shorter than the agent's InternetCard
    /// request timeout; the hold is what lets a healthy agent idle with near-zero request volume.
    /// </summary>
    [Range(1, 600_000)]
    public int ServerHoldMs { get; set; } = 25_000;

    /// <summary>
    /// Window within which a hello or poll must arrive for the agent to count as alive (ADR-001
    /// <c>agentLivenessMs</c>, default 40000 ≈ 1.5× <see cref="ServerHoldMs"/>). A command enqueued
    /// against an agent that has gone silent past this window fails fast with <c>AGENT_OFFLINE</c>
    /// instead of hanging to the command deadline.
    /// </summary>
    [Range(1, 600_000)]
    public int AgentLivenessMs { get; set; } = 40_000;

    /// <summary>
    /// Per-agent command queue cap (ADR-001 <c>maxQueuedCommands</c>, default 64). Enqueue beyond
    /// this fails fast with <c>QUEUE_FULL</c>: a mutation is never silently dropped, so the queue
    /// rejects rather than evicting.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxQueuedCommands { get; set; } = 64;

    /// <summary>
    /// Server-side bounded event ring per agent (ADR-001 <c>maxBufferedEvents</c>, default 256),
    /// drop-oldest on overflow. Events are observational telemetry, so losing the oldest under a
    /// signal storm is acceptable; the server reports the running dropped count.
    /// </summary>
    [Range(1, 1_000_000)]
    public int MaxBufferedEvents { get; set; } = 256;

    /// <summary>
    /// Default server-side patience for a command's result when the caller does not specify one,
    /// in milliseconds (default 8000). After this elapses the command's awaiter is completed as a
    /// timeout and its id is tombstoned, so a late result is discarded rather than re-applied.
    /// </summary>
    [Range(1, 600_000)]
    public int DefaultCommandDeadlineMs { get; set; } = 8_000;

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

        // The liveness window must outlast a single long-poll hold; otherwise an agent parked on a
        // perfectly healthy long-poll would be declared offline mid-hold. ADR-001 fixes the ratio
        // at ~1.5x, so enforce at least "strictly greater" here.
        if (AgentLivenessMs <= ServerHoldMs)
        {
            yield return new ValidationResult(
                $"{nameof(AgentLivenessMs)} ({AgentLivenessMs}) must be greater than {nameof(ServerHoldMs)} " +
                $"({ServerHoldMs}); otherwise a healthy agent parked on a long-poll would be marked offline.",
                [nameof(AgentLivenessMs), nameof(ServerHoldMs)]);
        }
    }
}
