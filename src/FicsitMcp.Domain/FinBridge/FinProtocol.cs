namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Fixed constants of the FIN bridge wire contract (ADR-001). These are protocol-level facts, not
/// operator-tunable knobs (those live on <c>FinBridgeOptions</c>).
/// </summary>
public static class FinProtocol
{
    /// <summary>The protocol version this server speaks. Integer, starts at 1 (ADR-001 Decision 6).</summary>
    public const int Version = 1;

    /// <summary>Lowest agent protocol version the server can serve. Inclusive.</summary>
    public const int MinSupportedVersion = 1;

    /// <summary>Highest agent protocol version the server can serve. Inclusive.</summary>
    public const int MaxSupportedVersion = 1;

    /// <summary>HTTP header carrying the shared token on every inbound request (ADR-001 Decision 4).</summary>
    public const string TokenHeader = "X-FIN-Token";

    /// <summary>Path of the boot handshake endpoint.</summary>
    public const string HelloPath = "/fin/v1/hello";

    /// <summary>Path of the long-poll endpoint (commands down, results+events up).</summary>
    public const string PollPath = "/fin/v1/poll";

    /// <summary>True when the server can serve an agent reporting <paramref name="agentVersion"/>.</summary>
    public static bool IsSupportedVersion(int agentVersion)
        => agentVersion is >= MinSupportedVersion and <= MaxSupportedVersion;
}
