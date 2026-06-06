namespace FicsitMcp.Domain.Http;

/// <summary>
/// Per-request resilience opt-ins carried on <see cref="System.Net.Http.HttpRequestMessage.Options"/>,
/// read by the host's resilience pipeline to decide whether a given request may be retried.
/// </summary>
/// <remarks>
/// <para>
/// The retry policy gates on HTTP-method safety <em>or</em> an explicit per-request opt-in. The
/// dedicated-server API is POST-only — a single endpoint with a function envelope — so retry
/// idempotency is a property of the <em>function</em>, not of the HTTP method. Method-only gating
/// (the old <c>DisableForUnsafeHttpMethods</c>) would therefore mean <em>nothing</em> on that
/// surface ever retries, defeating resilience for read-only functions.
/// </para>
/// <para>
/// Rules enforced by the pipeline:
/// <list type="bullet">
///   <item>Safe methods (GET/HEAD/OPTIONS/TRACE) retry transient faults by default.</item>
///   <item>Unsafe methods (POST, etc.) retry transient faults <em>only</em> when the request sets
///     <see cref="AllowRetry"/> to <c>true</c>.</item>
/// </list>
/// </para>
/// <para>
/// The dedicated-server client (#5) sets <see cref="AllowRetry"/> on idempotent functions
/// (<c>QueryServerState</c>, <c>HealthCheck</c>, <c>VerifyAuthenticationToken</c>) and never on
/// state-changing ones (<c>SaveGame</c>, <c>Shutdown</c>, <c>RunCommand</c>) — a replayed shutdown or
/// command is a real outage, so those must never be retried regardless of transient-fault status.
/// </para>
/// <example>
/// How a surface client opts an idempotent POST into retries:
/// <code>
/// using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1");
/// request.Content = JsonContent.Create(new { function = "QueryServerState" });
/// request.Options.Set(SurfaceHttpRequestOptions.AllowRetry, true);
/// </code>
/// </example>
/// </remarks>
public static class SurfaceHttpRequestOptions
{
    /// <summary>
    /// When set to <c>true</c> on a request's <see cref="System.Net.Http.HttpRequestMessage.Options"/>,
    /// allows the resilience pipeline to retry that request on transient faults even though its HTTP
    /// method is unsafe (e.g. POST). Absent or <c>false</c> leaves the default behaviour: only
    /// safe/idempotent methods are retried. Never set this on a non-idempotent function such as
    /// <c>SaveGame</c>/<c>Shutdown</c>/<c>RunCommand</c>.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> AllowRetry = new("FicsitMcp.AllowRetry");
}
