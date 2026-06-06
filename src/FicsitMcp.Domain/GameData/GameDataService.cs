using System.Collections.Immutable;

using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Default <see cref="IGameDataService"/>: builds case-insensitive lookup indexes over an
/// immutable <see cref="GameDataSnapshot"/> once at construction, so every query is an
/// O(1) dictionary hit with no per-call scanning.
/// </summary>
/// <remarks>
/// The snapshot and all indexes are immutable; the service is therefore safe to register
/// as a singleton and share across concurrent tool invocations.
/// </remarks>
public sealed class GameDataService : IGameDataService
{
    private const int MaxNearMatches = 5;

    private readonly GameDataSnapshot _snapshot;

    // Class-name keyed (primary, always unique) and display-name keyed (secondary).
    private readonly Dictionary<string, GameItem> _itemsByClass;
    private readonly Dictionary<string, GameItem> _itemsByDisplay;
    private readonly Dictionary<string, GameBuilding> _buildingsByClass;
    private readonly Dictionary<string, GameBuilding> _buildingsByDisplay;
    private readonly Dictionary<string, GameRecipe> _recipesByClass;
    private readonly Dictionary<string, List<GameRecipe>> _recipesByDisplay;

    // Reverse indexes: item class -> recipes that produce / consume it.
    private readonly Dictionary<string, List<GameRecipe>> _producedBy;
    private readonly Dictionary<string, List<GameRecipe>> _consumedBy;

    /// <summary>Builds the service and its indexes from a parsed snapshot.</summary>
    public GameDataService(GameDataSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;

        StringComparer ci = StringComparer.OrdinalIgnoreCase;

        _itemsByClass = new Dictionary<string, GameItem>(ci);
        _itemsByDisplay = new Dictionary<string, GameItem>(ci);
        foreach (GameItem item in snapshot.Items)
        {
            _itemsByClass[item.ClassName] = item;
            // Display names can collide across forms/variants; first write wins, class
            // name remains the unambiguous key.
            _itemsByDisplay.TryAdd(item.DisplayName, item);
        }

        _buildingsByClass = new Dictionary<string, GameBuilding>(ci);
        _buildingsByDisplay = new Dictionary<string, GameBuilding>(ci);
        foreach (GameBuilding b in snapshot.Buildings)
        {
            _buildingsByClass[b.ClassName] = b;
            _buildingsByDisplay.TryAdd(b.DisplayName, b);
        }

        _recipesByClass = new Dictionary<string, GameRecipe>(ci);
        _recipesByDisplay = new Dictionary<string, List<GameRecipe>>(ci);
        _producedBy = new Dictionary<string, List<GameRecipe>>(ci);
        _consumedBy = new Dictionary<string, List<GameRecipe>>(ci);
        foreach (GameRecipe r in snapshot.Recipes)
        {
            _recipesByClass[r.ClassName] = r;
            if (!_recipesByDisplay.TryGetValue(r.DisplayName, out List<GameRecipe>? byName))
            {
                byName = [];
                _recipesByDisplay[r.DisplayName] = byName;
            }

            byName.Add(r);

            foreach (RecipeItemAmount p in r.Products)
            {
                AddTo(_producedBy, p.ItemClassName, r);
            }

            foreach (RecipeItemAmount ing in r.Ingredients)
            {
                AddTo(_consumedBy, ing.ItemClassName, r);
            }
        }
    }

    /// <inheritdoc />
    public GameDataMetadata Metadata => _snapshot.Metadata;

    /// <inheritdoc />
    public GameItem GetItem(string nameOrClass)
    {
        if (TryGetItem(nameOrClass, out GameItem item))
        {
            return item;
        }

        throw NotFound(nameOrClass, "item", _itemsByClass.Keys, _itemsByDisplay.Keys);
    }

    /// <inheritdoc />
    public bool TryGetItem(string nameOrClass, out GameItem item)
    {
        string key = Normalize(nameOrClass);
        if (_itemsByClass.TryGetValue(key, out GameItem? byClass))
        {
            item = byClass;
            return true;
        }

        if (_itemsByDisplay.TryGetValue(key, out GameItem? byDisplay))
        {
            item = byDisplay;
            return true;
        }

        item = null!;
        return false;
    }

    /// <inheritdoc />
    public GameBuilding GetBuilding(string nameOrClass)
    {
        string key = Normalize(nameOrClass);
        if (_buildingsByClass.TryGetValue(key, out GameBuilding? byClass))
        {
            return byClass;
        }

        if (_buildingsByDisplay.TryGetValue(key, out GameBuilding? byDisplay))
        {
            return byDisplay;
        }

        throw NotFound(nameOrClass, "building", _buildingsByClass.Keys, _buildingsByDisplay.Keys);
    }

    /// <inheritdoc />
    public GameRecipe GetRecipe(string nameOrClass)
    {
        string key = Normalize(nameOrClass);
        if (_recipesByClass.TryGetValue(key, out GameRecipe? byClass))
        {
            return byClass;
        }

        if (_recipesByDisplay.TryGetValue(key, out List<GameRecipe>? byName))
        {
            if (byName.Count == 1)
            {
                return byName[0];
            }

            // Ambiguous display name: surface the candidate class names to disambiguate.
            throw new UnknownGameDataNameException(
                nameOrClass,
                "recipe (ambiguous display name)",
                byName.Select(r => r.ClassName).Take(MaxNearMatches).ToImmutableArray());
        }

        throw NotFound(nameOrClass, "recipe", _recipesByClass.Keys, _recipesByDisplay.Keys);
    }

    /// <inheritdoc />
    public RecipeView GetRecipeView(string nameOrClass) => ToView(GetRecipe(nameOrClass));

