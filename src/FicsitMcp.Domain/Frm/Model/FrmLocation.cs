namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A compact world position normalized from FRM's verbose <c>location</c> object
/// (<c>{ x, y, z, rotation }</c>). FRM emits coordinates as world centimetres with
/// sub-millimetre precision (raw <c>long double</c>) and a rotation already normalized to
/// 0&#8211;360° with 0 = due north; the model cannot reason about that precision, so we round
/// coordinates to whole centimetres and rotation to one decimal.
/// </summary>
/// <param name="X">World X in centimetres, rounded to whole cm.</param>
/// <param name="Y">World Y in centimetres, rounded to whole cm.</param>
/// <param name="Z">World Z in centimetres, rounded to whole cm.</param>
/// <param name="RotationDegrees">
/// Heading in degrees, 0&#8211;360, where 0 is due north (FRM has already applied its
/// east&#8594;north conversion). Rounded to one decimal.
/// </param>
public sealed record FrmLocation(long X, long Y, long Z, double RotationDegrees);
