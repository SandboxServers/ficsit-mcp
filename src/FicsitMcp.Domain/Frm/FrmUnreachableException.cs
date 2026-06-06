namespace FicsitMcp.Domain.Frm;

/// <summary>
/// Thrown when an FRM read cannot reach the mod's web server: connection refused, timeout, a 404 on
/// a known endpoint such as <c>getPower</c> (mod present but the route is gone / disabled), or a non-JSON body (the port is
/// open but it is not FRM answering). The message is written for the model to ACT on — it names the
/// base URL and gives the exact in-game fix — rather than leaking a transport error or stack trace.
/// </summary>
/// <remarks>
/// This is FRM's own actionable error, distinct from the generic transport-layer
/// <c>SurfaceUnreachableException</c>: the remedy here is mod-specific (install the mod and start
/// its web server), which the generic "is it running?" message cannot convey.
/// </remarks>
public sealed class FrmUnreachableException : Exception
{
    /// <summary>The FRM base URL the read was attempted against.</summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// The bare endpoint name that failed (e.g. <c>getPower</c>, no leading slash), for diagnostics.
    /// This is the relative request path the client issues against the surface base address.
    /// </summary>
    public string Endpoint { get; }

    /// <summary>Builds the standard FRM-unreachable message naming the URL and the in-game remedy.</summary>
    public FrmUnreachableException(Uri baseAddress, string endpoint, string reason, Exception? innerException = null)
        : base(BuildMessage(baseAddress, reason), innerException)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        BaseAddress = baseAddress;
        Endpoint = endpoint;
    }

    private static string BuildMessage(Uri baseAddress, string reason)
    {
        string authority = baseAddress.GetLeftPart(UriPartial.Authority);
        return $"FRM not responding at {authority} ({reason}) — is the Ficsit Remote Monitoring mod " +
            "installed and its web server started? Run /frmweb start in-game, or enable autostart in the mod config.";
    }
}
