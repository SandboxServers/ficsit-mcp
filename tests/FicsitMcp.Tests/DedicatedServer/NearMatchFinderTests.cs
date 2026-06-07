using FicsitMcp.Domain.DedicatedServer.SaveManagement;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Unit tests for <see cref="NearMatchFinder"/> — the deterministic matcher that powers not-found
/// suggestions. Each test name states the property proven.
/// </summary>
public sealed class NearMatchFinderTests
{
    [Fact]
    public void ContainsExact_IgnoresCase()
    {
        Assert.True(NearMatchFinder.ContainsExact(["MySave"], "mysave"));
        Assert.False(NearMatchFinder.ContainsExact(["MySave"], "OtherSave"));
    }

    [Fact]
    public void FindNearMatches_OnEmptyCandidateList_ReturnsEmpty()
    {
        Assert.Empty(NearMatchFinder.FindNearMatches([], "anything"));
    }

    [Fact]
    public void FindNearMatches_RanksSubstringHitsAboveEditDistanceHits()
    {
        // "Factory" is a substring of "MyFactory"; "Faktory" is one edit from "Factory" (the query).
        IReadOnlyList<string> matches = NearMatchFinder.FindNearMatches(["MyFactory", "Faktory"], "Factory");

        Assert.Equal("MyFactory", matches[0]);
        Assert.Contains("Faktory", matches);
    }

    [Fact]
    public void FindNearMatches_SuggestsSingleCharacterTypo()
    {
        IReadOnlyList<string> matches = NearMatchFinder.FindNearMatches(["Main", "Backup"], "Maine");

        Assert.Equal(["Main"], matches);
    }

    [Fact]
    public void FindNearMatches_OmitsUnrelatedNames()
    {
        IReadOnlyList<string> matches = NearMatchFinder.FindNearMatches(["Wasteland", "Glacier"], "Forest");

        Assert.Empty(matches);
    }

    [Fact]
    public void FindNearMatches_CapsAtMaxSuggestions()
    {
        string[] candidates = ["save0", "save1", "save2", "save3", "save4", "save5", "save6"];

        IReadOnlyList<string> matches = NearMatchFinder.FindNearMatches(candidates, "save");

        Assert.Equal(NearMatchFinder.MaxSuggestions, matches.Count);
    }

    [Fact]
    public void FindNearMatches_CollapsesCaseInsensitiveDuplicates_KeepingFirstCasing()
    {
        IReadOnlyList<string> matches = NearMatchFinder.FindNearMatches(["Main", "MAIN", "main"], "Maine");

        Assert.Equal(["Main"], matches);
    }
}
