namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// A building a recipe can run in, resolved for presentation. Power is the building's
/// base draw at 100% clock; null when the building is not in the modelled set.
/// </summary>
/// <param name="ClassName">Building class name, e.g. <c>Build_ConstructorMk1_C</c>.</param>
/// <param name="DisplayName">Resolved display name, e.g. <c>Constructor</c>.</param>
/// <param name="PowerConsumptionMw">Base power draw in MW, if known.</param>
public sealed record ProducingBuilding(
    string ClassName,
    string DisplayName,
    double? PowerConsumptionMw);
