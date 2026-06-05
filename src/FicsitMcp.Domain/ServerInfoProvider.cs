using System.Reflection;
using System.Runtime.InteropServices;

namespace FicsitMcp.Domain;

/// <summary>
/// Default <see cref="IServerInfoProvider"/>: reports a fixed server name plus the
/// version of a supplied assembly and the current runtime description.
/// </summary>
/// <remarks>
/// The assembly and runtime description are constructor parameters (with
/// production defaults) so the values are deterministic in unit tests instead of
/// depending on whatever happens to be loaded.
/// </remarks>
public sealed class ServerInfoProvider : IServerInfoProvider
{
    /// <summary>The server name reported to MCP clients.</summary>
    public const string ServerName = "ficsit-mcp";

    private readonly Assembly _assembly;
    private readonly string _runtimeDescription;

    /// <summary>
    /// Creates a provider. Defaults to the host's entry assembly (falling back to
    /// this assembly) and the live runtime description; both can be overridden for
    /// deterministic testing.
    /// </summary>
    public ServerInfoProvider(Assembly? assembly = null, string? runtimeDescription = null)
    {
        _assembly = assembly ?? Assembly.GetEntryAssembly() ?? typeof(ServerInfoProvider).Assembly;
        _runtimeDescription = runtimeDescription ?? RuntimeInformation.FrameworkDescription;
    }

    /// <inheritdoc />
    public ServerInfo GetServerInfo()
    {
        string version =
            _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? _assembly.GetName().Version?.ToString()
            ?? "unknown";

        return new ServerInfo(ServerName, version, _runtimeDescription);
    }
}
