namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Thrown by <see cref="IFinBridge.SendAsync"/> when a command cannot be delivered or its result is
/// not received. Carries the typed <see cref="FinError"/> so the tool layer can branch on the code
/// (notably the <see cref="FinErrorCode.QueuedNotPickedUp"/> vs <see cref="FinErrorCode.DeliveredNoResult"/>
/// safety distinction) and surface the actionable message verbatim.
/// </summary>
/// <remarks>
/// At-most-once: a command is NEVER auto-retried. This exception is the surfaced failure; whether
/// to reissue is an explicit operator decision, gated by the code's safety meaning.
/// </remarks>
public sealed class FinBridgeException : Exception
{
    /// <summary>Constructs the exception from a typed bridge error.</summary>
    public FinBridgeException(FinError error)
        : base(error.Message)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>The typed error: code, actionable message, and any structured details.</summary>
    public FinError Error { get; }

    /// <summary>Shorthand for the error's typed code.</summary>
    public FinErrorCode Code => Error.Code;
}
