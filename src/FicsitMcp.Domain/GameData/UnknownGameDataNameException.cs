using System.Collections.Immutable;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Thrown when a class name or display name cannot be resolved to a known item,
/// recipe, or building. Carries the offending query and the closest known names so the
/// caller (and the model driving it) can recover without a second blind guess.
/// </summary>
/// <remarks>
/// Per the project's "fail loudly" rule, lookups never silently return null or an empty
/// result for an unrecognised name: an unknown name is almost always a typo or a stale
/// class reference, and surfacing near-matches turns a dead end into a self-correcting one.
/// </remarks>
public sealed class UnknownGameDataNameException : Exception
{
    /// <summary>The query that failed to resolve.</summary>
    public string Query { get; }

    /// <summary>What kind of entity was being looked up (e.g. "item", "recipe").</summary>
    public string EntityKind { get; }

    /// <summary>Closest known names, ranked best-first, for the caller to retry with.</summary>
    public ImmutableArray<string> NearMatches { get; }

    /// <summary>Creates the exception with an actionable, near-match-listing message.</summary>
    public UnknownGameDataNameException(string query, string entityKind, ImmutableArray<string> nearMatches)
        : base(BuildMessage(query, entityKind, nearMatches))
    {
        Query = query;
        EntityKind = entityKind;
        NearMatches = nearMatches;
    }

    private static string BuildMessage(string query, string entityKind, ImmutableArray<string> nearMatches)
    {
        if (nearMatches.IsDefaultOrEmpty)
        {
            return $"No {entityKind} found matching '{query}'. " +
                   $"Use the exact class name (e.g. Desc_IronPlate_C) or display name (e.g. Iron Plate).";
        }

        return $"No {entityKind} found matching '{query}'. Did you mean: {string.Join(", ", nearMatches)}?";
    }
}
