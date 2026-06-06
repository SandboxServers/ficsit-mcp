using System.Collections.Immutable;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// The outcome of validating a recipe/building pairing against vanilla data. Returned
/// (rather than thrown) so the FIN bridge can present a precise, correctable reason to
/// the model before attempting <c>fin_set_recipe</c>.
/// </summary>
/// <param name="IsValid">True only when the recipe exists and runs in the building.</param>
/// <param name="RecipeClassName">Resolved recipe class name, if the recipe existed.</param>
/// <param name="BuildingClassName">Resolved building class name, if the building existed.</param>
/// <param name="Reason">
/// Human-readable explanation. Empty when valid; otherwise states what was wrong
/// (unknown recipe, unknown building, or incompatible pairing).
/// </param>
/// <param name="ValidBuildingClassNames">
/// When the recipe exists, the buildings it can actually run in — so an incompatible
/// pairing can be corrected without a second query.
/// </param>
public sealed record RecipeValidationResult(
    bool IsValid,
    string? RecipeClassName,
    string? BuildingClassName,
    string Reason,
    ImmutableArray<string> ValidBuildingClassNames);
