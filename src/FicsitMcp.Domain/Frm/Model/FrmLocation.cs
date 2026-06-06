namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A compact world position normalized from FRM's verbose <c>location</c> object
/// (<c>{ x, y, z, rotation }</c>). FRM emits coordinates as world centimetres with
/// sub-millimetre precision (raw <c>long double</c>) and a rotation already normalized to
/// 0&#8211;360°; the model cannot reason about that precision, so we round coordinates to whole
/// centimetres and rotation to one decimal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate system</b> (Unreal Engine world space, as FRM emits it):
/// X, Y, Z are world <i>centimetres</i> (1 game metre = 100 units) — VERIFIED: FRM's
/// <c>getActorJSON</c> writes <c>GetActorLocation</c> components unscaled. The map origin is the
/// world centre; values run negative as well as positive.
/// </para>
/// <para>
/// <b>Rotation</b> is a single heading in degrees over the half-open range <c>[0, 360)</c>.
/// FRM derives it from the actor yaw and applies a <c>+450 mod 360</c> fold so the value reads as a
/// compass-style bearing — VERIFIED from source that the fold is applied and the range is
/// <c>[0, 360)</c>. The exact zero direction is documented by FRM as "due north" and we surface it as
/// such, but the precise world axis north maps to is ASSUMED (not re-derived against UE's axis
/// convention here); treat the heading as a stable relative bearing rather than a guaranteed
/// cardinal. Pitch and roll are not emitted.
/// </para>
/// </remarks>
/// <param name="X">World X in centimetres, rounded to whole cm.</param>
/// <param name="Y">World Y in centimetres, rounded to whole cm.</param>
/// <param name="Z">World Z in centimetres, rounded to whole cm.</param>
/// <param name="RotationDegrees">
/// Heading in degrees, 0&#8211;360, where 0 is due north (FRM has already applied its
/// east&#8594;north conversion). Rounded to one decimal.
/// </param>
public sealed record FrmLocation(long X, long Y, long Z, double RotationDegrees);
