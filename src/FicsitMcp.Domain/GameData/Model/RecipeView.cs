using System.Collections.Immutable;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// A fully-resolved, presentation-ready view of a recipe: display names and per-minute
/// rates for every ingredient and product, plus the buildings that can run it. This is
/// the single synthesized answer returned by <c>lookup_recipe</c> — the caller does not
/// have to join recipes to items to buildings itself.
/// </summary>
/// <param name="ClassName">Recipe class name, e.g. <c>Recipe_IronPlate_C</c>.</param>
/// <param name="DisplayName">Recipe display name, e.g. <c>Iron Plate</c>.</param>
/// <param name="DurationSeconds">Craft time in seconds at 100% clock.</param>
/// <param name="IsAlternate">Whether this is an Alternate (Hard Drive) recipe.</param>
/// <param name="Ingredients">Inputs with resolved names and rates.</param>
/// <param name="Products">Outputs with resolved names and rates.</param>
/// <param name="ProducedIn">Buildings that can run this recipe (empty = hand-craft only).</param>
public sealed record RecipeView(
    string ClassName,
    string DisplayName,
    double DurationSeconds,
    bool IsAlternate,
    ImmutableArray<RecipeRate> Ingredients,
    ImmutableArray<RecipeRate> Products,
    ImmutableArray<ProducingBuilding> ProducedIn);
