using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Reads a <see cref="Secret"/> from a JSON string and, crucially, writes it back redacted.
/// This guarantees that any code path that serializes an options object (a diagnostic dump,
/// a structured log of the config) emits "***" instead of the credential — the secret cannot
/// escape through the default serializer.
/// </summary>
public sealed class SecretJsonConverter : JsonConverter<Secret>
{
    /// <inheritdoc />
    public override Secret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Secret value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
