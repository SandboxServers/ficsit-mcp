using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Drives the repeatable snapshot-refresh procedure. It is the regeneration entry point
/// documented in <c>GameData/README.md</c> but is SKIPPED unless an operator opts in by
/// pointing <c>FICSITMCP_REGEN_DOCS</c> at a live Satisfactory Docs.json — so CI (which
/// has no game install) never runs it and never fails on it.
/// </summary>
public sealed class SnapshotGeneratorTests
{
    private const string RegenEnvVar = "FICSITMCP_REGEN_DOCS";

    [Fact]
    public void Regenerate_ShippedSnapshot_FromLiveDocs_WhenOptedIn()
    {
        string? docsPath = Environment.GetEnvironmentVariable(RegenEnvVar);
        if (string.IsNullOrWhiteSpace(docsPath) || !File.Exists(docsPath))
        {
            // Opt-in only. With no live Docs.json this is a no-op pass, so CI (no game
            // install) never runs the regeneration and never fails on it. Set
            // FICSITMCP_REGEN_DOCS to a Satisfactory Docs.json (UTF-16) to regenerate.
            return;
        }

        // Walk up from the test output dir to the repo's Domain project asset location.
        string outputPath = LocateShippedSnapshot();

        GameDataSnapshot snapshot = SnapshotGenerator.Regenerate(docsPath!, outputPath);

        // Sanity-gate the regenerated data with the headline golden rate before it is kept.
        var service = new GameDataService(snapshot);
        GameRecipe plate = service.GetRecipe("Recipe_IronPlate_C");
        Assert.Equal(20.0, plate.Products.Single(p => p.ItemClassName == "Desc_IronPlate_C").AmountPerMinute, precision: 6);
    }

    private static string LocateShippedSnapshot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName, "src", "FicsitMcp.Domain", "GameData", "game-data.v1.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate src/FicsitMcp.Domain/GameData/game-data.v1.json above the test output directory.");
    }
}
