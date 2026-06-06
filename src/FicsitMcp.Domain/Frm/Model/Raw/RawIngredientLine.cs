using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw <c>ingredients</c> array entry on a <c>/getFactory</c> building (an input item + its rate).
/// FRM names these fields <c>CurrentConsumed</c>/<c>MaxConsumed</c>/<c>ConsPercent</c>, distinct
/// from the production-side names — see <see cref="RawProductionLine"/>.
/// </summary>
public sealed record RawIngredientLine
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("CurrentConsumed")]
    public double CurrentConsumed { get; init; }

    [JsonPropertyName("MaxConsumed")]
    public double MaxConsumed { get; init; }

    [JsonPropertyName("ConsPercent")]
    public double ConsPercent { get; init; }
}