    /// <inheritdoc />
    public ItemRecipesResult GetRecipesForItem(string itemNameOrClass)
    {
        GameItem item = GetItem(itemNameOrClass);

        ImmutableArray<RecipeView> producedBy = LookupRecipes(_producedBy, item.ClassName);
        ImmutableArray<RecipeView> consumedBy = LookupRecipes(_consumedBy, item.ClassName);

        return new ItemRecipesResult(item, producedBy, consumedBy);
    }

    /// <inheritdoc />
    public RecipeValidationResult ValidateRecipeForBuilding(string recipeNameOrClass, string buildingNameOrClass)
    {
        GameRecipe recipe;
        try
        {
            recipe = GetRecipe(recipeNameOrClass);
        }
        catch (UnknownGameDataNameException)
        {
            return new RecipeValidationResult(
                IsValid: false,
                RecipeClassName: null,
                BuildingClassName: null,
                Reason: $"Unknown recipe '{recipeNameOrClass}'.",
                ValidBuildingClassNames: []);
        }

        GameBuilding building;
        try
        {
            building = GetBuilding(buildingNameOrClass);
        }
        catch (UnknownGameDataNameException)
        {
            return new RecipeValidationResult(
                IsValid: false,
                RecipeClassName: recipe.ClassName,
                BuildingClassName: null,
                Reason: $"Unknown building '{buildingNameOrClass}'.",
                ValidBuildingClassNames: recipe.ProducedInBuildingClassNames);
        }

        bool compatible = recipe.ProducedInBuildingClassNames.Contains(
            building.ClassName, StringComparer.OrdinalIgnoreCase);

        return new RecipeValidationResult(
            IsValid: compatible,
            RecipeClassName: recipe.ClassName,
            BuildingClassName: building.ClassName,
            Reason: compatible
                ? string.Empty
                : $"Recipe '{recipe.ClassName}' cannot be run in '{building.ClassName}'. " +
                  $"Valid buildings: {(recipe.ProducedInBuildingClassNames.IsDefaultOrEmpty ? "none (hand-craft only)" : string.Join(", ", recipe.ProducedInBuildingClassNames))}.",
            ValidBuildingClassNames: recipe.ProducedInBuildingClassNames);
    }

    private ImmutableArray<RecipeView> LookupRecipes(
        Dictionary<string, List<GameRecipe>> index, string itemClassName)
    {
        if (!index.TryGetValue(itemClassName, out List<GameRecipe>? recipes))
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<RecipeView>(recipes.Count);
        foreach (GameRecipe r in recipes)
        {
            builder.Add(ToView(r));
        }

        return builder.ToImmutable();
    }

    private RecipeView ToView(GameRecipe recipe) => new(
        recipe.ClassName,
        recipe.DisplayName,
        recipe.DurationSeconds,
        recipe.IsAlternate,
        ToRates(recipe.Ingredients),
        ToRates(recipe.Products),
        ToProducingBuildings(recipe.ProducedInBuildingClassNames));

    private ImmutableArray<RecipeRate> ToRates(ImmutableArray<RecipeItemAmount> lines)
    {
        if (lines.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<RecipeRate>(lines.Length);
        foreach (RecipeItemAmount line in lines)
        {
            string display = line.ItemClassName;
            ItemForm form = ItemForm.Invalid;
            if (_itemsByClass.TryGetValue(line.ItemClassName, out GameItem? item))
            {
                display = item.DisplayName;
                form = item.Form;
            }

            builder.Add(new RecipeRate(
                line.ItemClassName, display, line.AmountPerCraft, line.AmountPerMinute, form));
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<ProducingBuilding> ToProducingBuildings(ImmutableArray<string> classNames)
    {
        if (classNames.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<ProducingBuilding>(classNames.Length);
        foreach (string cls in classNames)
        {
            if (_buildingsByClass.TryGetValue(cls, out GameBuilding? b))
            {
                builder.Add(new ProducingBuilding(b.ClassName, b.DisplayName, b.PowerConsumptionMw));
            }
            else
            {
                builder.Add(new ProducingBuilding(cls, cls, null));
            }
        }

        return builder.ToImmutable();
    }

    private static void AddTo(Dictionary<string, List<GameRecipe>> index, string key, GameRecipe recipe)
    {
        if (!index.TryGetValue(key, out List<GameRecipe>? list))
        {
            list = [];
            index[key] = list;
        }

        list.Add(recipe);
    }

    private static string Normalize(string nameOrClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrClass);
        return nameOrClass.Trim();
    }

    private UnknownGameDataNameException NotFound(
        string query, string kind, IEnumerable<string> classKeys, IEnumerable<string> displayKeys)
    {
        ImmutableArray<string> near = NearMatches(query, classKeys.Concat(displayKeys));
        return new UnknownGameDataNameException(query, kind, near);
    }

    /// <summary>
    /// Ranks candidate names by closeness to the query: exact-substring containment first
    /// (cheap and high-signal for class/display names), then shared prefix length. Used
    /// only on the cold error path, so a linear scan over keys is acceptable.
    /// </summary>
    private static ImmutableArray<string> NearMatches(string query, IEnumerable<string> candidates)
    {
        string q = query.Trim();
        if (q.Length == 0)
        {
            return [];
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(c => (name: c, score: Score(q, c)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxNearMatches)
            .Select(x => x.name)
            .ToImmutableArray();
    }

    private static int Score(string query, string candidate)
    {
        if (candidate.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1000 + query.Length;
        }

        // Shared leading run of characters (case-insensitive).
        int shared = 0;
        int max = Math.Min(query.Length, candidate.Length);
        while (shared < max && char.ToUpperInvariant(query[shared]) == char.ToUpperInvariant(candidate[shared]))
        {
            shared++;
        }

        return shared >= 3 ? shared : 0;
    }
}
