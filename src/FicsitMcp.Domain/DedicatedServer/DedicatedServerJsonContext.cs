using System.Text.Json;
using System.Text.Json.Serialization;

using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// System.Text.Json source-generated context for every dedicated-server DTO and envelope. Using a
/// source-generated context (rather than reflection-based serialization) keeps the surface
/// trimming/AOT-viable and avoids reflection at runtime — a requirement for this client.
/// </summary>
/// <remarks>
/// <para>
/// Registered types: every typed request record, every response record, the generic success
/// envelope per response shape, and the error envelope. The privilege-level enum is serialized by
/// its string name (the exact identifier the server expects), set globally via
/// <see cref="JsonSourceGenerationOptionsAttribute"/>.
/// </para>
/// <para>
/// <see cref="JsonElement"/> is registered so the error envelope's free-form <c>errorData</c> and
/// the open-set request builder (arbitrary mod functions, e.g. FRM's <c>frm</c>) can round-trip a
/// raw payload without a typed shape.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
// Envelopes.
[JsonSerializable(typeof(DedicatedServerRequestEnvelope))]
[JsonSerializable(typeof(DedicatedServerErrorEnvelope))]
[JsonSerializable(typeof(JsonElement))]
// Auth.
[JsonSerializable(typeof(PasswordlessLoginRequest))]
[JsonSerializable(typeof(PasswordLoginRequest))]
[JsonSerializable(typeof(AuthenticationTokenResponse))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<AuthenticationTokenResponse>))]
// Health.
[JsonSerializable(typeof(HealthCheckRequest))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<HealthCheckResponse>))]
// Server state.
[JsonSerializable(typeof(QueryServerStateResponse))]
[JsonSerializable(typeof(ServerGameState))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<QueryServerStateResponse>))]
// Server management.
[JsonSerializable(typeof(ClaimServerRequest))]
[JsonSerializable(typeof(RenameServerRequest))]
[JsonSerializable(typeof(SetClientPasswordRequest))]
[JsonSerializable(typeof(SetAdminPasswordRequest))]
[JsonSerializable(typeof(SetAutoLoadSessionNameRequest))]
// Server options / advanced settings.
[JsonSerializable(typeof(GetServerOptionsResponse))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<GetServerOptionsResponse>))]
[JsonSerializable(typeof(ApplyServerOptionsRequest))]
[JsonSerializable(typeof(GetAdvancedGameSettingsResponse))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<GetAdvancedGameSettingsResponse>))]
[JsonSerializable(typeof(ApplyAdvancedGameSettingsRequest))]
// Console.
[JsonSerializable(typeof(RunCommandRequest))]
[JsonSerializable(typeof(RunCommandResponse))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<RunCommandResponse>))]
// Save game.
[JsonSerializable(typeof(SaveGameRequest))]
[JsonSerializable(typeof(LoadGameRequest))]
[JsonSerializable(typeof(DeleteSaveFileRequest))]
[JsonSerializable(typeof(DeleteSaveSessionRequest))]
[JsonSerializable(typeof(CreateNewGameRequest))]
[JsonSerializable(typeof(NewGameData))]
[JsonSerializable(typeof(EnumerateSessionsResponse))]
[JsonSerializable(typeof(SessionSaveStruct))]
[JsonSerializable(typeof(SaveHeader))]
[JsonSerializable(typeof(DedicatedServerSuccessEnvelope<EnumerateSessionsResponse>))]
[JsonSerializable(typeof(UploadSaveGameRequest))]
[JsonSerializable(typeof(DownloadSaveGameRequest))]
// Shared map type used by options/settings payloads.
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public sealed partial class DedicatedServerJsonContext : JsonSerializerContext
{
}
