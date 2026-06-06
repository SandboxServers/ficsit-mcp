using System.Collections.Concurrent;
using System.Text.Json;

namespace FicsitMcp.Domain.Http;

/// <summary>
/// JSON-file-backed <see cref="ICertificatePinStore"/>. Pins persist across runs so a server's
/// self-signed certificate, once trusted, stays trusted until it actually changes.
/// </summary>
/// <remarks>
/// <para>
/// Default location is <c>%LocalAppData%/ficsit-mcp/cert-pins.json</c>
/// (<see cref="Environment.SpecialFolder.LocalApplicationData"/>). That directory is chosen over
/// the content root because the published host may live in a read-only directory, whereas the
/// per-user app-data path is always writable and is not committed to source control (a pin file
/// is host-specific local state, not configuration).
/// </para>
/// <para>
/// The in-memory map is the source of truth during a run; each <see cref="Pin"/> rewrites the
/// whole file under a lock. The set of pinned hosts is tiny (one per configured surface), so a
/// full rewrite is cheaper and simpler than an incremental format.
/// </para>
/// </remarks>
public sealed class FileCertificatePinStore : ICertificatePinStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly ConcurrentDictionary<string, string> _pins;
    private readonly object _writeLock = new();

    /// <summary>
    /// Creates a store backed by <paramref name="pinFilePath"/>, loading any existing pins.
    /// Pass a custom path in tests; production resolves the LocalApplicationData default via
    /// <see cref="DefaultPinFilePath"/>.
    /// </summary>
    public FileCertificatePinStore(string pinFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pinFilePath);
        PinFilePath = pinFilePath;
        _pins = Load(pinFilePath);
    }

    /// <inheritdoc />
    public string PinFilePath { get; }

    /// <summary>The default per-user pin-store path under LocalApplicationData.</summary>
    public static string DefaultPinFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ficsit-mcp",
        "cert-pins.json");

    /// <inheritdoc />
    public string? GetPinned(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        return _pins.GetValueOrDefault(NormalizeHost(host));
    }

    /// <inheritdoc />
    public void Pin(string host, string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        string key = NormalizeHost(host);
        string value = NormalizeThumbprint(thumbprint);

        // Serialize writers so concurrent first-contacts can't interleave file rewrites.
        lock (_writeLock)
        {
            _pins[key] = value;
            Persist();
        }
    }

    // Hosts are case-insensitive; store a single canonical form so "Host" and "host" match.
    private static string NormalizeHost(string host) => host.Trim().ToLowerInvariant();

    // Thumbprints are compared case-insensitively and without separators; canonicalize on store.
    private static string NormalizeThumbprint(string thumbprint) =>
        thumbprint.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

    private static ConcurrentDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ConcurrentDictionary<string, string>();
        }

        try
        {
            string json = File.ReadAllText(path);
            Dictionary<string, string>? data =
                JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return data is null
                ? new ConcurrentDictionary<string, string>()
                : new ConcurrentDictionary<string, string>(data);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable pin file must not crash the host. Treat it as "no pins
            // yet": the next contact re-pins. This is safe because a missing pin only ever
            // triggers the (logged) first-contact path, never a silent downgrade of an existing pin.
            return new ConcurrentDictionary<string, string>();
        }
    }

    private void Persist()
    {
        string? directory = Path.GetDirectoryName(PinFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Snapshot under the same lock the caller holds, then write atomically via a temp file
        // + move so a crash mid-write can't leave a half-written (corrupt) pin store.
        string json = JsonSerializer.Serialize(
            new Dictionary<string, string>(_pins),
            SerializerOptions);
        string tempPath = PinFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, PinFilePath, overwrite: true);
    }
}
