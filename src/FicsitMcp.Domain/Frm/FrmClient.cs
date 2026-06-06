using System.Collections.Immutable;
using System.Net;
using System.Text.Json;

using FicsitMcp.Domain.Frm.Model;
using FicsitMcp.Domain.Frm.Model.Raw;
using FicsitMcp.Domain.Http;

using Microsoft.Extensions.Logging;

namespace FicsitMcp.Domain.Frm;

/// <summary>
/// Default <see cref="IFrmClient"/>: fetches FRM GET endpoints over the injected
/// <see cref="SurfaceHttpClient"/> (whose base address and resilience the host wired), deserializes
/// with the source-generated <see cref="FrmJsonContext"/> (unknown-field-tolerant), and normalizes
/// to compact records via <see cref="FrmNormalizer"/>. Free of host/transport concerns beyond the
/// shell, so it stays unit-testable.
/// </summary>
/// <remarks>
/// Every degradation path logs once with structured fields (<c>surface</c>, <c>endpoint</c>,
/// <c>reason</c>) using message templates, then throws <see cref="FrmUnreachableException"/> with an
/// actionable in-game remedy — never a silent failure and never a leaked stack trace to the model.
/// </remarks>
public sealed class FrmClient : IFrmClient
{
    private const string SurfaceName = "FRM";

    private readonly SurfaceHttpClient _http;
    private readonly ILogger<FrmClient> _logger;

    /// <summary>Creates the client over a configured FRM <see cref="SurfaceHttpClient"/>.</summary>
    public FrmClient(SurfaceHttpClient http, ILogger<FrmClient> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ImmutableArray<FrmProdStatsItem>> GetProdStatsAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getProdStats",
            FrmJsonContext.Default.ImmutableArrayRawProdStatsItem,
            FrmNormalizer.ToProdStatsItem,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmFactoryBuilding>> GetFactoryAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getFactory",
            FrmJsonContext.Default.ImmutableArrayRawFactoryBuilding,
            FrmNormalizer.ToFactoryBuilding,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmPowerCircuit>> GetPowerAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getPower",
            FrmJsonContext.Default.ImmutableArrayRawPowerCircuit,
            FrmNormalizer.ToPowerCircuit,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmTrain>> GetTrainsAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getTrains",
            FrmJsonContext.Default.ImmutableArrayRawTrain,
            FrmNormalizer.ToTrain,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmDrone>> GetDronesAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getDrone",
            FrmJsonContext.Default.ImmutableArrayRawDrone,
            FrmNormalizer.ToDrone,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmVehicle>> GetVehiclesAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getVehicles",
            FrmJsonContext.Default.ImmutableArrayRawVehicle,
            FrmNormalizer.ToVehicle,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmPlayer>> GetPlayersAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getPlayer",
            FrmJsonContext.Default.ImmutableArrayRawPlayer,
            FrmNormalizer.ToPlayer,
            cancellationToken);

    /// <inheritdoc />
    public Task<ImmutableArray<FrmResourceNode>> GetResourceNodesAsync(CancellationToken cancellationToken) =>
        GetAsync(
            "getResourceNode",
            FrmJsonContext.Default.ImmutableArrayRawResourceNode,
            FrmNormalizer.ToResourceNode,
            cancellationToken);

    /// <summary>
    /// The single GET-fetch-deserialize-normalize path shared by every endpoint method: issues
    /// <c>GET /{endpoint}</c>, maps every failure mode (transport, bad status, non-JSON body, parse
    /// error) to a logged, actionable <see cref="FrmUnreachableException"/>, then projects each raw
    /// element through <paramref name="normalize"/>.
    /// </summary>
    /// <typeparam name="TRaw">The raw DTO element type for this endpoint.</typeparam>
    /// <typeparam name="TModel">The normalized record type returned to callers.</typeparam>
    private async Task<ImmutableArray<TModel>> GetAsync<TRaw, TModel>(
        string endpoint,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<ImmutableArray<TRaw>> typeInfo,
        Func<TRaw, TModel> normalize,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendAsync(endpoint, cancellationToken).ConfigureAwait(false);

        using (response)
        {
            EnsureFrmStatus(endpoint, response);

            ImmutableArray<TRaw> raw = await DeserializeAsync(endpoint, response, typeInfo, cancellationToken)
                .ConfigureAwait(false);

            if (raw.IsDefaultOrEmpty)
            {
                return [];
            }

            var builder = ImmutableArray.CreateBuilder<TModel>(raw.Length);
            foreach (TRaw element in raw)
            {
                builder.Add(normalize(element));
            }

            return builder.MoveToImmutable();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

        try
        {
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (SurfaceUnreachableException ex)
        {
            // The shell already mapped connection-refused/timeout to the generic transport error.
            // Re-map to FRM's actionable remedy (install mod + /frmweb start), preserving the cause.
            _logger.LogWarning(
                "Surface {surface} endpoint {endpoint} degraded: {reason}",
                SurfaceName, endpoint, "transport unreachable");
            throw new FrmUnreachableException(_http.BaseAddress, endpoint, "connection refused or timed out", ex);
        }
    }

    private void EnsureFrmStatus(string endpoint, HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // A 404 on a KNOWN endpoint means the port is answering but FRM is not serving this route —
        // wrong server on the port, a disabled/renamed endpoint, or a mod-version mismatch. Treat it
        // as FRM-unreachable with the actionable remedy rather than a bare HTTP error.
        string reason = response.StatusCode == HttpStatusCode.NotFound
            ? "endpoint not found (HTTP 404) — wrong server on the port, or a mod-version mismatch"
            : $"unexpected HTTP {(int)response.StatusCode} {response.StatusCode}";

        _logger.LogWarning(
            "Surface {surface} endpoint {endpoint} degraded: {reason}",
            SurfaceName, endpoint, reason);
        throw new FrmUnreachableException(_http.BaseAddress, endpoint, reason);
    }

    private async Task<ImmutableArray<TRaw>> DeserializeAsync<TRaw>(
        string endpoint,
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<ImmutableArray<TRaw>> typeInfo,
        CancellationToken cancellationToken)
    {
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await JsonSerializer.DeserializeAsync(body, typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            // The port is open and returned 2xx, but the body is not the JSON array FRM emits — most
            // likely something OTHER than FRM is listening on this port (a different web server, a
            // proxy error page). Surface the actionable FRM remedy, not a raw parse stack trace.
            _logger.LogWarning(
                "Surface {surface} endpoint {endpoint} degraded: {reason}",
                SurfaceName, endpoint, "response body was not valid FRM JSON");
            throw new FrmUnreachableException(
                _http.BaseAddress,
                endpoint,
                "response was not valid FRM JSON — is another server answering on this port?",
                ex);
        }
    }
}
