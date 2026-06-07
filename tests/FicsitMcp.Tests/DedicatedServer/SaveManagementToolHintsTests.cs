using System.Reflection;

using FicsitMcp.Domain.DedicatedServer.SaveManagement;
using FicsitMcp.Tools;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Verifies that each <see cref="SaveManagementTool"/> method projects the EXACT behavioral hints and
/// description-leading-consequence wording onto the MCP protocol surface (<see cref="Tool"/>). The MCP
/// SDK's <see cref="McpServerTool.Create(MethodInfo, object?, McpServerToolCreateOptions?)"/> builds
/// the same <see cref="Tool"/> the server advertises in <c>tools/list</c>, so asserting against it is a
/// self-contained stand-in for the tool-list schema snapshot: a hint regression (e.g. a destructive
/// tool silently losing <c>DestructiveHint</c>, which would mislead the model) fails this test.
/// </summary>
public sealed class SaveManagementToolHintsTests
{
    private static Tool ProtocolToolFor(string methodName)
    {
        MethodInfo method = typeof(SaveManagementTool)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"No public static method {methodName} on SaveManagementTool.");

        // Supply the same DI service the host wires so the SDK recognises the ISaveManagementService
        // parameter as injected (and excludes it from the model-facing input schema), exactly as
        // WithToolsFromAssembly does in production.
        IServiceProvider services = new ServiceCollection()
            .AddSingleton<ISaveManagementService>(_ => throw new InvalidOperationException(
                "The service is only used for parameter binding here, never invoked."))
            .BuildServiceProvider();

        var options = new McpServerToolCreateOptions { Services = services };
        return McpServerTool.Create(method, target: null, options).ProtocolTool;
    }

    public static TheoryData<string, string, bool, bool?, bool, bool> HintMatrix() => new()
    {
        // method,                toolName,                ReadOnly, Destructive, Idempotent, OpenWorld
        { nameof(SaveManagementTool.ListSessions),       "list_sessions",         true,  null,  true,  true },
        { nameof(SaveManagementTool.SaveGame),           "save_game",             false, null,  false, true },
        { nameof(SaveManagementTool.LoadSave),           "load_save",             false, true,  false, true },
        { nameof(SaveManagementTool.DeleteSave),         "delete_save",           false, true,  false, true },
        { nameof(SaveManagementTool.DeleteSession),      "delete_session",        false, true,  false, true },
        { nameof(SaveManagementTool.DownloadSave),       "download_save",         true,  null,  true,  true },
        { nameof(SaveManagementTool.UploadSave),         "upload_save",           false, true,  false, true },
        { nameof(SaveManagementTool.SetAutoLoadSession), "set_auto_load_session", false, null,  true,  true },
        { nameof(SaveManagementTool.RollbackTo),         "rollback_to",           false, true,  false, true },
    };

    [Theory]
    [MemberData(nameof(HintMatrix))]
    public void Tool_AdvertisesExpectedNameAndHints(
        string methodName,
        string expectedToolName,
        bool readOnly,
        bool? destructive,
        bool idempotent,
        bool openWorld)
    {
        Tool tool = ProtocolToolFor(methodName);

        Assert.Equal(expectedToolName, tool.Name);
        Assert.NotNull(tool.Annotations);
        Assert.Equal(readOnly, tool.Annotations!.ReadOnlyHint);
        Assert.Equal(destructive, tool.Annotations.DestructiveHint);
        Assert.Equal(idempotent, tool.Annotations.IdempotentHint);
        Assert.Equal(openWorld, tool.Annotations.OpenWorldHint);
    }

    [Theory]
    [InlineData(nameof(SaveManagementTool.LoadSave))]
    [InlineData(nameof(SaveManagementTool.RollbackTo))]
    public void LoadAndRollback_LeadDescriptionWithPlayerDisconnectConsequence(string methodName)
    {
        Tool tool = ProtocolToolFor(methodName);

        Assert.NotNull(tool.Description);
        string firstSentence = tool.Description!.Split('.', 2)[0];
        Assert.Contains("disconnect", firstSentence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player", firstSentence, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(SaveManagementTool.DeleteSave))]
    [InlineData(nameof(SaveManagementTool.DeleteSession))]
    public void Deletes_LeadDescriptionWithPermanenceConsequence(string methodName)
    {
        Tool tool = ProtocolToolFor(methodName);

        Assert.NotNull(tool.Description);
        string firstSentence = tool.Description!.Split('.', 2)[0];
        Assert.Contains("permanent", firstSentence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("irreversibl", firstSentence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InjectedServiceParameter_DoesNotLeakIntoModelFacingInputSchema()
    {
        // The DI-injected ISaveManagementService must NOT appear as a model-supplied argument; only
        // the real string/bool inputs (and CancellationToken, which the SDK also hides) should.
        Tool tool = ProtocolToolFor(nameof(SaveManagementTool.LoadSave));

        string schema = tool.InputSchema.GetRawText();
        Assert.DoesNotContain("saves", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ISaveManagementService", schema, StringComparison.Ordinal);
        Assert.Contains("name", schema, StringComparison.Ordinal);
    }
}
