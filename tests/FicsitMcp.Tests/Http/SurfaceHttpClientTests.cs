using System.Net;

using FicsitMcp.Domain.Http;

namespace FicsitMcp.Tests.Http;

public sealed class SurfaceHttpClientTests
{
    private const string SurfaceName = "Test Surface";
    private static readonly Uri BaseAddress = new("https://127.0.0.1:7777");

    private static SurfaceHttpClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = BaseAddress }, SurfaceName);

    [Fact]
    public async Task SendAsync_ReturnsResponse_OnSuccess()
    {
        // Arrange
        var handler = FakeHttpMessageHandler.AlwaysReturns(HttpStatusCode.OK);
        SurfaceHttpClient client = CreateClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/state");

        // Act
        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_ConnectionRefused_ThrowsFriendlyUnreachable()
    {
        // Arrange: the transport throws as if the server were down.
        var handler = new FakeHttpMessageHandler((_, _, _) =>
            throw new HttpRequestException("Connection refused"));
        SurfaceHttpClient client = CreateClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/state");

        // Act
        SurfaceUnreachableException ex = await Assert.ThrowsAsync<SurfaceUnreachableException>(
            () => client.SendAsync(request, CancellationToken.None));

        // Assert: model-actionable message naming the surface, address, and likely cause.
        Assert.Equal(SurfaceName, ex.SurfaceName);
        Assert.Contains("Test Surface unreachable at https://127.0.0.1:7777", ex.Message, StringComparison.Ordinal);
        Assert.Contains("is it running?", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Timeout_ThrowsFriendlyUnreachable()
    {
        // Arrange: simulate a resilience/HttpClient timeout — a TaskCanceledException with no
        // external cancellation requested (the shape HttpClient throws on its own timeout).
        var handler = new FakeHttpMessageHandler((_, _, _) =>
            throw new TaskCanceledException("The request was canceled due to timeout."));
        SurfaceHttpClient client = CreateClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/state");

        // Act
        SurfaceUnreachableException ex = await Assert.ThrowsAsync<SurfaceUnreachableException>(
            () => client.SendAsync(request, CancellationToken.None));

        // Assert
        Assert.Contains("unreachable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_CallerCancellation_PropagatesAsCancellation_NotUnreachable()
    {
        // Arrange: a token that is already cancelled; a genuine caller cancellation must NOT be
        // disguised as the server being unreachable.
        var handler = new FakeHttpMessageHandler((_, _, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        SurfaceHttpClient client = CreateClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/state");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(request, cts.Token));
    }

    [Fact]
    public async Task SendAsync_PinMismatch_SurfacesAsPinMismatch_NotUnreachable()
    {
        // Arrange: the TOFU validator throws CertificatePinMismatchException from inside the TLS
        // handshake; HttpClient wraps it as the inner exception of an HttpRequestException. The
        // client must unwrap and rethrow the pin mismatch, not flatten it to "unreachable".
        var pinMismatch = new CertificatePinMismatchException(
            "host.local", "AAAA", "BBBB", "/tmp/cert-pins.json");
        var handler = new FakeHttpMessageHandler((_, _, _) =>
            throw new HttpRequestException("TLS handshake failed", pinMismatch));
        SurfaceHttpClient client = CreateClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/state");

        // Act
        CertificatePinMismatchException ex = await Assert.ThrowsAsync<CertificatePinMismatchException>(
            () => client.SendAsync(request, CancellationToken.None));

        // Assert
        Assert.Equal("host.local", ex.Host);
    }
}
