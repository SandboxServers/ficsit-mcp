using System.Collections.Immutable;
using System.Text.Json.Serialization;

using FicsitMcp.Domain.Frm.Model.Raw;

namespace FicsitMcp.Domain.Frm;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the raw FRM DTOs (AOT-friendly,
/// reflection-free), consistent with the .NET MCP SDK conventions. Every FRM read endpoint returns
/// a JSON ARRAY, so the registered roots are the array types.
/// </summary>
/// <remarks>
/// <para>
/// Deserialization is unknown-field-tolerant by construction: the raw DTOs declare only the fields
/// the normalizer consumes, and System.Text.Json ignores JSON members with no matching property, so
/// a mod update that ADDS fields cannot break parsing. <see cref="JsonSourceGenerationOptions"/>
/// also enables case-insensitive matching as a second line of defence against FRM's inconsistent
/// casing between endpoints.
/// </para>
/// <para>
/// Pairing case-insensitive matching with explicit <c>[JsonPropertyName]</c> on every raw DTO field
/// is DELIBERATE belt-and-suspenders, not redundancy to remove. The explicit names pin the contract
/// and document the exact FRM key; case-insensitivity is the drift shock-absorber for the fact that
/// FRM's casing is genuinely inconsistent and has flipped between mod versions (PascalCase object
/// fields, but lowercase <c>location</c>/<c>production</c>/<c>features</c>). The accepted cost is that
/// a casing-only typo in a <c>[JsonPropertyName]</c> would still bind rather than fail loudly; that
/// trade is intentional here because a routine game patch re-casing a field must NOT take the surface
/// down. The fixture-deserialization tests are what catch a genuinely wrong (not merely mis-cased)
/// property name.
/// </para>
/// <para>
/// Number handling allows reading numbers from JSON strings: FRM is built on Unreal's JSON writer
/// and has historically emitted some numerics as quoted strings; tolerating that here means a
/// quoting change in a future mod build degrades to a correct value rather than a parse failure.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ImmutableArray<RawProdStatsItem>))]
[JsonSerializable(typeof(ImmutableArray<RawFactoryBuilding>))]
[JsonSerializable(typeof(ImmutableArray<RawPowerCircuit>))]
[JsonSerializable(typeof(ImmutableArray<RawTrain>))]
[JsonSerializable(typeof(ImmutableArray<RawDrone>))]
[JsonSerializable(typeof(ImmutableArray<RawVehicle>))]
[JsonSerializable(typeof(ImmutableArray<RawPlayer>))]
[JsonSerializable(typeof(ImmutableArray<RawResourceNode>))]
public sealed partial class FrmJsonContext : JsonSerializerContext;
