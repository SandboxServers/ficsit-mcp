namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Thrown when an agent reports a protocol version the server cannot serve (ADR-001 Decision 6).
/// The host maps this to HTTP 426 with a typed <see cref="FinError"/> body naming the supported
/// range, so the agent can print an actionable in-game message. The server never silently downgrades
/// a mutation path on a version it does not fully understand.
/// </summary>
public sealed class ProtocolVersionMismatchException : Exception
{
    /// <summary>Builds the mismatch from the agent's reported version and the server's range.</summary>
    public ProtocolVersionMismatchException(int agentVersion, int serverMin, int serverMax)
        : base($"Agent speaks protocol {agentVersion}; server supports {serverMin}-{serverMax}. " +
               "Update the FIN agent script.")
    {
        AgentVersion = agentVersion;
        ServerSupportedMin = serverMin;
        ServerSupportedMax = serverMax;
    }

    /// <summary>The protocol version the agent reported.</summary>
    public int AgentVersion { get; }

    /// <summary>Lowest protocol version the server supports.</summary>
    public int ServerSupportedMin { get; }

    /// <summary>Highest protocol version the server supports.</summary>
    public int ServerSupportedMax { get; }
}
