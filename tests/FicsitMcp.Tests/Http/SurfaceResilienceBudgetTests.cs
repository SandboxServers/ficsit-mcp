using System.Net.Http;

using FicsitMcp.Configuration;
using FicsitMcp.Domain.Http;
using FicsitMcp.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using Polly.Timeout;

namespace FicsitMcp.Tests.Http;

/// <summary>
/// Verifies the resilience pipeline's TOTAL timeout budget (10s, the outermost strategy) using a
/// <see cref="FakeTimeProvider"/> threaded through the pipeline by
/// <see cref="SurfaceHttpClientRegistration"/>. A handler that never completes lets us advance
/// virtual time past the budget and observe the timeout fire at ~zero wall-clock — no real waiting.
/// </summary>
public sealed class SurfaceResilienceBudgetTests
{
    // Registers a FicsitMcp FakeTimeProvider BEFORE AddSurfaceHttpClients so its TryAddSingleton
    // keeps the fake (rather than registering TimeProvider.System), then swaps the FRM client's
    // primary handler for one that hangs until cancelled.
    private static ServiceProvider BuildProvider(
        FakeTimeProvider fakeTime,
        HttpMessageHandler handler)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frm:BaseUrl"] = "http://127.0.0.1:8080",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddSurfaceOptions(configuration);
        services.AddSurfaceHttpClients();

        services.AddHttpClient(SurfaceHttpClients.Frm)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TotalBudget_FiresTimeout_WhenServerNeverResponds()
    {
        // Arrange: a handler that never completes (only finishes if its request is cancelled), so the
        // only way out is a resilience timeout. The pipeline runs on fake time, so advancing the
        // clock past the 10s total budget triggers the timeout immediately in real wall-clock.
        var fakeTime = new FakeTimeProvider();
        using var handler = new NeverCompletesHandler();
        using ServiceProvider provider = BuildProvider(fakeTime, handler);
        HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(SurfaceHttpClients.Frm);

        // The HttpClient itself also has a (real-time) timeout; disable it so the ONLY deadline is
        // the resilience total budget driven by the fake clock.
        client.Timeout = Timeout.InfiniteTimeSpan;

        // Act: start the call (it parks inside the never-completing handler under the pipeline)...
        Task<HttpResponseMessage> call = client.GetAsync("/getFactory", CancellationToken.None);

        // ...then push virtual time just past the 10s total budget. A short pump loop lets the
        // pipeline's timeout task observe each advance without any real sleeping.
        for (int i = 0; i < 11 && !call.IsCompleted; i++)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        // Assert: the total-budget timeout rejected the call. (Through raw HttpClient the pipeline
        // surfaces Polly's TimeoutRejectedException; the friendly SurfaceUnreachableException mapping
        // lives in SurfaceHttpClient, which this test deliberately bypasses to assert the pipeline.)
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => call);
    }

    // A handler that returns a task which only completes when the request's CancellationToken fires
    // (i.e. when the resilience timeout cancels the attempt). It models a server that accepted the
    // connection but never replies.
    private sealed class NeverCompletesHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
