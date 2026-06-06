namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// A resolved ingredient/product line for presentation: the item's class name and
/// display name alongside its per-craft and per-minute amounts. Used in
/// <see cref="RecipeView"/> so callers get human-readable rates without a second lookup.
/// </summary>
/// <param name="ItemClassName">Item class name, e.g. <c>Desc_IronIngot_C</c>.</param>
/// <param name="ItemDisplayName">Resolved display name, e.g. <c>Iron Ingot</c>.</param>
/// <param name="AmountPerCraft">Amount per single craft (display units).</param>
/// <param name="AmountPerMinute">Throughput per minute at 100% clock.</param>
/// <param name="Form">Item form, so units (items vs m³) can be labelled.</param>
public sealed record RecipeRate(
    string ItemClassName,
    string ItemDisplayName,
    double AmountPerCraft,
    double AmountPerMinute,
    ItemForm Form);
