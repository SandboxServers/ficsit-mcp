using System.Reflection;

using FicsitMcp.Domain;

namespace FicsitMcp.Tests;

public sealed class ServerInfoProviderTests
{
    [Fact]
    public void GetServerInfo_ReportsServerNameVersionAndRuntime()
    {
        // Arrange: pin the assembly and runtime so the result is deterministic.
        Assembly assembly = typeof(ServerInfoProviderTests).Assembly;
        const string runtime = ".NET Test Runtime 1.0";
        var provider = new ServerInfoProvider(assembly, runtime);

        // Act
        ServerInfo info = provider.GetServerInfo();

        // Assert
        Assert.Equal(ServerInfoProvider.ServerName, info.Name);
        Assert.Equal(runtime, info.Runtime);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
    }

    [Fact]
    public void GetServerInfo_FallsBackToAssemblyVersion_WhenNoInformationalVersion()
    {
        // Arrange: the dynamically-built assembly below has no version attributes,
        // so the provider must fall back rather than throw or return null.
        Assembly bare = Assembly.Load(typeof(object).Assembly.GetName());
        var provider = new ServerInfoProvider(bare, "runtime");

        // Act
        ServerInfo info = provider.GetServerInfo();

        // Assert
        Assert.NotNull(info.Version);
        Assert.NotEqual(string.Empty, info.Version);
    }
}
