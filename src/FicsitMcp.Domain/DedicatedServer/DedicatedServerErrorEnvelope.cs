using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// The error envelope the dedicated-server API returns instead of a success payload:
/// <c>{ "errorCode": "...", "errorMessage": "...", "errorData": { ... } }</c>. Parsed by the client
/// and converted to a <see cref="DedicatedServerApiException"/>; never surfaced to callers directly.
/// </summary>
/// <remarks>
/// <c>errorData</c> is kept as a raw <see cref="JsonElement"/> because its shape varies per error
/// code; the client serializes it back to text for the exception rather than typing it.
/// </remarks>
public sealed record DedicatedServerErrorEnvelope(
    [property: JsonPropertyName("errorCode")] string? ErrorCode,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("errorData")] JsonElement? ErrorData);
