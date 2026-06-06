using FicsitMcp.Domain.Http;

namespace FicsitMcp.Tests.Http;

public sealed class FileCertificatePinStoreTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "ficsit-mcp-tests", Guid.NewGuid().ToString("N"));

    private string PinPath => Path.Combine(_tempDir, "cert-pins.json");

    [Fact]
    public void GetPinned_ReturnsNull_WhenNothingPinned()
    {
        // Arrange
        var store = new FileCertificatePinStore(PinPath);

        // Act + Assert
        Assert.Null(store.GetPinned("host.local"));
    }

    [Fact]
    public void Pin_ThenGetPinned_RoundTripsInMemory()
    {
        // Arrange
        var store = new FileCertificatePinStore(PinPath);

        // Act
        store.Pin("host.local", "AABBCC");

        // Assert
        Assert.Equal("AABBCC", store.GetPinned("host.local"));
    }

    [Fact]
    public void Pin_PersistsAcrossInstances()
    {
        // Arrange: pin with one store instance...
        var first = new FileCertificatePinStore(PinPath);
        first.Pin("host.local", "DEADBEEF");

        // Act: ...a fresh instance reads the same file (simulating a process restart).
        var second = new FileCertificatePinStore(PinPath);

        // Assert
        Assert.Equal("DEADBEEF", second.GetPinned("host.local"));
    }

    [Fact]
    public void GetPinned_IsCaseInsensitive_ForHostAndThumbprint()
    {
        // Arrange: store canonicalizes host (lower) and thumbprint (upper, no separators).
        var store = new FileCertificatePinStore(PinPath);
        store.Pin("Host.Local", "aa:bb:cc");

        // Act + Assert: a differently-cased host lookup still hits, and the value is canonical.
        Assert.Equal("AABBCC", store.GetPinned("HOST.LOCAL"));
    }

    [Fact]
    public void GetOrPin_ReturnsExistingPin_WhenSecondThumbprintOffered()
    {
        // Arrange: this is the lost-race scenario — two first-contacts offer different thumbprints.
        var store = new FileCertificatePinStore(PinPath);

        // Act: the first GetOrPin stores "AABBCC"; the second offers a DIFFERENT thumbprint but must
        // get back the value that won the race (the first one), not overwrite it.
        string first = store.GetOrPin("host.local", "AABBCC");
        string second = store.GetOrPin("host.local", "DDEEFF");

        // Assert: both calls agree on the first writer's value; the store was not clobbered.
        Assert.Equal("AABBCC", first);
        Assert.Equal("AABBCC", second);
        Assert.Equal("AABBCC", store.GetPinned("host.local"));
    }

    [Fact]
    public void GetOrPin_NormalizesThumbprint_OnFirstStore()
    {
        // Arrange
        var store = new FileCertificatePinStore(PinPath);

        // Act: separators/casing are canonicalized just like Pin does.
        string effective = store.GetOrPin("host.local", "aa:bb:cc");

        // Assert
        Assert.Equal("AABBCC", effective);
    }

    [Fact]
    public void HostKey_WithPort_IsHandledAndCaseInsensitive()
    {
        // Arrange: pins are now keyed by authority (host:port). The store must normalize a
        // "host:port" key (lower-casing the host part while preserving the port).
        var store = new FileCertificatePinStore(PinPath);
        store.Pin("Host.Local:7777", "AABBCC");

        // Act + Assert: a differently-cased authority still hits; a different port does NOT.
        Assert.Equal("AABBCC", store.GetPinned("HOST.LOCAL:7777"));
        Assert.Null(store.GetPinned("host.local:8888"));
    }

    [Fact]
    public void Load_TreatsCorruptFileAsEmpty_InsteadOfThrowing()
    {
        // Arrange: a garbage pin file must not crash the host on startup.
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(PinPath, "{ this is not valid json");

        // Act: constructing the store must not throw...
        var store = new FileCertificatePinStore(PinPath);

        // Assert: ...and it behaves as "no pins yet", re-pinning on next contact.
        Assert.Null(store.GetPinned("host.local"));
        store.Pin("host.local", "RECOVERED");
        Assert.Equal("RECOVERED", store.GetPinned("host.local"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
