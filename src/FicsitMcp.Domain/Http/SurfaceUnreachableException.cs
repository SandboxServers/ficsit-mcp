namespace FicsitMcp.Domain.Http;

/// <summary>
/// Thrown when an outbound call to a configured surface fails at the transport level —
/// the host is down, the port is closed, DNS does not resolve, or the request times out.
/// The message is written for the model to act on (it names the surface and base address and
/// suggests the likely cause), rather than surfacing a raw <see cref="HttpRequestException"/>
/// or <see cref="TaskCanceledException"/> whose text ("Connection refused") gives a tool caller
/// nothing to do.
/// </summary>
public sealed class SurfaceUnreachableException : Exception
{
    /// <summary>Human-readable surface name, e.g. "Dedicated Server HTTPS API".</summary>
    public string SurfaceName { get; }

    /// <summary>The base address the call was attempted against.</summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// Builds the standard unreachable message, for example:
    /// "Dedicated Server HTTPS API unreachable at https://127.0.0.1:7777 — is it running?".
    /// </summary>
    public SurfaceUnreachableException(string surfaceName, Uri baseAddress, Exception innerException)
        : base(BuildMessage(surfaceName, baseAddress), innerException)
    {
        SurfaceName = surfaceName;
        BaseAddress = baseAddress;
    }

    private static string BuildMessage(string surfaceName, Uri baseAddress) =>
        $"{surfaceName} unreachable at {baseAddress.GetLeftPart(UriPartial.Authority)} — is it running?";
}
