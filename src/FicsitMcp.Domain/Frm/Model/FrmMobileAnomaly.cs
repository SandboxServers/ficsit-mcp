namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// Derived problem flags for a mobile/logistics entity (train, drone, vehicle), computed by the
/// client from FRM's raw status fields so a single SUMMARY call reveals trouble without the model
/// cross-referencing a raw dump. <c>[Flags]</c>: an entity can have several at once.
/// </summary>
/// <remarks>
/// These are DERIVED, not reported verbatim by FRM. The mapping from FRM's status enums/booleans
/// to these flags (and the false positives tuned out, e.g. a manually-driven idle vehicle is not
/// "stuck") lives in the normalization code and is documented there.
/// </remarks>
[Flags]
public enum FrmMobileAnomaly
{
    /// <summary>No problem detected.</summary>
    None = 0,

    /// <summary>The train has derailed (or has a pending collision) — fully stopped, needs manual recovery.</summary>
    Derailed = 1 << 0,

    /// <summary>Out of fuel — a fuelled vehicle that can no longer move.</summary>
    NoFuel = 1 << 1,

    /// <summary>Has fuel but not enough for a round trip — will strand mid-route.</summary>
    LowFuel = 1 << 2,

    /// <summary>Self-driving/autopilot is on but the entity has no valid path (cannot route).</summary>
    NoPath = 1 << 3,

    /// <summary>Self-driving but not moving and not docking — stuck/idle on its route.</summary>
    Stuck = 1 << 4,

    /// <summary>Self-driving reports an error state (the train's <c>SelfDriving</c> diagnostic is non-clear).</summary>
    SelfDrivingError = 1 << 5,

    /// <summary>The entity has no paired/target station configured, so it has nowhere to go.</summary>
    NoStation = 1 << 6,
}
