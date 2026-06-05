namespace FicsitMcp.Domain;

/// <summary>
/// Identifying information about the running MCP server: which build is live and
/// what runtime it is hosted on. Used by the <c>server_info</c> tool to prove the
/// host/transport pipeline end to end.
/// </summary>
/// <param name="Name">Human-readable server name.</param>
/// <param name="Version">Informational version of the host assembly.</param>
/// <param name="Runtime">The .NET runtime description the server is running on.</param>
public sealed record ServerInfo(string Name, string Version, string Runtime);
