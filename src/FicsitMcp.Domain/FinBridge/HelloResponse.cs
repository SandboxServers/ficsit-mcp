using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The server's acceptance of a <see cref="HelloRequest"/>, mirroring the response branch of
/// <c>hello.schema.json</c>. Carries the timing contract so the agent need not hard-code it.
/// Refusals (e.g. protocol skew) are not this shape; they are typed error responses (HTTP 426).
/// </summary>
public sealed record HelloResponse
{
    /// <summary>The server's protocol version, so skew is detectable even on acceptance.</summary>
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    /// <summary>True when the server registered the agent and marked it alive.</summary>
    [JsonPropertyName("sessionAccepted")]
    public required bool SessionAccepted { get; init; }

    /// <summary>How long the server holds a long-poll open before returning (default 25000).</summary>
    [JsonPropertyName("serverHoldMs")]
    public required int ServerHoldMs { get; init; }

    /// <summary>Window within which a hello/poll must arrive for the agent to count as alive.</summary>
    [JsonPropertyName("agentLivenessMs")]
    public required int AgentLivenessMs { get; init; }
}
