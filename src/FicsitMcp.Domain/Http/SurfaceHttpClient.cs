namespace FicsitMcp.Domain.Http;

/// <summary>
/// Thin shell wrapping a per-surface <see cref="HttpClient"/> (already configured with base
/// address and resilience by the host) that gives surface clients two things they should never
/// re-implement: a single place to send a request with a <see cref="CancellationToken"/>, and
/// uniform mapping of transport faults to the model-actionable <see cref="SurfaceUnreachableException"/>.
/// </summary>
/// <remarks>
/// This is the typed-client SHELL the infrastructure layer hands to the surface engineers
/// (<c>ficsit-server-api-client</c>, <c>frm-observe-surface</c>). They build their request/response
/// semantics ON TOP of this — they do not new up <see cref="HttpClient"/> and do not duplicate the
/// transport-error mapping. The resilience pipeline (timeout/retry) lives in the handler chain the
/// host wires onto the underlying client, so a call here is already retried/timed-out appropriately.
/// </remarks>
public sealed class SurfaceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _surfaceName;

    /// <summary>
    /// Wraps <paramref name="httpClient"/> for the surface named <paramref name="surfaceName"/>.
    /// </summary>
    public SurfaceHttpClient(HttpClient httpClient, string surfaceName)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceName);
        _httpClient = httpClient;
        _surfaceName = surfaceName;
    }

    /// <summary>The configured base address for this surface (host + port).</summary>
    public Uri BaseAddress =>
        _httpClient.BaseAddress
        ?? throw new InvalidOperationException(
            $"{_surfaceName} HttpClient has no BaseAddress; the surface was not configured before use.");

    /// <summary>
    /// Sends <paramref name="request"/> and returns the raw response, threading
    /// <paramref name="cancellationToken"/> all the way through. Transport faults (connection
    /// refused, DNS failure, timeout) are translated to <see cref="SurfaceUnreachableException"/>;
    /// a <see cref="CertificatePinMismatchException"/> from the TOFU validator is surfaced as-is.
    /// Callers own response-status handling (this does not call EnsureSuccessStatusCode).
    /// </summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

    /// <summary>
    /// Sends <paramref name="request"/> with an explicit <paramref name="completionOption"/> and
    /// returns the raw response, threading <paramref name="cancellationToken"/> all the way through.
    /// Pass <see cref="HttpCompletionOption.ResponseHeadersRead"/> for large/streamed bodies (e.g. a
    /// save-game download) so <see cref="HttpClient"/> returns as soon as the headers are available
    /// instead of buffering the whole body into memory first; the caller then streams
    /// <see cref="HttpContent"/> off the wire in chunks. Transport faults are mapped exactly as the
    /// default overload (connection refused/DNS/timeout → <see cref="SurfaceUnreachableException"/>,
    /// TOFU mismatch surfaced as-is); callers own response-status handling.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await _httpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A genuine caller cancellation must propagate unchanged, not be reported as the
            // server being unreachable. Only timeouts (cancellation we did NOT request) below
            // are treated as transport failures.
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // No external cancellation requested, so this is a resilience/HttpClient timeout:
            // the server is too slow or unresponsive. Map to the friendly unreachable error.
            throw new SurfaceUnreachableException(_surfaceName, BaseAddress, ex);
        }
        catch (HttpRequestException ex)
        {
            // The TOFU validator throws CertificatePinMismatchException from inside the TLS
            // handshake; HttpClient surfaces it as the inner exception of an HttpRequestException.
            // Unwrap and rethrow it so a changed certificate reports the actionable pin-mismatch
            // message rather than a generic "unreachable".
            if (ExtractPinMismatch(ex) is { } pinMismatch)
            {
                throw pinMismatch;
            }

            // Connection refused, host not found, TLS handshake failure, etc.
            throw new SurfaceUnreachableException(_surfaceName, BaseAddress, ex);
        }
    }

    private static CertificatePinMismatchException? ExtractPinMismatch(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is CertificatePinMismatchException pinMismatch)
            {
                return pinMismatch;
            }
        }

        return null;
    }
}
