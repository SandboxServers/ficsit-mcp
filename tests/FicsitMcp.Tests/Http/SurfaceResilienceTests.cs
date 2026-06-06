using System.Net;

using FicsitMcp.Configuration;
using FicsitMcp.Domain.Http;
using FicsitMcp.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FicsitMcp.Tests.Http;

/// <summary>
/// Exercises the REAL resilience pipeline wired by <see cref="SurfaceHttpClientRegistration"/>:
/// build the production registration, then swap each client's primary handler for a counting fake
/// (last ConfigurePrimaryHttpMessageHandler wins) so we observe how many attempts the pipeline made.
/// </summary>
public sealed class SurfaceResilienceTests
{
    // FRM is plain HTTP (no TLS/TOFU handler to fight), which makes it the clean surface for
    // observing retry behavior. The retry policy is identical across surfaces (shared pipeline).
    private static ServiceProvider BuildProvider(FakeHttpMessageHandler handler)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frm:BaseUrl"] = "http://127.0.0.1:8080",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSurfaceOptions(configuration);
        services.AddSurfaceHttpClients();

        // Replace the FRM client's primary handler with our counting fake (last registration wins).
        services.AddHttpClient(SurfaceHttpClients.Frm)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static HttpClient FrmClient(ServiceProvider provider) =>
        provider.GetRequiredService<IHttpClientFactory>().CreateClient(SurfaceHttpClients.Frm);

    [Fact]
    public async Task TransientFailure_OnIdempotentGet_IsRetried()
    {
        // Arrange: fail twice with a transient 503, then succeed. The retry strategy (max 3) must
        // recover, so the caller sees the eventual 200.
        var handler = FakeHttpMessageHandler.FailsThenSucceeds(failCount: 2);
        using ServiceProvider provider = BuildProvider(handler);
        HttpClient client = FrmClient(provider);

        // Act
        using HttpResponseMessage response = await client.GetAsync("/getFactory", CancellationToken.None);

        // Assert: succeeded, and it actually took multiple attempts (3 = 1 original + 2 retries).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.AttemptCount);
    }

    [Fact]
    public async Task NonIdempotentPost_IsNeverRetried_EvenOnTransientFailure()
    {
        // Arrange: every POST attempt returns a transient 503. A retry here would be a replayed
        // SaveGame/Shutdown/RunCommand — a real outage — so the pipeline must NOT retry POST.
        var handler = FakeHttpMessageHandler.AlwaysReturns(HttpStatusCode.ServiceUnavailable);
        using ServiceProvider provider = BuildProvider(handler);
        HttpClient client = FrmClient(provider);
        using var content = new StringContent("{}");

        // Act
        using HttpResponseMessage response = await client.PostAsync("/command", content, CancellationToken.None);

        // Assert: exactly ONE attempt — the request was sent once and never replayed.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.AttemptCount);
    }

    [Fact]
    public async Task NonTransientFailure_OnGet_IsNotRetried()
    {
        // Arrange: a 404 is not a transient fault, so even an idempotent GET must not be retried.
        var handler = FakeHttpMessageHandler.AlwaysReturns(HttpStatusCode.NotFound);
        using ServiceProvider provider = BuildProvider(handler);
        HttpClient client = FrmClient(provider);

        // Act
        using HttpResponseMessage response = await client.GetAsync("/missing", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, handler.AttemptCount);
    }
}
