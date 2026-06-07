namespace FicsitMcp.Domain.DedicatedServer.SaveManagement;

/// <summary>
/// Raised when a destructive save operation (load, delete, rollback) names a save or session that
/// does not exist on the server. This is thrown by the backing service BEFORE any destructive client
/// call is made, so a typo can never reach <c>LoadGame</c>/<c>DeleteSaveFile</c>/<c>DeleteSaveSession</c>.
/// </summary>
/// <remarks>
/// The exception carries the closest existing names (<see cref="NearMatches"/>) computed from the live
/// <c>EnumerateSessions</c> list, so the model can self-correct and retry with a valid name in a single
/// round trip rather than blindly re-guessing. The <see cref="Exception.Message"/> already embeds those
/// suggestions in prose for surfaces that only see the message text.
/// </remarks>
public sealed class SaveNotFoundException : Exception
{
    /// <summary>The kind of name that was not found ("save" or "session"), for the message and callers.</summary>
    public string Kind { get; }

    /// <summary>The name the caller asked for that did not exist.</summary>
    public string RequestedName { get; }

    /// <summary>
    /// The closest existing names, best-first, or empty when the server has none of that kind at all.
    /// Suggested so the caller can retry with a corrected name without another discovery round trip.
    /// </summary>
    public IReadOnlyList<string> NearMatches { get; }

    /// <summary>Creates a not-found error carrying near-match suggestions.</summary>
    public SaveNotFoundException(string kind, string requestedName, IReadOnlyList<string> nearMatches)
        : base(BuildMessage(kind, requestedName, nearMatches))
    {
        Kind = kind;
        RequestedName = requestedName;
        NearMatches = nearMatches;
    }

    private static string BuildMessage(string kind, string requestedName, IReadOnlyList<string> nearMatches)
    {
        if (nearMatches.Count == 0)
        {
            return $"No {kind} named '{requestedName}' exists on the server, and there are no {kind}s to "
                + $"suggest. Use list_sessions to see what is available.";
        }

        string suggestions = string.Join(", ", nearMatches.Select(static n => $"'{n}'"));
        return $"No {kind} named '{requestedName}' exists on the server. Closest matches: {suggestions}. "
            + "Retry with one of these exact names, or use list_sessions to see all options.";
    }
}
