using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Channels;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.FinBridge;
using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Frm.Model;

namespace FicsitMcp.Tests.ServerObservation;

/// <summary>
/// Hand-rolled fake <see cref="IDedicatedServerApiClient"/> for the observation-service tests. Only
/// the two methods the service uses (<see cref="QueryServerStateAsync"/>, <see cref="HealthCheckAsync"/>)
/// are scriptable; every other member throws <see cref="NotSupportedException"/> so an accidental use
/// is loud. Set the <c>*Response</c> property to script success or the <c>*Exception</c> to script a
/// throw.
/// </summary>
internal sealed class FakeDedicatedServerApiClient : IDedicatedServerApiClient
{
    public QueryServerStateResponse? StateResponse { get; init; }
    public Exception? StateException { get; init; }
    public HealthCheckResponse? HealthResponse { get; init; }
    public Exception? HealthException { get; init; }

    public Task<QueryServerStateResponse> QueryServerStateAsync(CancellationToken cancellationToken = default)
    {
        if (StateException is not null)
        {
            return Task.FromException<QueryServerStateResponse>(StateException);
        }

        return Task.FromResult(StateResponse ?? throw new InvalidOperationException("No StateResponse scripted."));
    }

    public Task<HealthCheckResponse> HealthCheckAsync(
        HealthCheckRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (HealthException is not null)
        {
            return Task.FromException<HealthCheckResponse>(HealthException);
        }

        return Task.FromResult(HealthResponse ?? throw new InvalidOperationException("No HealthResponse scripted."));
    }

    // ---- Unused members: loud if touched -------------------------------------------------------

    public Task<AuthenticationTokenResponse> PasswordlessLoginAsync(PasswordlessLoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthenticationTokenResponse> PasswordLoginAsync(PasswordLoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task VerifyAuthenticationTokenAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<GetServerOptionsResponse> GetServerOptionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<GetAdvancedGameSettingsResponse> GetAdvancedGameSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EnumerateSessionsResponse> EnumerateSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthenticationTokenResponse> ClaimServerAsync(ClaimServerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RenameServerAsync(RenameServerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SetClientPasswordAsync(SetClientPasswordRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AuthenticationTokenResponse> SetAdminPasswordAsync(SetAdminPasswordRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SetAutoLoadSessionNameAsync(SetAutoLoadSessionNameRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ApplyServerOptionsAsync(ApplyServerOptionsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ApplyAdvancedGameSettingsAsync(ApplyAdvancedGameSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<RunCommandResponse?> RunCommandAsync(RunCommandRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CreateNewGameAsync(CreateNewGameRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveGameAsync(SaveGameRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteSaveFileAsync(DeleteSaveFileRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteSaveSessionAsync(DeleteSaveSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UploadSaveGameAsync(UploadSaveGameRequest request, Stream saveGameContent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DownloadSaveGameAsync(DownloadSaveGameRequest request, Stream destination, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<JsonElement?> InvokeRawAsync(string function, JsonElement? data, bool allowRetry = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>
/// Hand-rolled fake <see cref="IFrmClient"/>. Only <see cref="GetProdStatsAsync"/> (the surface's
/// cheapest read, used as the reachability probe) is scriptable; the rest throw.
/// </summary>
internal sealed class FakeFrmClient : IFrmClient
{
    public ImmutableArray<FrmProdStatsItem> ProdStats { get; init; } = ImmutableArray<FrmProdStatsItem>.Empty;
    public Exception? ProdStatsException { get; init; }

    public Task<ImmutableArray<FrmProdStatsItem>> GetProdStatsAsync(CancellationToken cancellationToken)
    {
        if (ProdStatsException is not null)
        {
            return Task.FromException<ImmutableArray<FrmProdStatsItem>>(ProdStatsException);
        }

        return Task.FromResult(ProdStats);
    }

    public Task<ImmutableArray<FrmFactoryBuilding>> GetFactoryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ImmutableArray<FrmPowerCircuit>> GetPowerAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ImmutableArray<FrmTrain>> GetTrainsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ImmutableArray<FrmDrone>> GetDronesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ImmutableArray<FrmVehicle>> GetVehiclesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ImmutableArray<FrmPlayer>> GetPlayersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ImmutableArray<FrmResourceNode>> GetResourceNodesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>
/// Hand-rolled fake <see cref="IFinBridge"/> exposing a fixed agent roster via
/// <see cref="GetAgents"/> (the only member the observation service reads). The rest throw.
/// </summary>
internal sealed class FakeFinBridge : IFinBridge
{
    private readonly IReadOnlyList<AgentLiveness> _agents;

    public FakeFinBridge(IReadOnlyList<AgentLiveness> agents) => _agents = agents;

    public IReadOnlyList<AgentLiveness> GetAgents() => _agents;

    public Task<FinResult> SendAsync(string agentId, FinCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public AgentLiveness GetLiveness(string agentId) => throw new NotSupportedException();
    public IReadOnlyList<FinEvent> RecentEvents() => throw new NotSupportedException();
    public IDisposable Subscribe(out ChannelReader<FinEvent> reader) => throw new NotSupportedException();
    public HelloResponse HelloAsync(HelloRequest hello) => throw new NotSupportedException();
    public Task<PollResponse> PollAsync(PollRequest poll, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
