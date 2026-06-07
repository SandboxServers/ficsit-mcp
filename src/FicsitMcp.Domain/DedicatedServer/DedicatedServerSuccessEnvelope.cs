using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// The success envelope the dedicated-server API returns: <c>{ "data": { ... } }</c>. Generic over
/// the typed payload so each function deserializes its own response <c>data</c> shape. Functions
/// that return 204 No Content have no envelope at all and are handled without this type.
/// </summary>
/// <typeparam name="TData">The function's response payload record.</typeparam>
public sealed record DedicatedServerSuccessEnvelope<TData>(
    [property: JsonPropertyName("data")] TData? Data);
