using System.Collections.Immutable;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.FinBridge;
using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Frm.Model;
using FicsitMcp.Domain.Http;
using FicsitMcp.Domain.ServerObservation;

namespace FicsitMcp.Tests.ServerObservation;

/// <summary>
/// Unit tests for <see cref="ServerObservationService"/> — the logic behind the get_server_state /
/// health_check / verify_connection tools — over hand-rolled fakes for the three surface clients.
/// AAA style. The verify_connection aggregation is exercised per surface, including the unconfigured
/// path, the reachable path, and the unreachable (probe-throws) path.
/// </summary>
public sealed class ServerObservationServiceTests
{
    private const string BaseUrl = "https://127.0.0.1:7777";
    private const string FrmUrl = "http://127.0.0.1:8080";
    private const string FinUrl = "http://0.0.0.0:8421";

    // ---- get_server_state -----------------------------------------------------------------------

    [Fact]
    public async Task GetServerState_DistilsTheApiPayloadIntoTheSummary()
    {
        // Arrange
        var state = new ServerGameState(
            ActiveSessionName: "MyFactory",
            NumConnectedPlayers: 3,
            PlayerLimit: 4,
            TechTier: 7,
            ActiveSchematic: "Schematic_X",
            GamePhase: "Phase_4",
            IsGameRunning: true,
            TotalGameDuration: 123456,
            IsGamePaused: false,
            AverageTickRate: 29.5,
            AutoLoadSessionName: "MyFactory");
        var dedicated = new FakeDedicatedServerApiClient
        {
            StateResponse = new QueryServerStateResponse(state),
        };
        ServerObservationService service = CreateService(dedicated);

        // Act
        ServerStateSummary summary = await service.GetServerStateAsync();

        // Assert
        Assert.Equal("MyFactory", summary.ActiveSessionName);
        Assert.Equal(3, summary.ConnectedPlayers);
        Assert.Equal(4, summary.PlayerLimit);
        Assert.Equal(7, summary.TechTier);
        Assert.Equal("Phase_4", summary.GamePhase);
        Assert.True(summary.IsGameRunning);
        Assert.False(summary.IsGamePaused);
        Assert.Equal(29.5, summary.AverageTickRate);
        Assert.Equal(123456, summary.TotalGameDurationSeconds);
    }

    [Fact]
    public async Task GetServerState_PropagatesTheClientsTypedException()
    {
        // Arrange — an auth failure from the client must surface, not be swallowed.
        var dedicated = new FakeDedicatedServerApiClient
        {
            StateException = new DedicatedServerAuthException("invalid_token", "Token rejected."),
        };
        ServerObservationService service = CreateService(dedicated);

        // Act + Assert
        await Assert.ThrowsAsync<DedicatedServerAuthException>(() => service.GetServerStateAsync());
    }

    // ---- health_check ---------------------------------------------------------------------------

    [Theory]
    [InlineData("healthy", ServerHealthStatus.Healthy)]
    [InlineData("HEALTHY", ServerHealthStatus.Healthy)]
    [InlineData("slow", ServerHealthStatus.Degraded)]
    [InlineData("Slow", ServerHealthStatus.Degraded)]
    [InlineData("something-else", ServerHealthStatus.Unknown)]
    public async Task CheckHealth_MapsTheApiHealthString(string rawHealth, ServerHealthStatus expected)
    {
        // Arrange
        var dedicated = new FakeDedicatedServerApiClient
        {
            HealthResponse = new HealthCheckResponse(rawHealth),
        };
        ServerObservationService service = CreateService(dedicated);

        // Act
        ServerHealth health = await service.CheckHealthAsync();

        // Assert
        Assert.Equal(expected, health.Status);
        Assert.Equal(rawHealth, health.RawHealth);
    }

    // ---- verify_connection: dedicated server ----------------------------------------------------

