namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A single item's world-wide production/consumption line from FRM <c>/getProdStats</c>:
/// the current and theoretical-max production and consumption rates for one item class,
/// summed across the entire factory.
/// </summary>
/// <remarks>
/// FRM also emits a <c>ProdPerMin</c> DISPLAY STRING (e.g. <c>"P: 30/ min - C: 12/ min"</c>);
/// it is deliberately dropped here because the numeric truth is the four rate fields. The model
/// reasons about <see cref="CurrentProduced"/> vs <see cref="MaxProduced"/> directly.
/// </remarks>
/// <param name="ClassName">
/// Native item class, e.g. <c>Desc_IronPlate_C</c>. Returned as-is for the game-data layer to
/// resolve to a display name; not mapped here.
/// </param>
/// <param name="DisplayName">FRM-provided display name (best-effort; prefer class-name resolution).</param>
/// <param name="CurrentProduced">Current production rate (items/min, m³/min for fluids).</param>
/// <param name="MaxProduced">Theoretical-max production rate at full clock.</param>
/// <param name="CurrentConsumed">Current consumption rate.</param>
/// <param name="MaxConsumed">Theoretical-max consumption rate.</param>
/// <param name="ProducedPercent">Current/max production as a percentage (0&#8211;100+).</param>
/// <param name="ConsumedPercent">Current/max consumption as a percentage (0&#8211;100+).</param>
/// <param name="Form">Physical form string FRM reports (Solid/Liquid/Gas/Heat/…).</param>
public sealed record FrmProdStatsItem(
    string ClassName,
    string DisplayName,
    double CurrentProduced,
    double MaxProduced,
    double CurrentConsumed,
    double MaxConsumed,
    double ProducedPercent,
    double ConsumedPercent,
    string Form);
