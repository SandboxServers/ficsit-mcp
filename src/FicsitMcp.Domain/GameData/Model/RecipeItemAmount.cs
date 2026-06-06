using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// One ingredient or product line of a recipe: an item, the per-craft amount, and the
/// resulting throughput at 100% clock speed.
/// </summary>
/// <param name="ItemClassName">
/// The referenced item's class name, e.g. <c>Desc_IronIngot_C</c>. Resolve display
/// names via <see cref="IGameDataService"/>.
/// </param>
/// <param name="AmountPerCraft">
/// Amount consumed/produced per single craft, in display units (items for solids,
/// cubic metres for fluids/gases — already converted from the raw millilitre values).
/// </param>
/// <param name="AmountPerMinute">
/// Throughput at 100% clock: <c>AmountPerCraft × 60 / craftDurationSeconds</c>.
/// This is the canonical vanilla rate (e.g. 20 Iron Plate/min).
/// </param>
public sealed record RecipeItemAmount(
    [property: JsonPropertyName("c")] string ItemClassName,
    [property: JsonPropertyName("a")] double AmountPerCraft,
    [property: JsonPropertyName("m")] double AmountPerMinute);
