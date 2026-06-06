using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw <c>production</c> array entry on a <c>/getFactory</c> building (an output item + its rate).
/// FRM names these fields <c>CurrentProd</c>/<c>MaxProd</c>/<c>ProdPercent</c>, distinct from the
/// ingredient-side names — see <see cref="RawIngredientLine"/>.
/// </summary>
public sealed record RawProductionLine
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("CurrentProd")]
    public double CurrentProd { get; init; }

    [JsonPropertyName("MaxProd")]
    public double MaxProd { get; init; }

    [JsonPropertyName("ProdPercent")]
    public double ProdPercent { get; init; }
}
