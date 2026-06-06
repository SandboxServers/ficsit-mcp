using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// An immutable item descriptor parsed from Docs.json (the <c>Desc_*_C</c> classes).
/// Items are parts, raw resources, fluids/gases, equipment, and the item-forms of
/// buildings — anything the game models as something that can sit in an inventory.
/// </summary>
/// <param name="ClassName">
/// The native class name, e.g. <c>Desc_IronPlate_C</c>. This is the stable key that
/// FRM payloads and recipes reference; resolution against it is case-insensitive.
/// </param>
/// <param name="DisplayName">Human-readable name, e.g. <c>Iron Plate</c>.</param>
/// <param name="Description">Flavour/description text, trimmed in the shipped snapshot.</param>
/// <param name="Form">Physical form (solid/liquid/gas).</param>
/// <param name="StackSize">Inventory stack size (1 for fluids/unstackable).</param>
/// <param name="SinkPoints">AWESOME Sink point value (0 if not sinkable).</param>
public sealed record GameItem(
    [property: JsonPropertyName("cn")] string ClassName,
    [property: JsonPropertyName("dn")] string DisplayName,
    [property: JsonPropertyName("desc")] string Description,
    [property: JsonPropertyName("form")] ItemForm Form,
    [property: JsonPropertyName("stack")] int StackSize,
    [property: JsonPropertyName("sink")] int SinkPoints);
