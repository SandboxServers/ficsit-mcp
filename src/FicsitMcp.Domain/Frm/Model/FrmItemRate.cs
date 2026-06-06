namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// One production-output or ingredient-input line on a factory machine: an item and its
/// current vs theoretical-max rate. Used for both the <c>production</c> and <c>ingredients</c>
/// arrays FRM emits on each <c>/getFactory</c> building (the two arrays carry different rate
/// field names in FRM; both are normalized to this single shape).
/// </summary>
/// <param name="ClassName">Native item class, e.g. <c>Desc_IronPlate_C</c> (resolved elsewhere).</param>
/// <param name="DisplayName">FRM-provided display name.</param>
/// <param name="CurrentRate">Current throughput, items/min (m³/min for fluids).</param>
/// <param name="MaxRate">Theoretical-max throughput at this machine's clock.</param>
/// <param name="Percent">Current/max as a percentage (0&#8211;100).</param>
public sealed record FrmItemRate(
    string ClassName,
    string DisplayName,
    double CurrentRate,
    double MaxRate,
    double Percent);
