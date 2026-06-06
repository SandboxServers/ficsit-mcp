using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getFactory</c> array element. The two rate arrays use DIFFERENT field names in FRM:
/// <c>production</c> entries carry <c>CurrentProd</c>/<c>MaxProd</c>/<c>ProdPercent</c>, while
/// <c>ingredients</c> entries carry <c>CurrentConsumed</c>/<c>MaxConsumed</c>/<c>ConsPercent</c> —
/// hence two raw line types. Dropped raw fields (bounding box, colour slot, GeoJSON features, raw
/// inventories) are simply not declared and ignored on deserialization.
/// </summary>
public sealed record RawFactoryBuilding
{
    [JsonPropertyName("ID")]
    public string? Id { get; init; }

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("location")]
    public RawLocation? Location { get; init; }

    [JsonPropertyName("Recipe")]
    public string? Recipe { get; init; }

    [JsonPropertyName("RecipeClassName")]
    public string? RecipeClassName { get; init; }

    [JsonPropertyName("Productivity")]
    public double Productivity { get; init; }

    [JsonPropertyName("ManuSpeed")]
    public double ManuSpeed { get; init; }

    [JsonPropertyName("Somersloops")]
    public int Somersloops { get; init; }

    [JsonPropertyName("PowerShards")]
    public int PowerShards { get; init; }

    [JsonPropertyName("IsConfigured")]
    public bool IsConfigured { get; init; }

    [JsonPropertyName("IsProducing")]
    public bool IsProducing { get; init; }

    [JsonPropertyName("IsPaused")]
    public bool IsPaused { get; init; }

    [JsonPropertyName("PowerInfo")]
    public RawPowerInfo? PowerInfo { get; init; }

    [JsonPropertyName("production")]
    public ImmutableArray<RawProductionLine> Production { get; init; } = [];

    [JsonPropertyName("ingredients")]
    public ImmutableArray<RawIngredientLine> Ingredients { get; init; } = [];
}
