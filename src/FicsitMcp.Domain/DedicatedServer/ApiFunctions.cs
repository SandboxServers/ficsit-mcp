namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// The exact <c>function</c> names the base-game dedicated-server API recognises, as verified
/// against the official Satisfactory modding docs / OpenAPI spec. These are the closed set the
/// typed client methods use; mods may register ADDITIONAL functions (e.g. FRM's <c>frm</c>) which
/// callers reach through <see cref="IDedicatedServerApiClient.InvokeRawAsync"/> — the function name
/// is deliberately an open set at the envelope layer.
/// </summary>
internal static class ApiFunctions
{
    // Auth.
    public const string PasswordlessLogin = "PasswordlessLogin";
    public const string PasswordLogin = "PasswordLogin";
    public const string VerifyAuthenticationToken = "VerifyAuthenticationToken";

    // State / reads.
    public const string HealthCheck = "HealthCheck";
    public const string QueryServerState = "QueryServerState";
    public const string GetServerOptions = "GetServerOptions";
    public const string GetAdvancedGameSettings = "GetAdvancedGameSettings";
    public const string EnumerateSessions = "EnumerateSessions";

    // Management.
    public const string ClaimServer = "ClaimServer";
    public const string RenameServer = "RenameServer";
    public const string SetClientPassword = "SetClientPassword";
    public const string SetAdminPassword = "SetAdminPassword";
    public const string SetAutoLoadSessionName = "SetAutoLoadSessionName";
    public const string ApplyServerOptions = "ApplyServerOptions";
    public const string ApplyAdvancedGameSettings = "ApplyAdvancedGameSettings";

    // Console.
    public const string RunCommand = "RunCommand";
    public const string Shutdown = "Shutdown";

    // Save game.
    public const string CreateNewGame = "CreateNewGame";
    public const string SaveGame = "SaveGame";
    public const string LoadGame = "LoadGame";
    public const string DeleteSaveFile = "DeleteSaveFile";
    public const string DeleteSaveSession = "DeleteSaveSession";
    public const string UploadSaveGame = "UploadSaveGame";
    public const string DownloadSaveGame = "DownloadSaveGame";
}
