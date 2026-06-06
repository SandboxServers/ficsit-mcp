using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getProdStats</c> array element. Unknown-field-tolerant: only the fields the
/// normalizer consumes are declared; the dropped <c>ProdPerMin</c> display string and any
/// future siblings are ignored on deserialization.
/// </summary>
public sealed record RawProdStatsItem
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("CurrentProd")]
    public double CurrentProd { get; init; }

    [JsonPropertyName("MaxProd")]
    public double MaxProd { get; init; }

    [JsonPropertyName("CurrentConsumed")]
    public double CurrentConsumed { get; init; }

    [JsonPropertyName("MaxConsumed")]
    public double MaxConsumed { get; init; }

    [JsonPropertyName("ProdPercent")]
    public double ProdPercent { get; init; }

    [JsonPropertyName("ConsPercent")]
    public double ConsPercent { get; init; }

    [JsonPropertyName("Type")]
    public string? Type { get; init; }
}
