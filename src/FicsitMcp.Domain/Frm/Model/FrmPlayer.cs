namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A player (pioneer) from FRM <c>/getPlayer</c>, normalized to position, health, and online state.
/// Personal inventory is dropped — the model rarely needs a per-player item dump, and including it
/// would bloat every players listing.
/// </summary>
/// <param name="Name">Player name.</param>
/// <param name="ClassName">Native player class.</param>
/// <param name="Location">Compact world position.</param>
/// <param name="SpeedKmh">Current movement speed in km/h.</param>
/// <param name="Online">Whether the player is currently online.</param>
/// <param name="Health">Current health points.</param>
/// <param name="Dead">Whether the player is dead.</param>
public sealed record FrmPlayer(
    string Name,
    string ClassName,
    FrmLocation Location,
    double SpeedKmh,
    bool Online,
    double Health,
    bool Dead);
