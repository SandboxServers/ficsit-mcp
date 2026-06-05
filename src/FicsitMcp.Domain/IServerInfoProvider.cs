namespace FicsitMcp.Domain;

/// <summary>
/// Supplies identifying information about the running server. Abstracted behind an
/// interface so the MCP tool depends on a contract (and so it can be substituted
/// in tests) rather than reaching for static reflection/runtime globals directly.
/// </summary>
public interface IServerInfoProvider
{
    /// <summary>Gets the current server's name, version, and host runtime.</summary>
    ServerInfo GetServerInfo();
}
