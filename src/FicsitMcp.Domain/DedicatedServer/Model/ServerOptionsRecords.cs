using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>Response <c>data</c> for <c>GetServerOptions</c>.</summary>
/// <param name="ServerOptions">Currently-active options as a string→string map.</param>
/// <param name="PendingServerOptions">Options applied but pending a restart, as a string→string map.</param>
public sealed record GetServerOptionsResponse(
    [property: JsonPropertyName("serverOptions")] IReadOnlyDictionary<string, string> ServerOptions,
    [property: JsonPropertyName("pendingServerOptions")] IReadOnlyDictionary<string, string> PendingServerOptions);

/// <summary>Request <c>data</c> for <c>ApplyServerOptions</c>.</summary>
/// <param name="UpdatedServerOptions">The options to set, as a string→string map.</param>
public sealed record ApplyServerOptionsRequest(
    [property: JsonPropertyName("updatedServerOptions")] IReadOnlyDictionary<string, string> UpdatedServerOptions);

/// <summary>Response <c>data</c> for <c>GetAdvancedGameSettings</c>.</summary>
/// <param name="CreativeModeEnabled">Whether advanced (creative) game settings are active.</param>
/// <param name="AdvancedGameSettings">The applied advanced settings as a string→string map.</param>
public sealed record GetAdvancedGameSettingsResponse(
    [property: JsonPropertyName("creativeModeEnabled")] bool CreativeModeEnabled,
    [property: JsonPropertyName("advancedGameSettings")] IReadOnlyDictionary<string, string> AdvancedGameSettings);

/// <summary>Request <c>data</c> for <c>ApplyAdvancedGameSettings</c>.</summary>
/// <param name="AppliedAdvancedGameSettings">The advanced settings to apply, as a string→string map.</param>
public sealed record ApplyAdvancedGameSettingsRequest(
    [property: JsonPropertyName("appliedAdvancedGameSettings")] IReadOnlyDictionary<string, string> AppliedAdvancedGameSettings);
