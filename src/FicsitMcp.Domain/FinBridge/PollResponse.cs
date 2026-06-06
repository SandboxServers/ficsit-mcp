using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The body the server returns from a held <c>/fin/v1/poll</c> (server → agent), mirroring
/// <c>poll-response.schema.json</c>. Carries the commands to execute and the server's protocol
/// version (so skew is detectable even on a 200). <see cref="Commands"/> may be empty when the hold
/// expired with nothing queued.
/// </summary>
public sealed record PollResponse
{
    /// <summary>The server's protocol version.</summary>
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    /// <summary>
    /// Command envelopes for the agent to execute. Possibly empty. Each id is unique; the agent
    /// dedups on it for at-most-once execution.
    /// </summary>
    [JsonPropertyName("commands")]
    public IReadOnlyList<FinCommand> Commands { get; init; } = [];
}
