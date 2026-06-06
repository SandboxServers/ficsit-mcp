using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// An immutable production recipe parsed from Docs.json (the <c>Recipe_*_C</c> classes).
/// Carries the per-craft and per-minute amounts so callers never have to redo the
/// duration math; rates are computed once at parse time from <see cref="DurationSeconds"/>.
/// </summary>
/// <param name="ClassName">Native class name, e.g. <c>Recipe_IronPlate_C</c>.</param>
/// <param name="DisplayName">Human-readable name, e.g. <c>Iron Plate</c>.</param>
/// <param name="DurationSeconds">Craft time in seconds at 100% clock.</param>
/// <param name="Ingredients">Inputs consumed per craft, with per-minute rates.</param>
/// <param name="Products">Outputs produced per craft, with per-minute rates.</param>
/// <param name="ProducedInBuildingClassNames">
/// Automation buildings that can run this recipe (e.g. <c>Build_ConstructorMk1_C</c>).
/// Manual hand-craft sources (workbench, equipment workshop, build gun) are filtered
/// out at parse time; an empty list means the recipe is hand-craft-only.
/// </param>
/// <param name="IsAlternate">
/// True for Alternate recipes (class name prefixed <c>Recipe_Alternate</c>): unlocked
/// from Hard Drives rather than the milestone tree.
/// </param>
public sealed record GameRecipe(
    [property: JsonPropertyName("cn")] string ClassName,
    [property: JsonPropertyName("dn")] string DisplayName,
    [property: JsonPropertyName("dur")] double DurationSeconds,
    [property: JsonPropertyName("in")] ImmutableArray<RecipeItemAmount> Ingredients,
    [property: JsonPropertyName("out")] ImmutableArray<RecipeItemAmount> Products,
    [property: JsonPropertyName("bld")] ImmutableArray<string> ProducedInBuildingClassNames,
    [property: JsonPropertyName("alt")] bool IsAlternate);
