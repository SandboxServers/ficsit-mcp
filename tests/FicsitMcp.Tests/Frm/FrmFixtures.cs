using System.Net;
using System.Net.Http.Headers;
using System.Text;

using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Http;

using Microsoft.Extensions.Logging.Abstractions;

namespace FicsitMcp.Tests.Frm;

/// <summary>
/// Shared helpers for the FRM client tests: loads the checked-in doc/source-derived fixture JSON and
/// builds an <see cref="FrmClient"/> whose transport returns a canned response, so deserialization
/// and normalization run against realistic FRM payload structure without a live mod.
/// </summary>
internal static class FrmFixtures
{
    /// <summary>The FRM base URL used by the test client (matches the message naming assertions).</summary>
    public static readonly Uri BaseAddress = new("http://127.0.0.1:8080");

    /// <summary>Absolute path to a fixture file under <c>Fixtures/Frm</c>.</summary>
    public static string Path(string fileName) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "Frm", fileName);

    /// <summary>Reads a fixture file's raw text.</summary>
    public static string Read(string fileName) => File.ReadAllText(Path(fileName));

    /// <summary>
    /// Builds an <see cref="FrmClient"/> whose every request returns <paramref name="status"/> with
    /// the given <paramref name="body"/> and content type. The handler's base address is the FRM URL.
    /// </summary>
    public static FrmClient ClientReturning(
        string body,
        HttpStatusCode status = HttpStatusCode.OK,
        string contentType = "application/json")
    {
        var handler = new StubHandler(body, status, contentType);
        var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var surface = new SurfaceHttpClient(http, SurfaceHttpClients.Frm);
        return new FrmClient(surface, NullLogger<FrmClient>.Instance);
    }

    /// <summary>Builds an <see cref="FrmClient"/> serving a named fixture file as a 200 JSON body.</summary>
    public static FrmClient ClientServing(string fixtureFileName) => ClientReturning(Read(fixtureFileName));

    /// <summary>
    /// Builds an <see cref="FrmClient"/> whose transport throws <paramref name="toThrow"/> — used to
    /// simulate the mod being absent (connection refused) or the web server not started (timeout).
    /// </summary>
    public static FrmClient ClientThrowing(Exception toThrow)
    {
        var handler = new ThrowingHandler(toThrow);
        var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var surface = new SurfaceHttpClient(http, SurfaceHttpClients.Frm);
        return new FrmClient(surface, NullLogger<FrmClient>.Instance);
    }

    private sealed class StubHandler(string body, HttpStatusCode status, string contentType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception toThrow) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => throw toThrow;
    }
}
