using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// The request envelope every call POSTs to the single <c>/api/v1</c> endpoint:
/// <c>{ "function": "...", "data": { ... } }</c>. The <c>data</c> payload is a pre-serialized
/// <see cref="JsonElement"/> so any typed request (or a raw mod payload) can be carried without this
/// type needing to know its shape; it is omitted entirely when null
/// (<see cref="JsonIgnoreCondition.WhenWritingNull"/>, set globally on the context).
/// </summary>
public sealed record DedicatedServerRequestEnvelope(
    [property: JsonPropertyName("function")] string Function,
    [property: JsonPropertyName("data")] JsonElement? Data);
