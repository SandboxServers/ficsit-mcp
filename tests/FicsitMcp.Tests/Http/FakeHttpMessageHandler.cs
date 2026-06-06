using System.Net;

namespace FicsitMcp.Tests.Http;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that records every request and returns a response
/// produced by a supplied delegate. The delegate receives the 1-based attempt number so a test can
/// fail the first N calls (to exercise retries) and succeed afterwards, or count how many times a
/// request was sent (to prove a non-idempotent call was NOT retried).
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;

    public FakeHttpMessageHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    /// <summary>Total number of times <see cref="SendAsync"/> was invoked.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Convenience factory: always return the given status code, counting attempts.</summary>
    public static FakeHttpMessageHandler AlwaysReturns(HttpStatusCode statusCode) =>
        new((_, _, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    /// <summary>
    /// Convenience factory: return <paramref name="transientStatus"/> for the first
    /// <paramref name="failCount"/> attempts, then <see cref="HttpStatusCode.OK"/>.
    /// </summary>
    public static FakeHttpMessageHandler FailsThenSucceeds(
        int failCount,
        HttpStatusCode transientStatus = HttpStatusCode.ServiceUnavailable) =>
        new((_, attempt, _) => Task.FromResult(
            new HttpResponseMessage(attempt <= failCount ? transientStatus : HttpStatusCode.OK)));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        AttemptCount++;
        return await _responder(request, AttemptCount, cancellationToken).ConfigureAwait(false);
    }
}
