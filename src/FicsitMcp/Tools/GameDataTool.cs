using System.ComponentModel;

using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

using ModelContextProtocol.Server;

namespace FicsitMcp.Tools;

/// <summary>
/// MCP tools over the canonical Satisfactory game-data layer. Thin: each method validates
/// its input, delegates to <see cref="IGameDataService"/>, and returns the synthesized
/// domain result. All data is read from a static, versioned snapshot — no live game.
/// </summary>
[McpServerToolType]
public sealed class GameDataTool
{
    /// <summary>
    /// Looks up every recipe that produces or consumes an item, with per-minute rates and
    /// the buildings that run each recipe.
    /// </summary>
    /// <remarks>
    /// Read-only, idempotent, closed-world: it reads a static snapshot of vanilla game data
    /// (no live game, no external systems), so the same input always yields the same output.
    /// </remarks>
    [McpServerTool(Name = "lookup_recipe", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Looks up all recipes that PRODUCE and all recipes that CONSUME a given Satisfactory item, " +
        "each with per-minute throughput at 100% clock and the building(s) that can run it. " +
        "Accepts the item's display name (e.g. \"Iron Plate\") or class name (e.g. \"Desc_IronPlate_C\"), " +
        "case-insensitively. Rates are computed from each recipe's craft duration (vanilla example: " +
        "the Iron Plate recipe is 3 Iron Ingot/craft -> 2 Iron Plate/craft over 6s = 30 Iron Ingot/min -> " +
        "20 Iron Plate/min). Fluid and gas amounts are reported in cubic metres per minute. " +
        "Data comes from a static, checked-in snapshot of vanilla game data (no live game). " +
        "If the name is unknown or ambiguous, the call fails with a message listing the closest matches " +
        "so you can retry with a corrected name.")]
    public static ItemRecipesResult LookupRecipe(
        IGameDataService gameData,
        [Description(
            "The item to look up, by display name (\"Iron Plate\") or class name (\"Desc_IronPlate_C\"). " +
            "Case-insensitive.")]
        string item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item);
        return gameData.GetRecipesForItem(item);
    }

    /// <summary>
    /// Looks up a single item's static properties (display name, form, stack size, sink points).
    /// </summary>
    /// <remarks>
    /// Read-only, idempotent, closed-world for the same reasons as <see cref="LookupRecipe"/>.
    /// </remarks>
    [McpServerTool(Name = "lookup_item", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Looks up a single Satisfactory item's static properties: display name, class name, physical " +
        "form (solid/liquid/gas), inventory stack size, AWESOME Sink point value, and description. " +
        "Accepts the item's display name (e.g. \"Iron Plate\") or class name (e.g. \"Desc_IronPlate_C\"), " +
        "case-insensitively. To find what produces or consumes the item, use lookup_recipe instead. " +
        "Data comes from a static, checked-in snapshot of vanilla game data (no live game). " +
        "If the name is unknown, the call fails with a message listing the closest matches.")]
    public static GameItem LookupItem(
        IGameDataService gameData,
        [Description(
            "The item to look up, by display name (\"Iron Plate\") or class name (\"Desc_IronPlate_C\"). " +
            "Case-insensitive.")]
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return gameData.GetItem(name);
    }
}
