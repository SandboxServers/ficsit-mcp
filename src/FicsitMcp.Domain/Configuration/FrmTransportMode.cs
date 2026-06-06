namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// How the server reaches Ficsit Remote Monitoring (FRM). The #11 verification pass found FRM
/// is reachable two ways; modelling the choice as an enum now lets the FRM client pick a path
/// without a later options redesign.
/// </summary>
public enum FrmTransportMode
{
    /// <summary>
    /// Talk to the FRM mod's own web server directly over HTTP (default
    /// <c>http://host:8080</c>). The common case.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Reach FRM indirectly through the dedicated-server HTTPS API as a passthrough
    /// (<c>POST /api/v1</c> with function "frm"). Reuses the dedicated-server connection and
    /// its credentials instead of exposing the FRM port directly.
    /// </summary>
    DedicatedApiPassthrough = 1,
}
