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
