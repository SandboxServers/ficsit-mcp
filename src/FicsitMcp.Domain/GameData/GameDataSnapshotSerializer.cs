using System.Text.Json;
using System.Text.Json.Serialization;

using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Serializes and deserializes a <see cref="GameDataSnapshot"/> to/from the shipped
/// UTF-8 JSON asset. The on-disk shape mirrors the model records directly; enums are
/// written as strings so the snapshot is human-diffable in review.
/// </summary>
public static class GameDataSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            // Compact by default; the generator writes an indented copy via WriteIndented.
            WriteIndented = false,
            // Drop default values (0 power, false alternate, null clearance, empty
            // descriptions) to keep the shipped asset small. Deserialize tolerates the
            // omissions: missing numerics read back as 0, missing arrays as empty.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>Serializes a snapshot to compact UTF-8 JSON (the shipped form).</summary>
    public static string Serialize(GameDataSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    /// <summary>Serializes a snapshot to indented UTF-8 JSON (for readable diffs).</summary>
    public static string SerializeIndented(GameDataSnapshot snapshot) =>
        JsonSerializer.Serialize(
            snapshot,
            new JsonSerializerOptions(Options) { WriteIndented = true });

    /// <summary>
    /// Deserializes a snapshot from UTF-8 JSON. Throws <see cref="GameDataLoadException"/>
    /// (naming the source) when the JSON is malformed or yields a null snapshot.
    /// </summary>
    public static GameDataSnapshot Deserialize(string json, string source)
    {
        try
        {
            GameDataSnapshot? snapshot = JsonSerializer.Deserialize<GameDataSnapshot>(json, Options);
            return snapshot
                ?? throw new GameDataLoadException($"The game-data snapshot from '{source}' deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new GameDataLoadException(
                $"The game-data snapshot from '{source}' is not valid snapshot JSON: {ex.Message}", ex);
        }
    }
}
