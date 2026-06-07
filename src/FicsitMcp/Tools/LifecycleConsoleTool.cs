using System.ComponentModel;
using System.Security.Cryptography;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Tools.Results;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FicsitMcp.Tools;

/// <summary>
/// The "sharp" lifecycle and console MCP tools over the Dedicated Server HTTPS API: running an
/// arbitrary console command, shutting the server down, renaming it, and changing the client/admin
/// passwords. Thin by design — each tool validates input, delegates to
/// <see cref="IDedicatedServerApiClient"/>, and maps typed protocol exceptions to an actionable,
/// secret-free error. The wire protocol (envelope, bearer auth, the admin-password token-rotation
/// dance) lives entirely in the client; these tools never touch HTTP.
/// </summary>
/// <remarks>
/// <para>
/// <b>Secrets discipline.</b> Password parameters are accepted but NEVER logged, echoed in a result,
/// or placed in an exception message. Log templates reference only non-secret fields (the function
/// being invoked, the new server name). The admin-password change additionally mints a fresh bearer
/// token internally (see <see cref="SetAdminPassword"/>); that token is likewise never surfaced.
/// </para>
/// <para>
/// <b>Behavioral hints are honest.</b> <c>run_console_command</c>, <c>shutdown_server</c>,
/// <c>set_client_password</c>, and <c>set_admin_password</c> are marked <c>Destructive</c> (and the
/// console command additionally <c>OpenWorld</c>, since its effect space is unknowable). Only
/// <c>rename_server</c> is non-destructive. Each description leads with the consequence so a
/// well-behaved model confirms with the user before invoking.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class LifecycleConsoleTool
{
    /// <summary>
    /// Number of random bytes for a freshly minted admin bearer token. 32 bytes = 256 bits of
    /// entropy, encoded as URL-safe base64 — comfortably unguessable and safe to send as a header.
    /// </summary>
    private const int AdminTokenByteLength = 32;

    /// <summary>
    /// Executes an arbitrary console command on the server. The effects may be irreversible and
    /// unknown — there is no closed list of commands and mods can register more.
    /// </summary>
    [McpServerTool(Name = "run_console_command", Destructive = true, OpenWorld = true, Idempotent = false)]
    [Description(
        "Executes an arbitrary server console command whose effects may be irreversible and unknown. " +
        "The command runs in the live server context and there is no fixed list of commands — vanilla " +
        "exposes cvars (e.g. FG.* settings) and stat dumps, but installed mods can register commands " +
        "that do anything, including destructive or world-altering actions. This is the raw escape " +
        "hatch: confirm the exact command with the user before invoking, because the model cannot " +
        "predict the outcome. Returns the captured console output and the command's boolean return " +
        "value. Requires admin privilege; if the command is rejected the call fails with the server's " +
        "error rather than a partial result.")]
    public static async Task<RunConsoleCommandResult> RunConsoleCommand(
        IDedicatedServerApiClient client,
        ILogger<LifecycleConsoleTool> logger,
        [Description(
            "The console command line to execute verbatim on the server (e.g. \"FG.NetworkQuality 3\"). " +
            "Effects are unknowable in general; confirm with the user first.")]
        string command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        // Log the INVOCATION but never the command's effect assumptions. The command text is the
        // model's own input (not a secret), so it is safe to log for an audit trail.
        logger.LogInformation("Running console command on the dedicated server: {Command}", command);

        try
        {
            RunCommandResponse? response =
                await client.RunCommandAsync(new RunCommandRequest(command), cancellationToken)
                    .ConfigureAwait(false);

            // A null response is the "executed, no output" 204 contract: surface empty output and a
            // success-shaped return value so the model does not mistake silence for failure.
            return new RunConsoleCommandResult(
                response?.CommandResult ?? string.Empty,
                response?.ReturnValue ?? true);
        }
        catch (DedicatedServerApiException ex)
        {
            throw ToActionableError("run the console command", ex);
        }
    }

    /// <summary>
    /// Shuts the server process down. Whether that means "restart" or "stays down" depends on how
    /// the process is supervised — see the description.
    /// </summary>
    [McpServerTool(Name = "shutdown_server", Destructive = true, Idempotent = false)]
    [Description(
        "Shuts the dedicated server down, disconnecting all connected players immediately. The " +
        "real-world outcome depends on how the server process is supervised, and this MCP server " +
        "cannot tell which applies: under a service manager or restart policy (systemd Restart=, a " +
        "Docker restart policy, a wrapper script) the process is relaunched, so this acts as a " +
        "RESTART lever; run as a bare foreground process it simply STAYS DOWN until someone starts " +
        "it again manually. The API has no save-before-shutdown option, so unsaved progress since the " +
        "last save is lost — call the save tool first if you need a checkpoint. Confirm with the user " +
        "which outcome they expect before invoking.")]
    public static async Task<ShutdownServerResult> ShutdownServer(
        IDedicatedServerApiClient client,
        ILogger<LifecycleConsoleTool> logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting dedicated server shutdown.");

        try
        {
            await client.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            return new ShutdownServerResult(
                "Shutdown request accepted. If the server is supervised (service manager or restart " +
                "policy) it will restart; if it runs as a bare process it will stay down until started " +
                "manually.");
        }
        catch (DedicatedServerApiException ex)
        {
            throw ToActionableError("shut the server down", ex);
        }
    }

    /// <summary>
    /// Sets the server's display name. Reversible metadata, so non-destructive; idempotent because
    /// setting the same name twice yields the same state.
    /// </summary>
    [McpServerTool(Name = "rename_server", Destructive = false, Idempotent = true)]
    [Description(
        "Sets the dedicated server's display name (the name shown in the server browser). This is " +
        "reversible metadata only — it does not affect gameplay, saves, or connected players — so it " +
        "is safe to run and re-run. Requires admin privilege.")]
    public static async Task<RenameServerResult> RenameServer(
        IDedicatedServerApiClient client,
        ILogger<LifecycleConsoleTool> logger,
        [Description("The new server display name.")] string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        logger.LogInformation("Renaming the dedicated server to {ServerName}.", name);

        try
        {
            await client.RenameServerAsync(new RenameServerRequest(name), cancellationToken)
                .ConfigureAwait(false);
            return new RenameServerResult(name);
        }
        catch (DedicatedServerApiException ex)
        {
            throw ToActionableError("rename the server", ex);
        }
    }

    /// <summary>
    /// Sets or clears the client (join) password. Destructive: it locks out players who lack the new
    /// password, and an empty value opens the server to anyone.
    /// </summary>
    [McpServerTool(Name = "set_client_password", Destructive = true, Idempotent = true)]
    [Description(
        "Changes the client (join) password, which immediately affects who can connect: players who " +
        "do not have the NEW password can no longer join, and passing an EMPTY password REMOVES the " +
        "requirement entirely, opening the server to anyone who can reach it. Existing connected " +
        "players are not kicked, but reconnection requires the new password. The password is never " +
        "logged or echoed back. Confirm the intended access change with the user before invoking. " +
        "Requires admin privilege.")]
    public static async Task<SetPasswordResult> SetClientPassword(
        IDedicatedServerApiClient client,
        ILogger<LifecycleConsoleTool> logger,
        [Description(
            "The new client (join) password. Pass an EMPTY string to remove the password and open the " +
            "server. Never logged or echoed.")]
        string password,
        CancellationToken cancellationToken = default)
    {
        // password may legitimately be empty (clears the password). Only null is invalid.
        ArgumentNullException.ThrowIfNull(password);

        bool clearing = password.Length == 0;

        // Structured log references only the ACTION, never the password value.
        logger.LogInformation(
            "Setting the client password ({Action}).", clearing ? "clearing" : "setting a new value");

        try
        {
            await client.SetClientPasswordAsync(new SetClientPasswordRequest(password), cancellationToken)
                .ConfigureAwait(false);

            return new SetPasswordResult(
                clearing
                    ? "Client password removed. The server now accepts connections without a password."
                    : "Client password updated. Players must use the new password to join.",
                PasswordCleared: clearing);
        }
        catch (DedicatedServerApiException ex)
        {
            throw ToActionableError("set the client password", ex);
        }
    }

    /// <summary>
    /// Changes the admin password. Destructive: it invalidates the current admin session/token (the
    /// client transparently re-authenticates with a freshly minted token) and locks out anyone using
    /// the old admin password.
    /// </summary>
    [McpServerTool(Name = "set_admin_password", Destructive = true, Idempotent = false)]
    [Description(
        "Changes the server admin password, which immediately invalidates the current admin " +
        "authentication token used to make the change. The client mints a new bearer token and adopts " +
        "it transparently so subsequent admin tools keep working, but any OTHER session using the old " +
        "admin password or token is locked out and must re-authenticate with the new password. An " +
        "empty admin password is rejected (it would leave the server with no admin credential). The " +
        "password is never logged or echoed back. Confirm with the user before invoking. Requires " +
        "admin privilege.")]
    public static async Task<SetPasswordResult> SetAdminPassword(
        IDedicatedServerApiClient client,
        ILogger<LifecycleConsoleTool> logger,
        [Description(
            "The new admin password. Must be non-empty. Never logged or echoed.")]
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
        {
            // An empty admin password is not a "clear" — it would leave the server with no admin
            // credential. Reject before sending. This message carries no secret.
            throw new ArgumentException(
                "The admin password must be non-empty. To open the server to clients, use " +
                "set_client_password instead.",
                nameof(password));
        }

        // Changing the admin password invalidates the OLD token, so the API requires the request to
        // carry the NEW token the server should honor afterwards. We mint a fresh, high-entropy token
        // here (the caller chooses it, akin to server.GenerateAPIToken). The token is a SECRET: it is
        // never logged, never returned in the result, and never placed in an exception. The client
        // adopts it on success for all subsequent calls.
        string newToken = GenerateAdminToken();

        // Structured log references only the ACTION — never the password or the minted token.
        logger.LogInformation("Changing the admin password and rotating the admin token.");

        try
        {
            await client.SetAdminPasswordAsync(
                    new SetAdminPasswordRequest(password, newToken), cancellationToken)
                .ConfigureAwait(false);

            return new SetPasswordResult(
                "Admin password changed. The previous admin token is now invalid; this session has " +
                "transparently re-authenticated. Any other admin session must re-authenticate with the " +
                "new password.",
                PasswordCleared: false);
        }
        catch (DedicatedServerApiException ex)
        {
            throw ToActionableError("set the admin password", ex);
        }
    }

    /// <summary>
    /// Mints a fresh, URL-safe, high-entropy bearer token for the admin-password rotation. Uses a
    /// cryptographic RNG; the value never leaves this process except as the bearer the client sends.
    /// </summary>
    private static string GenerateAdminToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(AdminTokenByteLength);
        // Base64Url: safe to send as an Authorization header value with no escaping.
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Maps a typed protocol exception to an actionable, SECRET-FREE <see cref="McpException"/> for
    /// the model. The source exceptions already carry only the server's own error code/message (never
    /// a password or token), and we add no input here, so nothing secret can leak. The ambiguous-
    /// result case is called out specifically so the model re-queries state instead of blind-retrying.
    /// </summary>
    private static McpException ToActionableError(string action, DedicatedServerApiException ex)
    {
        if (ex is DedicatedServerAmbiguousResultException ambiguous)
        {
            return new McpException(
                $"Could not confirm whether the attempt to {action} took effect: the authentication " +
                $"token was rejected after the request may already have been delivered, so '{ambiguous.FunctionName}' " +
                "was NOT retried to avoid running it twice. Re-query server state before trying again.",
                ex);
        }

        if (ex is DedicatedServerAuthException)
        {
            return new McpException(
                $"Not authorized to {action}: {ex.ServerMessage ?? ex.Message} " +
                "Ensure an admin token is configured (FICSITMCP_DedicatedServer__AdminToken) or " +
                "authenticate first.",
                ex);
        }

        return new McpException(
            $"Failed to {action}: {ex.ServerMessage ?? ex.Message}",
            ex);
    }
}
