namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A resource node from FRM <c>/getResourceNode</c>: what resource it yields, its purity, and
/// whether it is already being exploited by an extractor.
/// </summary>
/// <remarks>
/// FRM serves this endpoint on the game thread (heavier than the pure-data reads). The model uses
/// it to discover unexploited deposits; <see cref="Exploited"/> is the key filter.
/// </remarks>
/// <param name="Name">Resource display name FRM reports (e.g. "Iron Ore").</param>
/// <param name="ClassName">Native resource class, e.g. <c>Desc_OreIron_C</c> (resolved elsewhere).</param>
/// <param name="Purity">Human purity string: Pure / Normal / Impure.</param>
/// <param name="ResourceForm">Physical form string (Solid/Liquid/Gas).</param>
/// <param name="NodeType">FRM node-type string (node / fracking core / geyser / …).</param>
/// <param name="Exploited">True when an extractor is already placed on this node.</param>
/// <param name="Location">Compact world position.</param>
public sealed record FrmResourceNode(
    string Name,
    string ClassName,
    string Purity,
    string ResourceForm,
    string NodeType,
    bool Exploited,
    FrmLocation Location);
