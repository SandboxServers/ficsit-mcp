using FicsitMcp.Domain.FinBridge;

using Microsoft.AspNetCore.Http;

namespace FicsitMcp.FinBridge;

/// <summary>
/// The single place the bridge turns a <see cref="FinError"/> into an HTTP response: one bare-body
/// shape (the <see cref="FinError"/> object itself, matching <c>common.schema.json</c>'s
/// <c>errorObject</c> and ADR-001's examples — no <c>{ "error": { ... } }</c> wrapper) and one
/// code→status mapping. Centralized so the endpoints, the auth middleware, and future codes (#20/#21)
/// cannot drift into three subtly different error shapes or status mappings.
/// </summary>
internal static class FinHttpError
{
    /// <summary>Maps a <see cref="FinErrorCode"/> to the HTTP status it surfaces as.</summary>
    public static int StatusFor(FinErrorCode code) => code switch
    {
        FinErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        FinErrorCode.ProtocolVersionMismatch => StatusCodes.Status426UpgradeRequired,
        FinErrorCode.InvalidArgs => StatusCodes.Status400BadRequest,
        // The remaining codes are tool/transport-facing (they ride a result envelope, not an HTTP
        // status) but map to a sane default if ever returned as an HTTP body.
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>Builds an <see cref="IResult"/> that writes <paramref name="error"/> as a bare JSON body with its mapped status.</summary>
    public static IResult Result(FinError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Results.Json(error, statusCode: StatusFor(error.Code));
    }

    /// <summary>Writes <paramref name="error"/> as a bare JSON body with its mapped status directly to the response (for middleware that has no <see cref="IResult"/> pipeline).</summary>
    public static async Task WriteAsync(HttpContext context, FinError error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        context.Response.StatusCode = StatusFor(error.Code);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(error).ConfigureAwait(false);
    }
}
