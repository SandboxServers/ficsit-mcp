using System.Collections.Immutable;

namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A single production machine from FRM <c>/getFactory</c> (constructor, assembler, smelter,
/// refinery, manufacturer, …), normalized to the fields the model reasons about: what it makes,
/// how hard it is running, and whether it is actually producing.
/// </summary>
/// <remarks>
/// <para>
/// <c>/getFactory</c> is the heaviest FRM endpoint — a large save returns THOUSANDS of these.
/// The raw payload also carries per-building bounding boxes, colour slots, GeoJSON map
/// <c>features</c>, and full input/output inventories; all of that is dropped here. Callers must
/// filter (by area, class, or id) and cap/paginate before listing buildings — never return the
/// whole array to the model.
/// </para>
/// </remarks>
/// <param name="Id">Stable per-building id (FRM <c>ID</c>, the actor name).</param>
/// <param name="Name">Building display/tag name.</param>
/// <param name="ClassName">Native building class, e.g. <c>Build_ConstructorMk1_C</c>.</param>
/// <param name="Location">Compact world position, or <c>null</c> when FRM reported no location.</param>
/// <param name="Recipe">Current recipe display name, or empty when unconfigured.</param>
/// <param name="RecipeClassName">Current recipe class name.</param>
/// <param name="Productivity">Uptime/productivity percentage (0&#8211;100).</param>
/// <param name="ClockPercent">Overclock setting (<c>ManuSpeed</c>), percentage.</param>
/// <param name="Somersloops">Number of Somersloops slotted (production amplification).</param>
/// <param name="PowerShards">Number of Power Shards slotted (overclocking).</param>
/// <param name="IsConfigured">Whether a recipe is set.</param>
/// <param name="IsProducing">Whether the machine is actively producing right now.</param>
/// <param name="IsPaused">Whether production is manually paused.</param>
/// <param name="PowerConsumedMw">Live power draw, MW.</param>
/// <param name="FuseTriggered">Whether the machine's circuit fuse is blown (machine is unpowered).</param>
/// <param name="Production">Output item rate lines.</param>
/// <param name="Ingredients">Input item rate lines.</param>
public sealed record FrmFactoryBuilding(
    string Id,
    string Name,
    string ClassName,
    FrmLocation? Location,
    string Recipe,
    string RecipeClassName,
    double Productivity,
    double ClockPercent,
    int Somersloops,
    int PowerShards,
    bool IsConfigured,
    bool IsProducing,
    bool IsPaused,
    double PowerConsumedMw,
    bool FuseTriggered,
    ImmutableArray<FrmItemRate> Production,
    ImmutableArray<FrmItemRate> Ingredients);
