using FicsitMcp.Domain.FinBridge;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FicsitMcp.FinBridge;

/// <summary>
/// Maps the two FIN bridge endpoints (ADR-001 Decision 1) onto an endpoint route builder and
/// translates domain outcomes to HTTP. Kept thin: all behaviour lives in <see cref="IFinBridge"/>;
/// this layer only does transport (deserialize, dispatch, status mapping). Token auth runs upstream
/// in <see cref="FinTokenAuthMiddleware"/>, so a handler here only ever sees authenticated requests.
/// </summary>
internal static class FinBridgeEndpoints
{
    /// <summary>Registers <c>POST /fin/v1/hello</c> and <c>POST /fin/v1/poll</c>.</summary>
    public static void MapFinBridge(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost(FinProtocol.HelloPath, HandleHelloAsync);
        routes.MapPost(FinProtocol.PollPath, HandlePollAsync);
    }

    private static async Task<IResult> HandleHelloAsync(HttpContext context, IFinBridge bridge)
    {
        HelloRequest? hello = await ReadJsonAsync<HelloRequest>(context).ConfigureAwait(false);
        if (hello is null)
        {
            return InvalidBody("hello");
        }

        try
        {
            HelloResponse response = bridge.HelloAsync(hello);
            return Results.Ok(response);
        }
        catch (ProtocolVersionMismatchException ex)
        {
            return VersionMismatch(ex);
        }
    }

    private static async Task<IResult> HandlePollAsync(HttpContext context, IFinBridge bridge)
    {
        PollRequest? poll = await ReadJsonAsync<PollRequest>(context).ConfigureAwait(false);
        if (poll is null)
        {
            return InvalidBody("poll");
        }

        try
        {
            // The agent's HTTP request being aborted (it gave up the long-poll) cancels the hold.
            PollResponse response = await bridge.PollAsync(poll, context.RequestAborted).ConfigureAwait(false);
            return Results.Ok(response);
        }
        catch (ProtocolVersionMismatchException ex)
        {
            return VersionMismatch(ex);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The agent disconnected mid-hold; nothing to return. 499-style: let the connection drop.
            return Results.Empty;
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContext context)
        where T : class
    {
        try
        {
            return await context.Request.ReadFromJsonAsync<T>(context.RequestAborted).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static IResult InvalidBody(string envelope)
        => Results.BadRequest(new FinError
        {
            Code = FinErrorCode.InvalidArgs,
            Message = $"Request body is not a valid {envelope} envelope.",
        });

    private static IResult VersionMismatch(ProtocolVersionMismatchException ex)
        => Results.Json(
            new FinError
            {
                Code = FinErrorCode.ProtocolVersionMismatch,
                Message = ex.Message,
                Details = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    agentVersion = ex.AgentVersion,
                    serverSupportedMin = ex.ServerSupportedMin,
                    serverSupportedMax = ex.ServerSupportedMax,
                }),
            },
            statusCode: StatusCodes.Status426UpgradeRequired);
}
