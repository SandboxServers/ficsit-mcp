using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Parses a raw Satisfactory <c>Docs.json</c> (e.g. <c>en-US.json</c>) into an immutable
/// <see cref="GameDataSnapshot"/>. Knows the two non-obvious facts about the format:
/// the file is UTF-16 encoded, and it is structured as an array of per-native-class
/// groups (<c>{ NativeClass, Classes[] }</c>).
/// </summary>
/// <remarks>
/// This type is pure: it reads bytes/strings and produces a model. It performs all the
/// rate math (per-craft → per-minute) and unit normalisation (fluid/gas millilitres →
/// cubic metres) once, here, so the rest of the domain never re-derives rates.
/// </remarks>
public static partial class DocsJsonParser
{
    // Items live across many native-class groups; all of them carry mForm + mDisplayName.
    private static readonly string[] ItemGroupSuffixes =
    [
        "FGItemDescriptor", "FGResourceDescriptor", "FGItemDescriptorBiomass",
        "FGConsumableDescriptor", "FGItemDescriptorNuclearFuel", "FGEquipmentDescriptor",
        "FGAmmoTypeProjectile", "FGAmmoTypeSpreadshot", "FGAmmoTypeInstantHit",
        "FGVehicleDescriptor", "FGPowerShardDescriptor", "FGItemDescriptorPowerBoosterFuel",
        "FGBuildingDescriptor",
    ];

    // Production-relevant building groups (those carrying mPowerConsumption / mPowerProduction
    // that we actually model: machines, extractors, generators).
    private static readonly string[] BuildingGroupSuffixes =
    [
        "FGBuildableManufacturer", "FGBuildableManufacturerVariablePower",
        "FGBuildableResourceExtractor", "FGBuildableFrackingExtractor",
        "FGBuildableFrackingActivator", "FGBuildableWaterPump",
        "FGBuildableGeneratorFuel", "FGBuildableGeneratorNuclear",
        "FGBuildableGeneratorGeoThermal",
    ];

    // Tokens in mProducedIn that mean "made by hand", not by an automation building.
    private static readonly string[] ManualCraftMarkers =
    [
        "BP_WorkBench", "BP_Workshop", "WorkBench", "Workshop",
        "BuildGun", "FGBuildGun", "AutomatedWorkBench",
    ];

