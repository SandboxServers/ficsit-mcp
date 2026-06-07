using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The boot handshake the agent POSTs to <c>/fin/v1/hello</c> (agent → server), mirroring the
/// request branch of <c>hello.schema.json</c>. Announces identity and versions; the server accepts
/// (returning the timing contract) or refuses on protocol skew.
/// </summary>
public sealed record HelloRequest
{
    /// <summary>Integer wire-contract version. The server gates on it and refuses skew.</summary>
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    /// <summary>Stable id the agent chose for the computer running it. Scopes queue/liveness/events.</summary>
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    /// <summary>Semver of the in-world Lua agent script. Advisory.</summary>
    [JsonPropertyName("agentScriptVersion")]
    public required string AgentScriptVersion { get; init; }

    /// <summary>Optional capability tags. Advisory in protocol v1; reserved for negotiation at v2.</summary>
    [JsonPropertyName("capabilities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Capabilities { get; init; }
}