    [Fact]
    public async Task VerifyConnection_DedicatedServerUnconfigured_ReportsNotConfiguredWithEnvVar()
    {
        // Arrange — no BaseUrl => surface dormant.
        ServerObservationService service = CreateService(
            new FakeDedicatedServerApiClient(),
            dedicatedOptions: new DedicatedServerOptions());

        // Act
        SurfaceConnectionStatus dedicated = await SurfaceFor(service, DedicatedServerOptions.SurfaceName);

        // Assert — unconfigured is reported, never thrown; reachability is not tested (null).
        Assert.False(dedicated.IsConfigured);
        Assert.Null(dedicated.IsReachable);
        Assert.Contains(DedicatedServerOptions.ActivatingEnvVar, dedicated.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyConnection_DedicatedServerConfiguredAndUp_ReportsReachable()
    {
        // Arrange
        var dedicated = new FakeDedicatedServerApiClient
        {
            HealthResponse = new HealthCheckResponse("healthy"),
        };
        ServerObservationService service = CreateService(dedicated);

        // Act
        SurfaceConnectionStatus status = await SurfaceFor(service, DedicatedServerOptions.SurfaceName);

        // Assert
        Assert.True(status.IsConfigured);
        Assert.True(status.IsReachable);
    }

    [Fact]
    public async Task VerifyConnection_DedicatedServerUnreachable_ReportsReasonNotThrows()
    {
        // Arrange — a transport failure during the probe must be caught and reported.
        var dedicated = new FakeDedicatedServerApiClient
        {
            HealthException = new SurfaceUnreachableException(
                DedicatedServerOptions.SurfaceName,
                new Uri(BaseUrl),
                new HttpRequestException("Connection refused")),
        };
        ServerObservationService service = CreateService(dedicated);

        // Act
        SurfaceConnectionStatus status = await SurfaceFor(service, DedicatedServerOptions.SurfaceName);

        // Assert
        Assert.True(status.IsConfigured);
        Assert.False(status.IsReachable);
        Assert.Contains("unreachable", status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ---- verify_connection: FRM -----------------------------------------------------------------

    [Fact]
    public async Task VerifyConnection_FrmUnconfigured_ReportsNotConfiguredWithEnvVar()
    {
        // Arrange
        ServerObservationService service = CreateService(
            new FakeDedicatedServerApiClient(),
            frmOptions: new FrmOptions());

        // Act
        SurfaceConnectionStatus frm = await SurfaceFor(service, FrmOptions.SurfaceName);

        // Assert
        Assert.False(frm.IsConfigured);
        Assert.Null(frm.IsReachable);
        Assert.Contains(FrmOptions.ActivatingEnvVar, frm.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyConnection_FrmConfiguredAndUp_ReportsReachable()
    {
        // Arrange
        var frmClient = new FakeFrmClient { ProdStats = ImmutableArray<FrmProdStatsItem>.Empty };
        ServerObservationService service = CreateService(new FakeDedicatedServerApiClient(), frm: frmClient);

        // Act
        SurfaceConnectionStatus frm = await SurfaceFor(service, FrmOptions.SurfaceName);

        // Assert
        Assert.True(frm.IsConfigured);
        Assert.True(frm.IsReachable);
    }

    [Fact]
    public async Task VerifyConnection_FrmUnreachable_ReportsReasonNotThrows()
    {
        // Arrange — FRM's own actionable error must be caught and reported.
        var frmClient = new FakeFrmClient
        {
            ProdStatsException = new FrmUnreachableException(
                new Uri(FrmUrl), "getProdStats", "Connection refused"),
        };
        ServerObservationService service = CreateService(new FakeDedicatedServerApiClient(), frm: frmClient);

        // Act
        SurfaceConnectionStatus frm = await SurfaceFor(service, FrmOptions.SurfaceName);

        // Assert
        Assert.True(frm.IsConfigured);
        Assert.False(frm.IsReachable);
        Assert.Contains("FRM not responding", frm.Detail, StringComparison.Ordinal);
    }

    // ---- verify_connection: FIN bridge ----------------------------------------------------------

    [Fact]
    public async Task VerifyConnection_FinBridgeUnconfigured_ReportsNotConfiguredWithEnvVar()
    {
        // Arrange — bridge dormant and no IFinBridge injected (mirrors the gated DI registration).
        ServerObservationService service = CreateService(
            new FakeDedicatedServerApiClient(),
            finOptions: new FinBridgeOptions(),
            finBridge: null);

        // Act
        SurfaceConnectionStatus fin = await SurfaceFor(service, FinBridgeOptions.SurfaceName);

        // Assert
        Assert.False(fin.IsConfigured);
        Assert.Null(fin.IsReachable);
        Assert.Contains(FinBridgeOptions.ActivatingEnvVar, fin.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyConnection_FinBridgeConfiguredButListenerMissing_ReportsUnreachable()
    {
        // Arrange — configured options but a null bridge means the listener was not wired up.
        ServerObservationService service = CreateService(
            new FakeDedicatedServerApiClient(),
            finOptions: ConfiguredFin(),
            finBridge: null);

        // Act
        SurfaceConnectionStatus fin = await SurfaceFor(service, FinBridgeOptions.SurfaceName);

        // Assert
        Assert.True(fin.IsConfigured);
        Assert.False(fin.IsReachable);
    }

    [Fact]
    public async Task VerifyConnection_FinBridgeConfiguredWithListener_NoAgents_ReportsReachableWithHint()
    {
        // Arrange — listener up, no agent has connected yet.
        var bridge = new FakeFinBridge(ImmutableArray<AgentLiveness>.Empty);
        ServerObservationService service = CreateService(
            new FakeDedicatedServerApiClient(),
            finOptions: ConfiguredFin(),
            finBridge: bridge);

        // Act
        SurfaceConnectionStatus fin = await SurfaceFor(service, FinBridgeOptions.SurfaceName);

        // Assert
        Assert.True(fin.IsConfigured);
        Assert.True(fin.IsReachable);
        Assert.Contains("no in-world FIN agent", fin.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyConnection_FinBridgeConfiguredWithListener_AgentAlive_ReportsAgentCount()
    {
        // Arrange — one known agent, alive.
        var agent = new AgentLiveness("agent-1", IsAlive: true, DateTimeOffset.UtcNow, "1.0.0", 0);
        var bridge = new FakeFinBridge([agent]);
        ServerObservationService service = CreateService(
            new FakeDedicatedServerApiClient(),
            finOptions: ConfiguredFin(),
            finBridge: bridge);

        // Act
        SurfaceConnectionStatus fin = await SurfaceFor(service, FinBridgeOptions.SurfaceName);

        // Assert
        Assert.True(fin.IsReachable);
        Assert.Contains("1 of 1 known agent", fin.Detail, StringComparison.Ordinal);
    }

    // ---- whole-result aggregation ---------------------------------------------------------------

    [Fact]
    public async Task VerifyConnection_ReportsAllThreeSurfaces_AndOneBadSurfaceDoesNotHideOthers()
    {
        // Arrange — dedicated up, FRM unreachable, FIN unconfigured: all three must still be reported.
        var dedicated = new FakeDedicatedServerApiClient { HealthResponse = new HealthCheckResponse("healthy") };
        var frmClient = new FakeFrmClient
        {
            ProdStatsException = new FrmUnreachableException(new Uri(FrmUrl), "getProdStats", "refused"),
        };
        ServerObservationService service = CreateService(
            dedicated,
            frm: frmClient,
            finOptions: new FinBridgeOptions(),
            finBridge: null);

        // Act
        ConnectionDiagnostics result = await service.VerifyConnectionAsync();

        // Assert
        Assert.Equal(3, result.Surfaces.Length);
        Assert.True(Single(result, DedicatedServerOptions.SurfaceName).IsReachable);
        Assert.False(Single(result, FrmOptions.SurfaceName).IsReachable);
        Assert.False(Single(result, FinBridgeOptions.SurfaceName).IsConfigured);
    }

    // ---- helpers --------------------------------------------------------------------------------

    private static FinBridgeOptions ConfiguredFin() =>
        new() { ListenUrl = FinUrl, SharedSecret = "secret" };

    private static async Task<SurfaceConnectionStatus> SurfaceFor(
        IServerObservationService service, string surfaceName)
    {
        ConnectionDiagnostics result = await service.VerifyConnectionAsync();
        return Single(result, surfaceName);
    }

    private static SurfaceConnectionStatus Single(ConnectionDiagnostics result, string surfaceName) =>
        result.Surfaces.Single(s => s.Surface == surfaceName);

    private static ServerObservationService CreateService(
        FakeDedicatedServerApiClient dedicated,
        IFrmClient? frm = null,
        DedicatedServerOptions? dedicatedOptions = null,
        FrmOptions? frmOptions = null,
        FinBridgeOptions? finOptions = null,
        IFinBridge? finBridge = null) =>
        new(
            dedicated,
            frm ?? new FakeFrmClient { ProdStats = ImmutableArray<FrmProdStatsItem>.Empty },
            dedicatedOptions ?? new DedicatedServerOptions { BaseUrl = BaseUrl },
            frmOptions ?? new FrmOptions { BaseUrl = FrmUrl },
            finOptions ?? new FinBridgeOptions(),
            finBridge);
}