    /// <summary>
    /// Reads and parses a Docs.json file from disk. The file is UTF-16 (LE, with BOM);
    /// it is read with <see cref="Encoding.Unicode"/> so a stray UTF-8 read cannot turn
    /// the content into garbage.
    /// </summary>
    /// <param name="path">Absolute path to the Docs.json / locale file.</param>
    /// <param name="metadata">Provenance to stamp onto the resulting snapshot.</param>
    /// <exception cref="GameDataLoadException">
    /// The file is missing, unreadable, or not valid Docs.json. The message names the path.
    /// </exception>
    public static GameDataSnapshot ParseFile(string path, GameDataMetadata metadata)
    {
        string json;
        try
        {
            // UTF-16 LE with BOM. Encoding.Unicode + BOM detection handles it.
            json = File.ReadAllText(path, Encoding.Unicode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new GameDataLoadException(
                $"Could not read the game-data file at '{path}': {ex.Message}", ex);
        }

        try
        {
            return Parse(json, metadata);
        }
        catch (JsonException ex)
        {
            throw new GameDataLoadException(
                $"The game-data file at '{path}' is not valid Docs.json (expected a UTF-16 " +
                $"array of {{ NativeClass, Classes }} groups): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses an already-decoded Docs.json string. Used by <see cref="ParseFile"/> and
    /// directly by tests that embed a small slice of real docs.
    /// </summary>
    public static GameDataSnapshot Parse(string json, GameDataMetadata metadata)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new GameDataLoadException(
                "Docs.json root must be an array of { NativeClass, Classes } groups.");
        }

        var items = ImmutableArray.CreateBuilder<GameItem>();
        var recipes = ImmutableArray.CreateBuilder<GameRecipe>();
        var buildings = ImmutableArray.CreateBuilder<GameBuilding>();

        foreach (JsonElement group in root.EnumerateArray())
        {
            if (!group.TryGetProperty("NativeClass", out JsonElement ncEl) ||
                !group.TryGetProperty("Classes", out JsonElement classesEl) ||
                classesEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            string nativeClass = ncEl.GetString() ?? string.Empty;

            if (EndsWithGroup(nativeClass, "FGRecipe"))
            {
                foreach (JsonElement c in classesEl.EnumerateArray())
                {
                    GameRecipe? r = ParseRecipe(c);
                    if (r is not null)
                    {
                        recipes.Add(r);
                    }
                }
            }
            else if (MatchesAny(nativeClass, ItemGroupSuffixes))
            {
                foreach (JsonElement c in classesEl.EnumerateArray())
                {
                    GameItem? i = ParseItem(c);
                    if (i is not null)
                    {
                        items.Add(i);
                    }
                }
            }
            else if (MatchesAny(nativeClass, BuildingGroupSuffixes))
            {
                foreach (JsonElement c in classesEl.EnumerateArray())
                {
                    GameBuilding? b = ParseBuilding(c);
                    if (b is not null)
                    {
                        buildings.Add(b);
                    }
                }
            }
        }

        ImmutableArray<GameItem> finalItems = items.ToImmutable();
        ImmutableArray<GameRecipe> finalRecipes =
            NormalizeFluidAmounts(recipes.ToImmutable(), finalItems);

        return new GameDataSnapshot(
            metadata,
            finalItems,
            finalRecipes,
            buildings.ToImmutable());
    }

    /// <summary>
    /// Rescales liquid/gas ingredient and product amounts from the raw millilitre values
    /// in Docs.json to cubic metres (÷1000), and recomputes per-minute rates accordingly.
    /// Runs after items are indexed because an item's form is only known from its
    /// descriptor, not from inside a recipe string. The result is a self-consistent model
    /// whose stored rates are the canonical vanilla per-minute figures.
    /// </summary>
    private static ImmutableArray<GameRecipe> NormalizeFluidAmounts(
        ImmutableArray<GameRecipe> recipes,
        ImmutableArray<GameItem> items)
    {
        var formByClass = new Dictionary<string, ItemForm>(StringComparer.OrdinalIgnoreCase);
        foreach (GameItem item in items)
        {
            formByClass[item.ClassName] = item.Form;
        }

        var result = ImmutableArray.CreateBuilder<GameRecipe>(recipes.Length);
        foreach (GameRecipe recipe in recipes)
        {
            result.Add(recipe with
            {
                Ingredients = RescaleLine(recipe.Ingredients, recipe.DurationSeconds, formByClass),
                Products = RescaleLine(recipe.Products, recipe.DurationSeconds, formByClass),
            });
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<RecipeItemAmount> RescaleLine(
        ImmutableArray<RecipeItemAmount> lines,
        double duration,
        IReadOnlyDictionary<string, ItemForm> formByClass)
    {
        if (lines.IsDefaultOrEmpty)
        {
            return lines;
        }

        var builder = ImmutableArray.CreateBuilder<RecipeItemAmount>(lines.Length);
        foreach (RecipeItemAmount line in lines)
        {
            bool isFluid = formByClass.TryGetValue(line.ItemClassName, out ItemForm form)
                && form is ItemForm.Liquid or ItemForm.Gas;

            double amountPerCraft = isFluid ? line.AmountPerCraft / 1000.0 : line.AmountPerCraft;
            double amountPerMinute = amountPerCraft * 60.0 / duration;
            builder.Add(line with { AmountPerCraft = amountPerCraft, AmountPerMinute = amountPerMinute });
        }

        return builder.ToImmutable();
    }

    private static bool EndsWithGroup(string nativeClass, string suffix) =>
        nativeClass.EndsWith("." + suffix + "'", StringComparison.Ordinal);

    private static bool MatchesAny(string nativeClass, string[] suffixes)
    {
        foreach (string s in suffixes)
        {
            if (EndsWithGroup(nativeClass, s))
            {
                return true;
            }
        }

        return false;
    }

    private static GameItem? ParseItem(JsonElement c)
    {
        string? className = GetString(c, "ClassName");
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        string display = GetString(c, "mDisplayName") ?? className;
        string description = TrimDescription(GetString(c, "mDescription") ?? string.Empty);
        ItemForm form = ParseForm(GetString(c, "mForm"));
        int sink = ParseInt(GetString(c, "mResourceSinkPoints"));
        int stack = ParseStackSize(GetString(c, "mCachedStackSize"), form);

        return new GameItem(className, display, description, form, stack, sink);
    }

    private static GameBuilding? ParseBuilding(JsonElement c)
    {
        string? className = GetString(c, "ClassName");
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        string display = GetString(c, "mDisplayName") ?? className;
        string description = TrimDescription(GetString(c, "mDescription") ?? string.Empty);
        double consumption = ParseDouble(GetString(c, "mPowerConsumption"));
        double production = ParseDouble(GetString(c, "mPowerProduction"));
        ClearanceBox? clearance = ParseClearance(GetString(c, "mClearanceData"));

        return new GameBuilding(className, display, description, consumption, production, clearance);
    }

    private static GameRecipe? ParseRecipe(JsonElement c)
    {
        string? className = GetString(c, "ClassName");
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        string display = GetString(c, "mDisplayName") ?? className;
        double duration = ParseDouble(GetString(c, "mManufactoringDuration"));
        if (duration <= 0)
        {
            // Customization / scanner pseudo-recipes have no real duration; skip rate math.
            duration = 1;
        }

        ImmutableArray<RecipeItemAmount> ingredients =
            ParseItemAmounts(GetString(c, "mIngredients"), duration);
        ImmutableArray<RecipeItemAmount> products =
            ParseItemAmounts(GetString(c, "mProduct"), duration);

        ImmutableArray<string> producedIn = ParseProducedIn(GetString(c, "mProducedIn"));
        bool isAlternate = className.StartsWith("Recipe_Alternate", StringComparison.OrdinalIgnoreCase);

        return new GameRecipe(className, display, duration, ingredients, products, producedIn, isAlternate);
    }

    /// <summary>
    /// Parses a UE struct-list of item amounts, e.g.
    /// <c>((ItemClass="...Desc_IronIngot_C'",Amount=3),...)</c>, keeping the RAW amount
    /// (millilitres for fluids/gases). Fluid scaling to cubic metres and the final
    /// per-minute recompute happen in <see cref="NormalizeFluidAmounts"/> once item forms
    /// are known, so this method cannot know an item's form yet.
    /// </summary>
    private static ImmutableArray<RecipeItemAmount> ParseItemAmounts(string? raw, double duration)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<RecipeItemAmount>();
        foreach (Match m in ItemAmountRegex().Matches(raw))
        {
            string classToken = m.Groups["cls"].Value;
            string itemClass = ExtractClassName(classToken);
            if (string.IsNullOrEmpty(itemClass))
            {
                continue;
            }

            double amount = ParseDouble(m.Groups["amt"].Value);
            // Per-minute is recomputed by the service after fluid scaling; store raw here.
            double perMin = amount * 60.0 / duration;
            builder.Add(new RecipeItemAmount(itemClass, amount, perMin));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> ParseProducedIn(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (Match m in QuotedPathRegex().Matches(raw))
        {
            string token = m.Groups["p"].Value;
            if (ManualCraftMarkers.Any(marker => token.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string cls = ExtractClassName(token);
            if (!string.IsNullOrEmpty(cls) && cls.StartsWith("Build_", StringComparison.Ordinal))
            {
                builder.Add(cls);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Extracts the trailing <c>Foo_C</c> class name from a UE object path such as
    /// <c>/Game/.../Desc_IronIngot.Desc_IronIngot_C</c> or a wrapped
    /// <c>"...'/Game/.../Build_X.Build_X_C'"</c>. Returns the part after the last dot.
    /// </summary>
    private static string ExtractClassName(string token)
    {
        string t = token.Trim().Trim('"', '\'');

        // Strip a leading type wrapper like BlueprintGeneratedClass'/Game/...'
        int quote = t.IndexOf('\'');
        if (quote >= 0)
        {
            t = t[(quote + 1)..].TrimEnd('\'');
        }

        int lastDot = t.LastIndexOf('.');
        return lastDot >= 0 ? t[(lastDot + 1)..] : t;
    }

    private static ClearanceBox? ParseClearance(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        Match box = ClearanceBoxRegex().Match(raw);
        if (!box.Success)
        {
            return null;
        }

        return new ClearanceBox(
            ParseDouble(box.Groups["minx"].Value),
            ParseDouble(box.Groups["miny"].Value),
            ParseDouble(box.Groups["minz"].Value),
            ParseDouble(box.Groups["maxx"].Value),
            ParseDouble(box.Groups["maxy"].Value),
            ParseDouble(box.Groups["maxz"].Value));
    }

    private static ItemForm ParseForm(string? form) => form switch
    {
        "RF_SOLID" => ItemForm.Solid,
        "RF_LIQUID" => ItemForm.Liquid,
        "RF_GAS" => ItemForm.Gas,
        _ => ItemForm.Invalid,
    };

    private static int ParseStackSize(string? raw, ItemForm form)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return form is ItemForm.Liquid or ItemForm.Gas ? 1 : 0;
        }

        // On item descriptors the value is the literal int (e.g. "200"); but the enum
        // tokens can also appear. Handle both.
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
        {
            return n;
        }

        return raw switch
        {
            "SS_ONE" => 1,
            "SS_SMALL" => 50,
            "SS_MEDIUM" => 100,
            "SS_BIG" => 200,
            "SS_HUGE" => 500,
            "SS_FLUID" => 1,
            _ => 0,
        };
    }

    private static string? GetString(JsonElement c, string name) =>
        c.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static double ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;

    private static int ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    private static string TrimDescription(string description)
    {
        // The shipped snapshot stays small; keep descriptions short and single-line.
        string cleaned = description.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        const int max = 200;
        return cleaned.Length <= max ? cleaned : cleaned[..max].TrimEnd() + "…";
    }

    [GeneratedRegex(@"ItemClass=(?<cls>[^,]+),Amount=(?<amt>-?[\d.]+)", RegexOptions.Compiled)]
    private static partial Regex ItemAmountRegex();

    [GeneratedRegex(@"""(?<p>[^""]+)""", RegexOptions.Compiled)]
    private static partial Regex QuotedPathRegex();

    [GeneratedRegex(
        @"ClearanceBox=\(Min=\(X=(?<minx>-?[\d.]+),Y=(?<miny>-?[\d.]+),Z=(?<minz>-?[\d.]+)\),Max=\(X=(?<maxx>-?[\d.]+),Y=(?<maxy>-?[\d.]+),Z=(?<maxz>-?[\d.]+)\)",
        RegexOptions.Compiled)]
    private static partial Regex ClearanceBoxRegex();
}
