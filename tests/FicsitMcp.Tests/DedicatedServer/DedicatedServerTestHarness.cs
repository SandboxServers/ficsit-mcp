using System.Net;
using System.Text;
using System.Text.Json;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.Http;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Test scaffolding for <see cref="DedicatedServerApiClient"/>: builds the client over a
/// <see cref="RecordingHandler"/> so tests can assert the outgoing envelope, headers, and the
/// per-request <see cref="SurfaceHttpRequestOptions.AllowRetry"/> opt-in, and can script responses
/// per attempt. The real resilience pipeline is bypassed (it has its own tests); these tests target
/// the protocol layer the client owns.
/// </summary>
internal static class DedicatedServerTestHarness
{
    public const string ConfigToken = "config-api-token";
    private static readonly Uri BaseAddress = new("https://127.0.0.1:7777");

    /// <summary>Builds a client with a config admin token (the preferred credential) seeded.</summary>
    public static (DedicatedServerApiClient Client, RecordingHandler Handler) CreateWithConfigToken(
        Func<CapturedRequest, int, HttpResponseMessage> responder)
    {
        var options = new DedicatedServerOptions
        {
            BaseUrl = BaseAddress.ToString(),
            AdminToken = ConfigToken,
        };
        return Create(options, responder);
    }

    /// <summary>Builds a client with NO config token (callers must authenticate explicitly).</summary>
    public static (DedicatedServerApiClient Client, RecordingHandler Handler) CreateWithoutToken(
        Func<CapturedRequest, int, HttpResponseMessage> responder)
    {
        var options = new DedicatedServerOptions { BaseUrl = BaseAddress.ToString() };
        return Create(options, responder);
    }

    private static (DedicatedServerApiClient, RecordingHandler) Create(
        DedicatedServerOptions options,
        Func<CapturedRequest, int, HttpResponseMessage> responder)
    {
        var handler = new RecordingHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = BaseAddress };
        var shell = new SurfaceHttpClient(http, DedicatedServerOptions.SurfaceName);
        return (new DedicatedServerApiClient(shell, options), handler);
    }

    /// <summary>A success envelope response: <c>{ "data": &lt;json&gt; }</c> with 200.</summary>
    public static HttpResponseMessage SuccessEnvelope(string dataJson) =>
        Json(HttpStatusCode.OK, $"{{\"data\":{dataJson}}}");

    /// <summary>A 204 No Content response (the shape for void functions).</summary>
    public static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    /// <summary>An error envelope response with the given code/message at the given status.</summary>
    public static HttpResponseMessage ErrorEnvelope(
        HttpStatusCode status,
        string errorCode,
        string errorMessage) =>
        Json(status, $"{{\"errorCode\":\"{errorCode}\",\"errorMessage\":\"{errorMessage}\"}}");

    /// <summary>A bare 401 with no body.</summary>
    public static HttpResponseMessage Unauthorized() => new(HttpStatusCode.Unauthorized);

    /// <summary>A raw binary (non-JSON) response, for the download path.</summary>
    public static HttpResponseMessage Binary(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        return response;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}

/// <summary>A request the <see cref="RecordingHandler"/> captured: method, URI, parsed body, headers.</summary>
internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? AuthorizationScheme,
    string? AuthorizationToken,
    bool AllowRetryOptionSet,
    string? ContentType,
    JsonDocument? JsonBody,
    string? RawBody)
{
    /// <summary>The <c>function</c> field from a JSON envelope body, if present.</summary>
    public string? Function =>
        JsonBody?.RootElement.ValueKind == JsonValueKind.Object
        && JsonBody.RootElement.TryGetProperty("function", out JsonElement fn)
            ? fn.GetString()
            : null;

    /// <summary>The <c>data</c> sub-object from a JSON envelope body, if present.</summary>
    public JsonElement? Data =>
        JsonBody?.RootElement.ValueKind == JsonValueKind.Object
        && JsonBody.RootElement.TryGetProperty("data", out JsonElement data)
            ? data
            : null;
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that captures each outgoing request (body buffered and parsed)
/// and returns a scripted response keyed by 1-based attempt number.
/// </summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<CapturedRequest, int, HttpResponseMessage> _responder;
    private readonly List<CapturedRequest> _requests = [];

    public RecordingHandler(Func<CapturedRequest, int, HttpResponseMessage> responder) =>
        _responder = responder;

    /// <summary>Every captured request, in send order.</summary>
    public IReadOnlyList<CapturedRequest> Requests => _requests;

    /// <summary>Number of times a request was sent.</summary>
    public int AttemptCount => _requests.Count;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? rawBody = null;
        JsonDocument? jsonBody = null;
        string? contentType = request.Content?.Headers.ContentType?.MediaType;

        if (request.Content is not null)
        {
            rawBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (contentType is "application/json" && !string.IsNullOrEmpty(rawBody))
            {
                try
                {
                    jsonBody = JsonDocument.Parse(rawBody);
                }
                catch (JsonException)
                {
                    jsonBody = null;
                }
            }
        }

        bool allowRetry =
            request.Options.TryGetValue(SurfaceHttpRequestOptions.AllowRetry, out bool value) && value;

        var captured = new CapturedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            allowRetry,
            contentType,
            jsonBody,
            rawBody);

        _requests.Add(captured);
        return _responder(captured, _requests.Count);
    }
}
