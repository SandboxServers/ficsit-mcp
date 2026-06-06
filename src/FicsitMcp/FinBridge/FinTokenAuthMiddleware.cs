using System.Security.Cryptography;
using System.Text;

using FicsitMcp.Domain.FinBridge;

using Microsoft.AspNetCore.Http;

namespace FicsitMcp.FinBridge;

/// <summary>
/// Rejects any inbound bridge request that does not carry the shared token in the
/// <c>X-FIN-Token</c> header (ADR-001 Decision 4). The comparison is constant-time, and the
/// rejection happens with HTTP 401 <b>before any other work</b> — no queueing, no body parsing —
/// so an unauthenticated request can have no side effect.
/// </summary>
internal sealed class FinTokenAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[] _expectedTokenUtf8;

    /// <summary>Captures the expected token (UTF-8) once so each request avoids re-encoding it.</summary>
    public FinTokenAuthMiddleware(RequestDelegate next, string expectedToken)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        ArgumentException.ThrowIfNullOrEmpty(expectedToken);
        _expectedTokenUtf8 = Encoding.UTF8.GetBytes(expectedToken);
    }

    /// <summary>Validates the token, short-circuiting unauthenticated requests with 401.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(FinProtocol.TokenHeader, out var provided)
            || !IsValid(provided.ToString()))
        {
            await WriteUnauthorizedAsync(context).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool IsValid(string provided)
    {
        // FixedTimeEquals is length-revealing but not content-revealing; that is the standard,
        // accepted trade-off for a shared-token check.
        byte[] providedUtf8 = Encoding.UTF8.GetBytes(provided);
        return CryptographicOperations.FixedTimeEquals(providedUtf8, _expectedTokenUtf8);
    }

    private static Task WriteUnauthorizedAsync(HttpContext context)
        => FinHttpError.WriteAsync(context, new FinError
        {
            Code = FinErrorCode.Unauthorized,
            Message = $"Missing or invalid {FinProtocol.TokenHeader}.",
        });
}
