using System.Collections.Immutable;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// The synthesized answer for <c>lookup_recipe(item)</c>: the resolved item plus every
/// recipe that produces it and every recipe that consumes it, each with per-minute
/// rates and producing buildings. One call answers "what makes this and what uses it",
/// which raw data would otherwise require stitching together by hand.
/// </summary>
/// <param name="Item">The resolved item the query was about.</param>
/// <param name="ProducedBy">Recipes whose products include this item.</param>
/// <param name="ConsumedBy">Recipes whose ingredients include this item.</param>
public sealed record ItemRecipesResult(
    GameItem Item,
    ImmutableArray<RecipeView> ProducedBy,
    ImmutableArray<RecipeView> ConsumedBy);
