using System.Net;

using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Http;

namespace FicsitMcp.Tests.Frm;

/// <summary>
/// Negative-path tests: FRM unreachable / mod absent must surface an actionable
/// <see cref="FrmUnreachableException"/> naming the URL and the <c>/frmweb start</c> remedy, and
/// unknown JSON fields must be tolerated (the mod adds fields between versions).
/// </summary>
public sealed class FrmClientDegradationTests
{
    [Fact]
    public async Task GetPower_UnknownFields_AreIgnored()
    {
        // The fixtures embed extra fields not declared on the raw DTOs; deserialization must succeed.
        FrmClient client = FrmFixtures.ClientServing("getPower.json");

        var circuits = await client.GetPowerAsync(CancellationToken.None);

        Assert.Equal(2, circuits.Length);
    }

    [Fact]
    public async Task GetPower_ConnectionRefused_ThrowsActionableFrmError()
    {
        // The transport throwing HttpRequestException is how "mod not installed / port closed" looks;
        // the shell maps it to SurfaceUnreachableException, which the FRM client re-maps to its remedy.
        FrmClient client = FrmFixtures.ClientThrowing(new HttpRequestException("Connection refused"));

        FrmUnreachableException ex = await Assert.ThrowsAsync<FrmUnreachableException>(
            () => client.GetPowerAsync(CancellationToken.None));

        Assert.Contains("FRM not responding at http://127.0.0.1:8080", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/frmweb start", ex.Message, StringComparison.Ordinal);
        Assert.IsType<SurfaceUnreachableException>(ex.InnerException);
    }

    [Fact]
    public async Task GetTrains_Timeout_ThrowsActionableFrmError()
    {
        // A web server that was never started looks like a timeout (TaskCanceledException with no
        // external cancellation) once the resilience budget is exhausted.
        FrmClient client = FrmFixtures.ClientThrowing(
            new TaskCanceledException("The request was canceled due to timeout."));

        FrmUnreachableException ex = await Assert.ThrowsAsync<FrmUnreachableException>(
            () => client.GetTrainsAsync(CancellationToken.None));

        Assert.Contains("/frmweb start", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFactory_NonJsonBody_ThrowsActionableFrmError()
    {
        // Mod absent but something else (a different web server) answers 200 with HTML, not FRM JSON.
        FrmClient client = FrmFixtures.ClientReturning(
            FrmFixtures.Read("not-frm-200.html"),
            HttpStatusCode.OK,
            contentType: "text/html");

        FrmUnreachableException ex = await Assert.ThrowsAsync<FrmUnreachableException>(
            () => client.GetFactoryAsync(CancellationToken.None));

        Assert.Contains("not valid FRM JSON", ex.Message, StringComparison.Ordinal);
        Assert.Equal("getFactory", ex.Endpoint);
    }

    [Fact]
    public async Task GetPower_NotFoundOnKnownEndpoint_ThrowsActionableFrmError()
    {
        // A 404 on a route we know exists means the wrong server is on the port or a version mismatch.
        FrmClient client = FrmFixtures.ClientReturning(
            string.Empty,
            HttpStatusCode.NotFound,
            contentType: "text/plain");

        FrmUnreachableException ex = await Assert.ThrowsAsync<FrmUnreachableException>(
            () => client.GetPowerAsync(CancellationToken.None));

        Assert.Contains("HTTP 404", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/frmweb start", ex.Message, StringComparison.Ordinal);
    }
}
