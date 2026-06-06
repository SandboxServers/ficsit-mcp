using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// The canonical Satisfactory game-data lookup layer. Backed by an immutable snapshot
/// built once at startup (shipped asset or a user-supplied Docs.json override); every
/// lookup is an O(1) dictionary hit, never a linear scan.
/// </summary>
/// <remarks>
/// All name resolution accepts either the native class name (<c>Desc_IronPlate_C</c>,
/// <c>Recipe_IronPlate_C</c>, <c>Build_ConstructorMk1_C</c>) or the human display name
/// (<c>Iron Plate</c>, <c>Constructor</c>), case-insensitively. Unknown names throw
/// <see cref="UnknownGameDataNameException"/> with near-matches rather than returning null.
/// </remarks>
public interface IGameDataService
{
    /// <summary>Provenance of the loaded data (game build, source).</summary>
    GameDataMetadata Metadata { get; }

    /// <summary>
    /// Resolves an item by class name or display name. Throws
    /// <see cref="UnknownGameDataNameException"/> if no match exists.
    /// </summary>
    GameItem GetItem(string nameOrClass);

    /// <summary>
    /// Attempts to resolve an item; returns false instead of throwing when not found.
    /// </summary>
    bool TryGetItem(string nameOrClass, out GameItem item);

    /// <summary>
    /// Resolves a building by class name or display name. Throws
    /// <see cref="UnknownGameDataNameException"/> if no match exists.
    /// </summary>
    GameBuilding GetBuilding(string nameOrClass);

    /// <summary>
    /// Resolves a recipe by class name or display name. Throws
    /// <see cref="UnknownGameDataNameException"/> if no match exists.
    /// </summary>
    /// <remarks>
    /// Display names are not unique (e.g. an item and its primary recipe often share a
    /// name); when a display name maps to more than one recipe the lookup is ambiguous
    /// and throws with the candidates listed. Use the class name to disambiguate.
    /// </remarks>
    GameRecipe GetRecipe(string nameOrClass);

    /// <summary>
    /// Returns a fully-resolved view of a single recipe (display names + per-minute
    /// rates + producing buildings). Throws if the recipe cannot be resolved.
    /// </summary>
    RecipeView GetRecipeView(string nameOrClass);

    /// <summary>
    /// The headline synthesized lookup: for an item, every recipe that PRODUCES it and
    /// every recipe that CONSUMES it, each with per-minute rates and producing buildings.
    /// Throws <see cref="UnknownGameDataNameException"/> if the item cannot be resolved.
    /// </summary>
    ItemRecipesResult GetRecipesForItem(string itemNameOrClass);

    /// <summary>
    /// Validates that a recipe/building combination is legal vanilla data: the recipe
    /// exists and lists the building among its automation buildings. The clean entry
    /// point consumed by the FIN bridge before it sets a machine's recipe.
    /// </summary>
    /// <returns>
    /// A result describing whether the pairing is valid and, if not, why (with the
    /// recipe's actual valid buildings for correction).
    /// </returns>
    RecipeValidationResult ValidateRecipeForBuilding(string recipeNameOrClass, string buildingNameOrClass);
}
