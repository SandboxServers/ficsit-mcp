using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Frm.Model;
using FicsitMcp.Domain.Frm.Model.Raw;

namespace FicsitMcp.Tests.Frm;

/// <summary>
/// Pure-normalizer behaviour that does not need a transport round-trip: location precision trimming,
/// the rotation 360→0 wrap, and the null-location contract.
/// </summary>
public sealed class FrmNormalizerTests
{
    [Fact]
    public void ToLocation_NullRaw_ReturnsNull()
    {
        // FRM gave no location object → null, distinct from a real position at world origin.
        Assert.Null(FrmNormalizer.ToLocation(null));
    }

    [Fact]
    public void ToLocation_RotationRoundingUpTo360_WrapsToZero()
    {
        // 359.95 rounds to 360.0 at one decimal, but FRM normalizes headings to [0, 360); the wrap
        // must fold the boundary back to 0.0 so no normalized heading ever reads as 360.
        FrmLocation? location = FrmNormalizer.ToLocation(new RawLocation { Rotation = 359.95 });

        Assert.NotNull(location);
        Assert.Equal(0.0, location.RotationDegrees);
    }

    [Fact]
    public void ToLocation_RotationBelowBoundary_KeepsOneDecimal()
    {
        // A heading safely below the boundary is kept, rounded to one decimal — not wrapped.
        FrmLocation? location = FrmNormalizer.ToLocation(new RawLocation { Rotation = 359.94 });

        Assert.NotNull(location);
        Assert.Equal(359.9, location.RotationDegrees);
    }

    [Fact]
    public void ToLocation_Coordinates_RoundToWholeCentimetres()
    {
        FrmLocation? location = FrmNormalizer.ToLocation(
            new RawLocation { X = 125000.678, Y = -34000.25, Z = 12000.9, Rotation = 90.0 });

        Assert.NotNull(location);
        Assert.Equal(125001, location.X);
        Assert.Equal(-34000, location.Y);
        Assert.Equal(12001, location.Z);
    }
}
