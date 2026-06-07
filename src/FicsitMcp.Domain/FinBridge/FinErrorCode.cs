using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Typed error codes for FIN bridge failures, mirroring the <c>errorCode</c> enum in
/// <c>common.schema.json</c> so callers can branch programmatically rather than parsing message
/// text. Serialized as the exact SCREAMING_SNAKE_CASE strings the wire contract defines.
/// </summary>
/// <remarks>
/// New codes are a deliberate, versioned addition to the schema enum, not an ad-hoc extension.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<FinErrorCode>))]
public enum FinErrorCode
{
    /// <summary>The target agent has not sent a hello/poll within the liveness window.</summary>
    [JsonStringEnumMemberName("AGENT_OFFLINE")]
    AgentOffline,

    /// <summary>The agent's command queue is at its cap; the enqueue was rejected, not dropped.</summary>
    [JsonStringEnumMemberName("QUEUE_FULL")]
    QueueFull,

    /// <summary>
    /// Enqueued but the agent never pulled it before the deadline. Almost certainly did not execute.
    /// </summary>
    [JsonStringEnumMemberName("QUEUED_NOT_PICKED_UP")]
    QueuedNotPickedUp,

    /// <summary>
    /// Delivered in a poll response but no result arrived before the deadline. May have executed —
    /// treat as possibly-applied; do not blindly reissue.
    /// </summary>
    [JsonStringEnumMemberName("DELIVERED_NO_RESULT")]
    DeliveredNoResult,

    /// <summary>A single-target write resolved to more than one component.</summary>
    [JsonStringEnumMemberName("AMBIGUOUS_TARGET")]
    AmbiguousTarget,

    /// <summary>The addressed component could not be found.</summary>
    [JsonStringEnumMemberName("TARGET_NOT_FOUND")]
    TargetNotFound,

    /// <summary>The agent speaks a protocol version the server cannot serve (HTTP 426).</summary>
    [JsonStringEnumMemberName("PROTOCOL_VERSION_MISMATCH")]
    ProtocolVersionMismatch,

    /// <summary>The request did not carry a valid shared token (HTTP 401).</summary>
    [JsonStringEnumMemberName("UNAUTHORIZED")]
    Unauthorized,

    /// <summary>The operation arguments failed validation.</summary>
    [JsonStringEnumMemberName("INVALID_ARGS")]
    InvalidArgs,

    /// <summary>The operation ran but failed in-world.</summary>
    [JsonStringEnumMemberName("OPERATION_FAILED")]
    OperationFailed,
}
