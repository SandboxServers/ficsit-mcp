using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// A single command the MCP server sends down to the agent inside a poll-response (server → agent),
/// mirroring <c>command.schema.json</c>. Correlated to its result only by the opaque
/// <see cref="Id"/>. Commands are never auto-retried; the agent dedups on id (at-most-once
/// execution).
/// </summary>
public sealed record FinCommand
{
    /// <summary>
    /// Server-generated unique opaque id (ULID/UUID). The sole correlation key between this command
    /// and its result; the agent keeps a recently-seen set keyed on this so a re-delivered command
    /// is acked but not re-executed.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>How the command names the component(s) it acts on.</summary>
    [JsonPropertyName("target")]
    public required FinTarget Target { get; init; }

    /// <summary>
    /// Name of the action to perform (e.g. discover, getState, setStandby, setPotential, setRecipe,
    /// execute). Open string so a new machine-control tool does not require a protocol bump.
    /// </summary>
    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    /// <summary>
    /// Operation-specific arguments, open by design. The one cross-cutting arg fixed by the contract
    /// is <c>allowMultiple</c>. Left as a raw JSON object so the bridge stays operation-agnostic
    /// (per-operation arg shapes are owned by the tool layer, issue #20).
    /// </summary>
    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Args { get; init; }

    /// <summary>RFC 3339 timestamp the server stamps when the command is enqueued. Telemetry only.</summary>
    [JsonPropertyName("issuedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>
    /// Relative deadline in milliseconds from delivery, after which the server stops waiting for
    /// this command's result and tombstones the id. The server's patience, distinct from the
    /// agent's own execution timeout.
    /// </summary>
    [JsonPropertyName("deadlineMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DeadlineMs { get; init; }
}
