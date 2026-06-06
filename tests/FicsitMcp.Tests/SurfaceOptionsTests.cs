using System.Text.Json;

using FicsitMcp.Configuration;
using FicsitMcp.Domain.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FicsitMcp.Tests;

public sealed class SurfaceOptionsTests
{
    // Builds a service provider exactly the way Program.cs does: an in-memory base config
    // standing in for appsettings.json, then a FICSITMCP_-prefixed env layer that must win.
    private static ServiceProvider BuildProvider(
        IDictionary<string, string?>? appsettings = null,
        IDictionary<string, string?>? prefixedEnv = null)
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettings ?? new Dictionary<string, string?>());

        if (prefixedEnv is not null)
        {
            // The real env source strips the FICSITMCP_ prefix; mirror that by adding the
            // already-stripped keys here so the test exercises the same precedence ordering.
            configBuilder.AddInMemoryCollection(prefixedEnv);
        }

        IConfiguration configuration = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddSurfaceOptions(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Binding_PopulatesDedicatedServerOptions_FromConfiguration()
    {
        // Arrange
        var appsettings = new Dictionary<string, string?>
        {
            ["DedicatedServer:BaseUrl"] = "https://127.0.0.1:7777",
            ["DedicatedServer:AdminToken"] = "super-secret-token",
            ["DedicatedServer:DangerousAcceptAnyCert"] = "true",
        };
        using ServiceProvider provider = BuildProvider(appsettings);

        // Act
        DedicatedServerOptions options = provider.GetRequiredService<IOptions<DedicatedServerOptions>>().Value;

        // Assert
        Assert.True(options.IsConfigured);
        Assert.Equal("https://127.0.0.1:7777", options.BaseUrl);
        Assert.Equal("super-secret-token", options.AdminToken.Reveal());
        Assert.True(options.DangerousAcceptAnyCert);
    }

    [Fact]
    public void Binding_ParsesFrmTransportMode_FromString()
    {
        // Arrange
        var appsettings = new Dictionary<string, string?>
        {
            ["Frm:BaseUrl"] = "http://127.0.0.1:8080",
            ["Frm:TransportMode"] = "DedicatedApiPassthrough",
        };
        using ServiceProvider provider = BuildProvider(appsettings);

        // Act
        FrmOptions options = provider.GetRequiredService<IOptions<FrmOptions>>().Value;

        // Assert
        Assert.Equal(FrmTransportMode.DedicatedApiPassthrough, options.TransportMode);
    }

    [Fact]
    public void Frm_DefaultsToDirectTransport_WhenUnspecified()
    {
        // Arrange
        var appsettings = new Dictionary<string, string?>
        {
            ["Frm:BaseUrl"] = "http://127.0.0.1:8080",
        };
        using ServiceProvider provider = BuildProvider(appsettings);

        // Act
        FrmOptions options = provider.GetRequiredService<IOptions<FrmOptions>>().Value;

        // Assert
        Assert.Equal(FrmTransportMode.Direct, options.TransportMode);
    }

    [Fact]
    public void EnvVars_OverrideAppsettings()
    {
        // Arrange: appsettings says one URL; the FICSITMCP_ env layer says another.
        var appsettings = new Dictionary<string, string?>
        {
            ["Frm:BaseUrl"] = "http://from-appsettings:8080",
        };
        var prefixedEnv = new Dictionary<string, string?>
        {
            ["Frm:BaseUrl"] = "http://from-env:9090",
        };
        using ServiceProvider provider = BuildProvider(appsettings, prefixedEnv);

        // Act
        FrmOptions options = provider.GetRequiredService<IOptions<FrmOptions>>().Value;

        // Assert: env wins.
        Assert.Equal("http://from-env:9090", options.BaseUrl);
    }

    [Fact]
    public void Validation_Fails_OnMalformedUrl()
    {
        // Arrange
        var appsettings = new Dictionary<string, string?>
        {
            ["DedicatedServer:BaseUrl"] = "not-a-valid-url",
            ["DedicatedServer:AdminToken"] = "token",
        };
        using ServiceProvider provider = BuildProvider(appsettings);
        IOptions<DedicatedServerOptions> options = provider.GetRequiredService<IOptions<DedicatedServerOptions>>();

        // Act + Assert: accessing .Value triggers DataAnnotations validation.
        OptionsValidationException ex = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("BaseUrl", string.Join(" ", ex.Failures), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_Fails_WhenConfiguredSurfaceHasNoCredential()
    {
        // Arrange: URL present (surface active) but no admin token.
        var appsettings = new Dictionary<string, string?>
        {
            ["DedicatedServer:BaseUrl"] = "https://127.0.0.1:7777",
        };
        using ServiceProvider provider = BuildProvider(appsettings);
        IOptions<DedicatedServerOptions> options = provider.GetRequiredService<IOptions<DedicatedServerOptions>>();

        // Act + Assert
        OptionsValidationException ex = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("admin token", string.Join(" ", ex.Failures), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnconfiguredSurface_ValidatesCleanly()
    {
        // Arrange: nothing configured at all — every surface must stay dormant, not error.
        using ServiceProvider provider = BuildProvider();

        // Act
        DedicatedServerOptions dedicated = provider.GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
        FrmOptions frm = provider.GetRequiredService<IOptions<FrmOptions>>().Value;
        FinBridgeOptions fin = provider.GetRequiredService<IOptions<FinBridgeOptions>>().Value;

        // Assert: no exception, and each reports itself unconfigured.
        Assert.False(dedicated.IsConfigured);
        Assert.False(frm.IsConfigured);
        Assert.False(fin.IsConfigured);
    }

    [Fact]
    public void OneSurfaceConfigured_DoesNotBreakTheOthers()
    {
        // Arrange: only FRM is configured; the other two stay dormant.
        var appsettings = new Dictionary<string, string?>
        {
            ["Frm:BaseUrl"] = "http://127.0.0.1:8080",
        };
        using ServiceProvider provider = BuildProvider(appsettings);

        // Act
        FrmOptions frm = provider.GetRequiredService<IOptions<FrmOptions>>().Value;
        DedicatedServerOptions dedicated = provider.GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
        FinBridgeOptions fin = provider.GetRequiredService<IOptions<FinBridgeOptions>>().Value;

        // Assert
        Assert.True(frm.IsConfigured);
        Assert.False(dedicated.IsConfigured);
        Assert.False(fin.IsConfigured);
    }

    [Fact]
    public void Require_Throws_ActionableMessage_NamingExactEnvVar()
    {
        // Arrange
        var frm = new FrmOptions();

        // Act
        SurfaceNotConfiguredException ex = Assert.Throws<SurfaceNotConfiguredException>(() => frm.Require());

        // Assert: message names the surface and the exact env var, per the issue.
        Assert.Equal("FRM endpoint", ex.SurfaceName);
        Assert.Equal("FICSITMCP_Frm__BaseUrl", ex.ActivatingEnvVar);
        Assert.Equal("FRM endpoint not configured; set FICSITMCP_Frm__BaseUrl", ex.Message);
    }

    [Fact]
    public void Require_ReturnsSurface_WhenConfigured()
    {
        // Arrange
        var frm = new FrmOptions { BaseUrl = "http://127.0.0.1:8080" };

        // Act
        FrmOptions result = frm.Require();

        // Assert
        Assert.Same(frm, result);
    }

    [Fact]
    public void ValidateOnStart_FailsFast_ForInvalidConfig()
    {
        // Arrange: a malformed dedicated-server URL must be caught at startup, not deferred
        // to the first tool call. IStartupValidator is what the host invokes during StartAsync.
        var appsettings = new Dictionary<string, string?>
        {
            ["DedicatedServer:BaseUrl"] = "::::not-a-url",
            ["DedicatedServer:AdminToken"] = "token",
        };
        using ServiceProvider provider = BuildProvider(appsettings);
        IStartupValidator validator = provider.GetRequiredService<IStartupValidator>();

        // Act + Assert
        Assert.Throws<OptionsValidationException>(validator.Validate);
    }

    [Fact]
    public void ValidateOnStart_Passes_WhenNothingConfigured()
    {
        // Arrange: the all-dormant case must survive startup validation untouched.
        using ServiceProvider provider = BuildProvider();
        IStartupValidator validator = provider.GetRequiredService<IStartupValidator>();

        // Act + Assert: does not throw.
        validator.Validate();
    }

    [Fact]
    public void Secret_ToString_NeverRevealsValue()
    {
        // Arrange
        var secret = new Secret("hunter2");

        // Act
        string rendered = secret.ToString();

        // Assert
        Assert.Equal("***", rendered);
        Assert.DoesNotContain("hunter2", rendered, StringComparison.Ordinal);
        Assert.Equal("hunter2", secret.Reveal());
    }

    [Fact]
    public void Secret_Serialization_Redacts()
    {
        // Arrange: serializing an options object must not leak the credential.
        var options = new DedicatedServerOptions
        {
            BaseUrl = "https://127.0.0.1:7777",
            AdminToken = "leak-me-if-you-can",
        };

        // Act
        string json = JsonSerializer.Serialize(options);

        // Assert
        Assert.DoesNotContain("leak-me-if-you-can", json, StringComparison.Ordinal);
        Assert.Contains("***", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_Unset_RendersAsUnset()
    {
        // Arrange
        var secret = default(Secret);

        // Act + Assert
        Assert.False(secret.HasValue);
        Assert.Null(secret.Reveal());
        Assert.Equal("(unset)", secret.ToString());
    }

    [Fact]
    public void ShippedAppsettingsJson_ValidatesCleanly_WithAllSurfacesDormant()
    {
        // Arrange: bind the REAL config file shipped with the host (linked into the test
        // output as appsettings.shipped.json). Regression test for an adversarial-review
        // finding: the shipped file's blank URL placeholders crashed startup because [Url]
        // rejects "" — in-memory test config could never catch that.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.shipped.json"))
            .Build();

        var services = new ServiceCollection();
        services.AddSurfaceOptions(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act: startup validation must accept the file exactly as shipped...
        provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert: ...and its blank placeholders must mean "dormant", not "invalid".
        Assert.False(provider.GetRequiredService<IOptions<DedicatedServerOptions>>().Value.IsConfigured);
        Assert.False(provider.GetRequiredService<IOptions<FrmOptions>>().Value.IsConfigured);
        Assert.False(provider.GetRequiredService<IOptions<FinBridgeOptions>>().Value.IsConfigured);
    }

    [Fact]
    public void BlankUrls_NormalizeToUnset_InsteadOfFailingUrlValidation()
    {
        // Arrange: blank URLs (empty env var, placeholder in a config file) must read as
        // "surface not configured" — the documented contract — not as a [Url] failure.
        var appsettings = new Dictionary<string, string?>
        {
            ["DedicatedServer:BaseUrl"] = "",
            ["Frm:BaseUrl"] = "   ",
            ["FinBridge:ListenUrl"] = "",
        };
        using ServiceProvider provider = BuildProvider(appsettings);

        // Act
        provider.GetRequiredService<IStartupValidator>().Validate();
        DedicatedServerOptions dedicated = provider.GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
        FrmOptions frm = provider.GetRequiredService<IOptions<FrmOptions>>().Value;
        FinBridgeOptions fin = provider.GetRequiredService<IOptions<FinBridgeOptions>>().Value;

        // Assert: normalized to null, all dormant, nothing thrown.
        Assert.Null(dedicated.BaseUrl);
        Assert.Null(frm.BaseUrl);
        Assert.Null(fin.ListenUrl);
        Assert.False(dedicated.IsConfigured);
        Assert.False(frm.IsConfigured);
        Assert.False(fin.IsConfigured);
    }
}
