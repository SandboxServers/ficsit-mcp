using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// An immutable production-relevant building parsed from Docs.json (the
/// <c>Build_*_C</c> classes that consume or produce power: manufacturers, extractors,
/// generators). Decorative/structural buildables are not modelled.
/// </summary>
/// <param name="ClassName">Native class name, e.g. <c>Build_ConstructorMk1_C</c>.</param>
/// <param name="DisplayName">Human-readable name, e.g. <c>Constructor</c>.</param>
/// <param name="Description">Building description, trimmed in the shipped snapshot.</param>
/// <param name="PowerConsumptionMw">
/// Base power draw in MW at 100% clock (0 for generators / passive buildings).
/// </param>
/// <param name="PowerProductionMw">
/// Base power output in MW (0 for consumers). Generators set this instead of consumption.
/// </param>
/// <param name="Clearance">
/// Footprint clearance box in centimetres, if present in the source data; otherwise null.
/// </param>
public sealed record GameBuilding(
    [property: JsonPropertyName("cn")] string ClassName,
    [property: JsonPropertyName("dn")] string DisplayName,
    [property: JsonPropertyName("desc")] string Description,
    [property: JsonPropertyName("pc")] double PowerConsumptionMw,
    [property: JsonPropertyName("pp")] double PowerProductionMw,
    [property: JsonPropertyName("clr")] ClearanceBox? Clearance);
