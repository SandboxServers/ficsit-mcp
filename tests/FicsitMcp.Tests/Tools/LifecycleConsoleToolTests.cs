using System.Net;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Tools;
using FicsitMcp.Tools.Results;

using ModelContextProtocol;

namespace FicsitMcp.Tests.Tools;

/// <summary>
/// Tests for the lifecycle/console tools. They are intentionally thin, so these assert three things
/// per tool: the happy path delegates the right record and shapes a useful result; a typed protocol
/// exception is mapped to an actionable <see cref="McpException"/> with no stack trace leaking to the
/// model; and — for the password tools — the password (and the minted admin token) appear NOWHERE in
/// the logs, the result, or the thrown exception.
/// </summary>
public sealed class LifecycleConsoleToolTests
{
    private static CapturingLogger<LifecycleConsoleTool> Logger() => new();

    // ===== run_console_command ===================================================================

    [Fact]
    public async Task RunConsoleCommand_DelegatesCommandAndReturnsOutput()
    {
        var client = new FakeDedicatedServerApiClient { RunCommandResult = new RunCommandResponse("line1\nline2", true) };

        RunConsoleCommandResult result =
            await LifecycleConsoleTool.RunConsoleCommand(client, Logger(), "FG.NetworkQuality 3");

        Assert.Equal("FG.NetworkQuality 3", client.LastRunCommand!.Command);
        Assert.Equal("line1\nline2", result.Output);
        Assert.True(result.ReturnValue);
    }

    [Fact]
    public async Task RunConsoleCommand_NullResponse_TreatedAsExecutedNoOutput()
    {
        var client = new FakeDedicatedServerApiClient { RunCommandResult = null };

        RunConsoleCommandResult result =
            await LifecycleConsoleTool.RunConsoleCommand(client, Logger(), "stat fps");

        Assert.Equal(string.Empty, result.Output);
        Assert.True(result.ReturnValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunConsoleCommand_RejectsBlankCommand(string blank)
    {
        var client = new FakeDedicatedServerApiClient();
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => LifecycleConsoleTool.RunConsoleCommand(client, Logger(), blank));
        Assert.Null(client.LastRunCommand);
    }

    [Fact]
    public async Task RunConsoleCommand_ApiError_MapsToActionableMcpException_NoStackTrace()
    {
        var client = new FakeDedicatedServerApiClient
        {
            ThrowOnRunCommand = new DedicatedServerApiException("command_failed", "Bad command"),
        };

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => LifecycleConsoleTool.RunConsoleCommand(client, Logger(), "nonsense"));

        Assert.Contains("run the console command", ex.Message);
        Assert.Contains("Bad command", ex.Message);
        Assert.DoesNotContain("at FicsitMcp", ex.Message);
    }

    [Fact]
    public async Task RunConsoleCommand_AmbiguousResult_TellsModelToReQueryNotRetry()
    {
        var client = new FakeDedicatedServerApiClient
        {
            ThrowOnRunCommand =
                new DedicatedServerAmbiguousResultException("RunCommand", "unauthorized", "token rejected"),
        };

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => LifecycleConsoleTool.RunConsoleCommand(client, Logger(), "give_items"));

