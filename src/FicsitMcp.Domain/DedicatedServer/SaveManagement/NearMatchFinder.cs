namespace FicsitMcp.Domain.DedicatedServer.SaveManagement;

/// <summary>
/// Finds the closest existing names to a mistyped one, so a not-found error can suggest corrections
/// instead of failing blankly. Pure and deterministic — no I/O — so the matching is unit-testable in
/// isolation from the API client.
/// </summary>
/// <remarks>
/// Ranking (best-first), case-insensitive throughout:
/// <list type="number">
///   <item><description>Candidates that <b>contain</b> the query (or vice-versa) as a substring — a
///     partial-name typo like "factory" matching "MyFactory_autosave_0".</description></item>
///   <item><description>Candidates within a length-relative <b>Levenshtein edit distance</b> — a small
///     typo like "Maine" → "Main". The threshold scales with the query length so short names tolerate
///     1 edit and long names a few, but a totally unrelated name is never suggested.</description></item>
/// </list>
/// Ties break by smaller edit distance, then by ordinal name order for determinism. At most
/// <see cref="MaxSuggestions"/> names are returned.
/// </remarks>
public static class NearMatchFinder
{
    /// <summary>The maximum number of suggestions returned, to keep an error message scannable.</summary>
    public const int MaxSuggestions = 5;

    /// <summary>
    /// True when <paramref name="candidates"/> contains <paramref name="query"/> by an
    /// ORDINAL-CASE-INSENSITIVE comparison. This is the exact-match test the destructive-op validation
    /// uses; the server may sanitize names but matching is otherwise exact.
    /// </summary>
    public static bool ContainsExact(IEnumerable<string> candidates, string query)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(query);
        return candidates.Any(c => string.Equals(c, query, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the closest existing names to <paramref name="query"/>, best-first, capped at
    /// <see cref="MaxSuggestions"/>. Returns an empty list when nothing is close enough (or there are no
    /// candidates). Duplicates in the input are collapsed; the original casing of each kept name is
    /// preserved in the output.
    /// </summary>
    public static IReadOnlyList<string> FindNearMatches(IEnumerable<string> candidates, string query)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(query);

        // Collapse case-insensitive duplicates, keeping first-seen casing and order.
        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string c in candidates)
        {
            if (c is not null && seen.Add(c))
            {
                distinct.Add(c);
            }
        }

        int threshold = DistanceThreshold(query.Length);

        var ranked = new List<(string Name, int Tier, int Distance, int Index)>();
        for (int i = 0; i < distinct.Count; i++)
        {
            string name = distinct[i];
            bool substring =
                name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (query.Length > 0 && query.Contains(name, StringComparison.OrdinalIgnoreCase));
            int distance = Levenshtein(name, query);

            if (substring)
            {
                // Tier 0: substring hits rank above pure edit-distance hits.
                ranked.Add((name, 0, distance, i));
            }
            else if (distance <= threshold)
            {
                ranked.Add((name, 1, distance, i));
            }
        }

        return ranked
            .OrderBy(static r => r.Tier)
            .ThenBy(static r => r.Distance)
            .ThenBy(static r => r.Index)
            .Take(MaxSuggestions)
            .Select(static r => r.Name)
            .ToList();
    }

    // Allow more edits for longer queries (roughly a third of the length), but at least 1 for short
    // names so a single-character typo is always caught, and never so loose that unrelated names match.
    private static int DistanceThreshold(int queryLength) => Math.Max(1, queryLength / 3);

    // Standard iterative Levenshtein distance with a single rolling row (O(n) space). Comparison is
    // case-insensitive via lowercasing each character, matching the rest of the matcher.
    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            char ai = char.ToLowerInvariant(a[i - 1]);
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = ai == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
