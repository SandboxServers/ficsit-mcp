using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.FinBridge;
using FicsitMcp.FinBridge;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FicsitMcp.Tests.FinBridge;

/// <summary>
/// End-to-end integration tests that start the REAL <see cref="FinBridgeHostedService"/> (a Kestrel
/// listener) on an ephemeral loopback port and drive it with a plain <see cref="HttpClient"/> acting
/// as the fake in-world agent — exactly the acceptance-criteria shape. Covers the full
/// hello → poll(command) → poll(result) round trip with <see cref="IFinBridge.SendAsync"/>, the
/// unauthenticated-rejection path, and protocol-version skew over the wire.
/// </summary>
public sealed class FinBridgeHostedServiceTests : IAsyncLifetime
{
    private const string Token = "integration-secret";
    private const string AgentId = "factory-floor-1";
    private const string ScriptVersion = "0.3.1";

    private FinBridgeHostedService _service = null!;
    private Domain.FinBridge.FinBridge _bridge = null!;
    private HttpClient _agent = null!;
    private string _baseUrl = null!;

    public async Task InitializeAsync()
    {
        int port = GetFreeTcpPort();
        _baseUrl = $"http://127.0.0.1:{port}";

        var options = new FinBridgeOptions
        {
            ListenUrl = _baseUrl,
            SharedSecret = Token,
            // Short hold so the empty-poll case in tests returns quickly; a queued command still
            // returns immediately regardless of the hold.
            ServerHoldMs = 1_000,
            AgentLivenessMs = 40_000,
        };

        _bridge = new Domain.FinBridge.FinBridge(
            Options.Create(options), TimeProvider.System, NullLogger<Domain.FinBridge.FinBridge>.Instance);

        _service = new FinBridgeHostedService(Options.Create(options), _bridge, NullLoggerFactory.Instance);
        await _service.StartAsync(CancellationToken.None);

        _agent = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _agent.DefaultRequestHeaders.Add(FinProtocol.TokenHeader, Token);
    }

    public async Task DisposeAsync()
    {
        _agent.Dispose();
        await _service.StopAsync(CancellationToken.None);
        _bridge.Dispose();
    }

    [Fact]
    public async Task FakeAgent_RoundTrips_Command_OverHttp()
    {
        // Boot handshake.
        HttpResponseMessage helloResponse = await _agent.PostAsJsonAsync(FinProtocol.HelloPath, new HelloRequest
        {
            ProtocolVersion = FinProtocol.Version,
            AgentId = AgentId,
            AgentScriptVersion = ScriptVersion,
        });
        helloResponse.EnsureSuccessStatusCode();
        HelloResponse? hello = await helloResponse.Content.ReadFromJsonAsync<HelloResponse>();
        Assert.NotNull(hello);
        Assert.True(hello.SessionAccepted);

        // A tool enqueues a command and awaits the result.
        Task<FinResult> send = _bridge.SendAsync(AgentId, new FinCommand
        {
            Id = "cmd-int-1",
            Target = FinTarget.Nick("iron-smelters"),
            Operation = "setStandby",
            DeadlineMs = 10_000,
        });

        // The fake agent long-polls and receives the command.
        PollResponse? delivered = await PollAsync();
        Assert.NotNull(delivered);
        Assert.Single(delivered.Commands);
        Assert.Equal("cmd-int-1", delivered.Commands[0].Id);

        // The agent executes and posts the result on the next poll, returning before+after state.
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            before = new { standby = true },
            after = new { standby = false },
        });
        await PollAsync(new FinResult { Id = "cmd-int-1", Ok = true, Payload = payload });

        FinResult result = await send.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Ok);
        Assert.Equal("cmd-int-1", result.Id);
    }

    [Fact]
    public async Task UnauthenticatedPost_IsRejected_With401_BeforeAnyWork()
    {
        using var anonymous = new HttpClient { BaseAddress = new Uri(_baseUrl) };

        HttpResponseMessage response = await anonymous.PostAsJsonAsync(FinProtocol.HelloPath, new HelloRequest
        {
            ProtocolVersion = FinProtocol.Version,
            AgentId = AgentId,
            AgentScriptVersion = ScriptVersion,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // The unauthenticated hello must not have registered the agent.
        Assert.False(_bridge.GetLiveness(AgentId).IsAlive);

        FinError? error = await response.Content.ReadFromJsonAsync<FinError>();
        Assert.NotNull(error);
        Assert.Equal(FinErrorCode.Unauthorized, error.Code);
    }

    [Fact]
    public async Task WrongToken_IsRejected_With401()
    {
        using var wrong = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        wrong.DefaultRequestHeaders.Add(FinProtocol.TokenHeader, "not-the-token");

        HttpResponseMessage response = await wrong.PostAsJsonAsync(FinProtocol.HelloPath, new HelloRequest
        {
            ProtocolVersion = FinProtocol.Version,
            AgentId = AgentId,
            AgentScriptVersion = ScriptVersion,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtocolVersionMismatch_Returns426_WithTypedBody()
    {
        HttpResponseMessage response = await _agent.PostAsJsonAsync(FinProtocol.HelloPath, new HelloRequest
        {
            ProtocolVersion = 999,
            AgentId = AgentId,
            AgentScriptVersion = ScriptVersion,
        });

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);

        FinError? error = await response.Content.ReadFromJsonAsync<FinError>();
        Assert.NotNull(error);
        Assert.Equal(FinErrorCode.ProtocolVersionMismatch, error.Code);
    }

    [Fact]
    public async Task DeadAgent_CommandFailsFast_WithActionableAgentOfflineError()
    {
        // No hello was sent, so the agent is offline; a tool's SendAsync must fail fast, not hang.
        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(
            () => _bridge.SendAsync("never-said-hello", new FinCommand
            {
                Id = "cmd-dead",
                Target = FinTarget.Nick("x"),
                Operation = "getState",
                DeadlineMs = 10_000,
            }));

        Assert.Equal(FinErrorCode.AgentOffline, ex.Code);
        Assert.Contains("agent script", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PollResponse?> PollAsync(params FinResult[] results)
    {
        HttpResponseMessage response = await _agent.PostAsJsonAsync(FinProtocol.PollPath, new PollRequest
        {
            ProtocolVersion = FinProtocol.Version,
            AgentId = AgentId,
            AgentScriptVersion = ScriptVersion,
            Results = results,
            Events = [],
            DroppedEvents = 0,
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PollResponse>();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