        Assert.Contains("Re-query server state", ex.Message);
        Assert.Contains("NOT retried", ex.Message);
    }

    // ===== shutdown_server =======================================================================

    [Fact]
    public async Task ShutdownServer_DelegatesAndReportsBothOutcomes()
    {
        var client = new FakeDedicatedServerApiClient();

        ShutdownServerResult result = await LifecycleConsoleTool.ShutdownServer(client, Logger());

        Assert.True(client.ShutdownCalled);
        // The result must surface the restart-vs-stays-down split, not claim one outcome.
        Assert.Contains("restart", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stay down", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShutdownServer_ApiError_MapsToActionableMcpException()
    {
        var client = new FakeDedicatedServerApiClient
        {
            ThrowOnShutdown = new DedicatedServerAuthException("unauthorized", "Token rejected", httpStatusCode: 401),
        };

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => LifecycleConsoleTool.ShutdownServer(client, Logger()));

        Assert.Contains("shut the server down", ex.Message);
    }

    // ===== rename_server =========================================================================

    [Fact]
    public async Task RenameServer_DelegatesNameAndEchoesIt()
    {
        var client = new FakeDedicatedServerApiClient();

        RenameServerResult result = await LifecycleConsoleTool.RenameServer(client, Logger(), "FICSIT Prime");

        Assert.Equal("FICSIT Prime", client.LastRename!.ServerName);
        Assert.Equal("FICSIT Prime", result.ServerName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameServer_RejectsBlankName(string blank)
    {
        var client = new FakeDedicatedServerApiClient();
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => LifecycleConsoleTool.RenameServer(client, Logger(), blank));
        Assert.Null(client.LastRename);
    }

    [Fact]
    public async Task RenameServer_ApiError_MapsToActionableMcpException()
    {
        var client = new FakeDedicatedServerApiClient
        {
            ThrowOnRename = new DedicatedServerApiException("rename_failed", "Name too long"),
        };

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => LifecycleConsoleTool.RenameServer(client, Logger(), "x"));

        Assert.Contains("rename the server", ex.Message);
        Assert.Contains("Name too long", ex.Message);
    }

    // ===== set_client_password ===================================================================

    [Fact]
    public async Task SetClientPassword_SetsValue_AndDoesNotReportCleared()
    {
        var client = new FakeDedicatedServerApiClient();

        SetPasswordResult result = await LifecycleConsoleTool.SetClientPassword(client, Logger(), "join-secret");

        Assert.Equal("join-secret", client.LastSetClientPassword!.Password);
        Assert.False(result.PasswordCleared);
    }

    [Fact]
    public async Task SetClientPassword_EmptyValue_ClearsAndReportsOpenServer()
    {
        var client = new FakeDedicatedServerApiClient();

        SetPasswordResult result = await LifecycleConsoleTool.SetClientPassword(client, Logger(), "");

        Assert.Equal("", client.LastSetClientPassword!.Password);
        Assert.True(result.PasswordCleared);
        Assert.Contains("without a password", result.Message);
    }

    [Fact]
    public async Task SetClientPassword_NullValue_Rejected()
    {
        var client = new FakeDedicatedServerApiClient();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => LifecycleConsoleTool.SetClientPassword(client, Logger(), null!));
    }

    [Fact]
    public async Task SetClientPassword_PasswordNeverAppearsInLogsOrResult()
    {
        const string secret = "S3cret-Join-P@ss-DEADBEEF";
        var client = new FakeDedicatedServerApiClient();
        var logger = Logger();

        SetPasswordResult result = await LifecycleConsoleTool.SetClientPassword(client, logger, secret);

        Assert.DoesNotContain(secret, logger.AllText);
        Assert.DoesNotContain(secret, result.Message);
    }

    [Fact]
    public async Task SetClientPassword_OnFailure_PasswordNeverAppearsInLogsOrException()
    {
        const string secret = "S3cret-Join-P@ss-DEADBEEF";
        var client = new FakeDedicatedServerApiClient
        {
            // The server's own error message must also not be a vector — it never contains the
            // password here, and the tool adds none of the input to the mapped message.
            ThrowOnSetClientPassword = new DedicatedServerApiException("set_failed", "Could not set password"),
        };
        var logger = Logger();

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => LifecycleConsoleTool.SetClientPassword(client, logger, secret));

        Assert.DoesNotContain(secret, logger.AllText);
        Assert.DoesNotContain(secret, ex.Message);
        Assert.DoesNotContain(secret, ex.ToString());
    }

    // ===== set_admin_password ====================================================================

    [Fact]
    public async Task SetAdminPassword_DelegatesPassword_AndMintsAFreshNonEmptyToken()
    {
        var client = new FakeDedicatedServerApiClient();

        SetPasswordResult result = await LifecycleConsoleTool.SetAdminPassword(client, Logger(), "admin-secret");

        Assert.Equal("admin-secret", client.LastSetAdminPassword!.Password);
        // The tool must mint a token for the change (the API requires the next token in the request).
        Assert.False(string.IsNullOrWhiteSpace(client.LastSetAdminPassword.AuthenticationToken));
        Assert.False(result.PasswordCleared);
    }

    [Fact]
    public async Task SetAdminPassword_MintsADifferentTokenEachCall()
    {
        var client = new FakeDedicatedServerApiClient();

        await LifecycleConsoleTool.SetAdminPassword(client, Logger(), "pw1");
        string first = client.LastSetAdminPassword!.AuthenticationToken;
        await LifecycleConsoleTool.SetAdminPassword(client, Logger(), "pw2");
        string second = client.LastSetAdminPassword!.AuthenticationToken;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task SetAdminPassword_EmptyPassword_RejectedBeforeSending()
    {
        var client = new FakeDedicatedServerApiClient();

        await Assert.ThrowsAsync<ArgumentException>(
            () => LifecycleConsoleTool.SetAdminPassword(client, Logger(), ""));

        // Nothing was sent — an empty admin password must never reach the server.
        Assert.Null(client.LastSetAdminPassword);
    }

    [Fact]
    public async Task SetAdminPassword_ResultDescribesSessionRotation()
    {
        var client = new FakeDedicatedServerApiClient();

        SetPasswordResult result = await LifecycleConsoleTool.SetAdminPassword(client, Logger(), "admin-secret");

        Assert.Contains("re-authenticated", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetAdminPassword_PasswordNeverAppearsInLogsOrResult()
    {
        const string secret = "Adm1n-Rotate-P@ss-CAFEBABE";
        var client = new FakeDedicatedServerApiClient();
        var logger = Logger();

        SetPasswordResult result = await LifecycleConsoleTool.SetAdminPassword(client, logger, secret);

        Assert.DoesNotContain(secret, logger.AllText);
        Assert.DoesNotContain(secret, result.Message);
        // The minted bearer token is also a secret: it must never be logged or surfaced in the result.
        string mintedToken = client.LastSetAdminPassword!.AuthenticationToken;
        Assert.DoesNotContain(mintedToken, logger.AllText);
        Assert.DoesNotContain(mintedToken, result.Message);
    }

    [Fact]
    public async Task SetAdminPassword_OnFailure_PasswordAndTokenNeverAppearInLogsOrException()
    {
        const string secret = "Adm1n-Rotate-P@ss-CAFEBABE";
        var client = new FakeDedicatedServerApiClient
        {
            ThrowOnSetAdminPassword =
                new DedicatedServerApiException("insufficient_privilege", "Not allowed", httpStatusCode: 403),
        };
        var logger = Logger();

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => LifecycleConsoleTool.SetAdminPassword(client, logger, secret));

        string mintedToken = client.LastSetAdminPassword!.AuthenticationToken;
        Assert.DoesNotContain(secret, logger.AllText);
        Assert.DoesNotContain(secret, ex.ToString());
        Assert.DoesNotContain(mintedToken, logger.AllText);
        Assert.DoesNotContain(mintedToken, ex.ToString());
    }
}
