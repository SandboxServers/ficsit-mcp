namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Thrown when game data cannot be loaded: the shipped snapshot is missing/corrupt, or
/// a user-supplied Docs.json override is unreadable or malformed. Messages always name
/// the offending path/source so an operator can fix the configuration rather than face
/// a crash loop with no cause.
/// </summary>
public sealed class GameDataLoadException : Exception
{
    /// <summary>Creates the exception with an actionable message.</summary>
    public GameDataLoadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception wrapping an underlying cause.</summary>
    public GameDataLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
