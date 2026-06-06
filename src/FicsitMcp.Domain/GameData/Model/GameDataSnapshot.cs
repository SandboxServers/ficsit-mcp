using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// The complete, immutable, parsed game-data set: every item, recipe, and
/// production building, plus provenance metadata. This is the unit that is serialized
/// to the shipped UTF-8 snapshot and the in-memory result of parsing a raw Docs.json.
/// </summary>
/// <param name="Metadata">Provenance: game build and where the data came from.</param>
/// <param name="Items">All parsed items.</param>
/// <param name="Recipes">All parsed recipes (vanilla + alternate).</param>
/// <param name="Buildings">All parsed production-relevant buildings.</param>
public sealed record GameDataSnapshot(
    [property: JsonPropertyName("meta")] GameDataMetadata Metadata,
    [property: JsonPropertyName("items")] ImmutableArray<GameItem> Items,
    [property: JsonPropertyName("recipes")] ImmutableArray<GameRecipe> Recipes,
    [property: JsonPropertyName("buildings")] ImmutableArray<GameBuilding> Buildings);
