using System.ComponentModel;

using FicsitMcp.Domain;

using ModelContextProtocol.Server;

namespace FicsitMcp.Tools;

/// <summary>
/// Placeholder MCP tool that proves the host/transport/DI pipeline end to end.
/// Real Satisfactory surface tools will follow the same shape: a thin tool method
/// that delegates to an injected Domain service.
/// </summary>
[McpServerToolType]
public sealed class ServerInfoTool
{
    /// <summary>
    /// Reports the running server's name, version, and host runtime. Read-only,
    /// idempotent, and closed-world: it inspects the local process only and never
    /// mutates anything, so the matching behavioral hints are set.
    /// </summary>
    [McpServerTool(Name = "server_info", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Returns identifying information about this MCP server: its name, build version, and the .NET runtime it is running on.")]
    public static ServerInfo ServerInfo(
        [Description("Provides the server's name, version, and runtime.")] IServerInfoProvider provider)
        => provider.GetServerInfo();
}
