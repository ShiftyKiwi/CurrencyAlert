using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace CurrencyAlert.Classes;

/// <summary>
/// Describes whether a currency should normally be shown in the curated browser.
/// The value is catalog metadata only; it is intentionally not persisted with a
/// tracked currency so that game-data changes can update it without a config migration.
/// </summary>
public enum CurrencyAvailability {
    Current,
    Legacy,
    Obsolete,
    Future,
    Unsupported,
}

/// <summary>
/// Selects the maintained game API used to read a currency balance.
/// </summary>
public enum CurrencyCounterKind {
    InventoryItem,
    Gil,
    Mgp,
    CompanySeal,
    WolfMarks,
    AlliedSeals,
    Tomestone,
    CurrencyManager,
    CosmicExploration,
    OccultCrescent,
    Unsupported,
}

/// <summary>
/// Runtime-only metadata for a currency.  The stable identity is the key/item ID,
/// never a localized display name.  Names and icons are read from Lumina whenever
/// the current client has the matching Item row.
/// </summary>
public sealed class CurrencyDefinition {
    private string? cachedName;
    private uint? cachedIconId;

    internal CurrencyDefinition(
        string key,
        uint itemId,
        string category,
        CurrencyCounterKind counterKind,
        CurrencyAvailability availability = CurrencyAvailability.Current,
        CurrencyType type = CurrencyType.Item,
        string? fallbackName = null,
        uint fallbackIconId = 0,
        byte nativeArgument = 0) {
        Key = key;
        ItemId = itemId;
        Category = category;
        CounterKind = counterKind;
        Availability = availability;
        Type = type;
        FallbackName = fallbackName;
        FallbackIconId = fallbackIconId;
        NativeArgument = nativeArgument;
    }

    public string Key { get; }
    public uint ItemId { get; }
    public string Category { get; }
    public CurrencyCounterKind CounterKind { get; }
    public CurrencyAvailability Availability { get; }
    public CurrencyType Type { get; }

    /// <summary>Additional native API argument, currently the Grand Company ID.</summary>
    internal byte NativeArgument { get; }
    internal string? FallbackName { get; }
    internal uint FallbackIconId { get; }

    public string Name {
        get {
            if (cachedName is not null) return cachedName;

            if (TryGetItem(ItemId, out var item) && !string.IsNullOrWhiteSpace(item.Name.ExtractText())) {
                return cachedName = item.Name.ExtractText();
            }

            return cachedName = FallbackName ?? (ItemId == 0 ? "Unsupported Currency" : $"Currency #{ItemId}");
        }
    }

    public uint IconId {
        get {
            if (cachedIconId is { } iconId) return iconId;

            if (TryGetItem(ItemId, out var item) && item.Icon != 0) {
                cachedIconId = item.Icon;
                return item.Icon;
            }

            cachedIconId = FallbackIconId;
            return FallbackIconId;
        }
    }

    public TrackedCurrency CreateTrackedCurrency() => new() {
        Type = Type,
        ItemId = ItemId,
        CurrencyKey = Key,
        Threshold = 1000,
        Enabled = true,
    };

    private static bool TryGetItem(uint itemId, out Item item) {
        item = default;
        if (itemId == 0) return false;

        item = Service.DataManager.GetExcelSheet<Item>().GetRow(itemId);
        return item.RowId != 0;
    }
}

/// <summary>
/// Builds the curated currency browser once from game data plus the small set of
/// non-standard currencies that are intentionally classified here.  This is never
/// consulted as a replacement for the unrestricted Add Item picker.
/// </summary>
public static class CurrencyCatalog {
    private const uint CurrencyItemUiCategoryId = 100;
    private const uint UnavailableIconId = 60071;

    private static readonly Dictionary<uint, CurrencyMetadata> CuratedItemMetadata = new() {
        [1] = new("Common", CurrencyCounterKind.Gil),
        [20] = new("Grand Companies", CurrencyCounterKind.CompanySeal, NativeArgument: 1),
        [21] = new("Grand Companies", CurrencyCounterKind.CompanySeal, NativeArgument: 2),
        [22] = new("Grand Companies", CurrencyCounterKind.CompanySeal, NativeArgument: 3),
        [25] = new("PvP", CurrencyCounterKind.WolfMarks),
        [27] = new("Hunts", CurrencyCounterKind.AlliedSeals),
        [28] = new("Tomestones", CurrencyCounterKind.Tomestone),
        [29] = new("Common", CurrencyCounterKind.Mgp),
        [36656] = new("PvP", CurrencyCounterKind.CurrencyManager),
        [10307] = new("Hunts", CurrencyCounterKind.CurrencyManager),
        [10308] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),
        [10309] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),
        [10310] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),
        [10311] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),
        [17833] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),
        [17834] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),
        [26533] = new("Hunts", CurrencyCounterKind.CurrencyManager),
        [26807] = new("FATE", CurrencyCounterKind.CurrencyManager),
        [28063] = new("Crafting & Gathering", CurrencyCounterKind.CurrencyManager),

        [21072] = new("Common", CurrencyCounterKind.CurrencyManager),
        [21172] = new("Common", CurrencyCounterKind.CurrencyManager),
        [30341] = new("Special / Event", CurrencyCounterKind.CurrencyManager),
        [41629] = new("Special / Event", CurrencyCounterKind.CurrencyManager),

        [21073] = new("Allied Societies / A Realm Reborn", CurrencyCounterKind.CurrencyManager),
        [21075] = new("Allied Societies / A Realm Reborn", CurrencyCounterKind.CurrencyManager),
        [21076] = new("Allied Societies / A Realm Reborn", CurrencyCounterKind.CurrencyManager),
        [21077] = new("Allied Societies / A Realm Reborn", CurrencyCounterKind.CurrencyManager),
        [21078] = new("Allied Societies / A Realm Reborn", CurrencyCounterKind.CurrencyManager),
        [21074] = new("Allied Societies / Heavensward", CurrencyCounterKind.CurrencyManager),
        [21079] = new("Allied Societies / Heavensward", CurrencyCounterKind.CurrencyManager),
        [21080] = new("Allied Societies / Heavensward", CurrencyCounterKind.CurrencyManager),
        [21081] = new("Allied Societies / Stormblood", CurrencyCounterKind.CurrencyManager),
        [21935] = new("Allied Societies / Stormblood", CurrencyCounterKind.CurrencyManager),
        [22525] = new("Allied Societies / Stormblood", CurrencyCounterKind.CurrencyManager),
        [28186] = new("Allied Societies / Shadowbringers", CurrencyCounterKind.CurrencyManager),
        [28187] = new("Allied Societies / Shadowbringers", CurrencyCounterKind.CurrencyManager),
        [28188] = new("Allied Societies / Shadowbringers", CurrencyCounterKind.CurrencyManager),
        [36657] = new("Allied Societies / Endwalker", CurrencyCounterKind.CurrencyManager),
        [37854] = new("Allied Societies / Endwalker", CurrencyCounterKind.CurrencyManager),
        [38952] = new("Allied Societies / Endwalker", CurrencyCounterKind.CurrencyManager),
        [44472] = new("Allied Societies / Dawntrail", CurrencyCounterKind.CurrencyManager),
        [46178] = new("Allied Societies / Dawntrail", CurrencyCounterKind.CurrencyManager),
        [48084] = new("Allied Societies / Dawntrail", CurrencyCounterKind.CurrencyManager),

        [38534] = new("Variant & Criterion", CurrencyCounterKind.CurrencyManager),
        [39885] = new("Variant & Criterion", CurrencyCounterKind.CurrencyManager),
        [41079] = new("Variant & Criterion", CurrencyCounterKind.CurrencyManager),
        [49125] = new("Variant & Criterion", CurrencyCounterKind.CurrencyManager),

        // CurrencyManager documents these content-bucket currencies.  They are
        // deliberately not sent through InventoryManager.GetInventoryItemCount.
        [37549] = new("Island Sanctuary", CurrencyCounterKind.CurrencyManager),
        [37550] = new("Island Sanctuary", CurrencyCounterKind.CurrencyManager),
        [41668] = new("Island Sanctuary", CurrencyCounterKind.CurrencyManager),
        [31135] = new("Field Operations / Bozja", CurrencyCounterKind.CurrencyManager),

        [45690] = new("Cosmic Exploration", CurrencyCounterKind.CosmicExploration),
        [45691] = new("Cosmic Exploration", CurrencyCounterKind.CosmicExploration),
        [48146] = new("Cosmic Exploration", CurrencyCounterKind.CosmicExploration),
        [48147] = new("Cosmic Exploration", CurrencyCounterKind.CosmicExploration),
        [48148] = new("Cosmic Exploration", CurrencyCounterKind.CosmicExploration),

        [45043] = new("Field Operations / Occult Crescent / South Horn", CurrencyCounterKind.OccultCrescent),
        [45044] = new("Field Operations / Occult Crescent / South Horn", CurrencyCounterKind.OccultCrescent),
        [47739] = new("Field Operations / Occult Crescent / South Horn", CurrencyCounterKind.OccultCrescent),
        [51975] = new("Field Operations / Occult Crescent / North Horn", CurrencyCounterKind.OccultCrescent),
        [51976] = new("Field Operations / Occult Crescent / North Horn", CurrencyCounterKind.OccultCrescent),
        [51977] = new("Field Operations / Occult Crescent / North Horn", CurrencyCounterKind.OccultCrescent),
        [52322] = new("Special / Event", CurrencyCounterKind.CurrencyManager),
    };

    private static readonly Dictionary<uint, CurrencyAvailability> AvailabilityOverrides = new() {
        // Retired scrips are preserved in the catalog for old profiles, but do not
        // clutter the default browser.
        [10308] = CurrencyAvailability.Obsolete,
        [10309] = CurrencyAvailability.Obsolete,
        [10310] = CurrencyAvailability.Obsolete,
        [10311] = CurrencyAvailability.Obsolete,
        [17833] = CurrencyAvailability.Obsolete,
        [17834] = CurrencyAvailability.Obsolete,
        [25199] = CurrencyAvailability.Legacy,
        [25200] = CurrencyAvailability.Legacy,
        [52322] = CurrencyAvailability.Future,
    };

    // These are display fallbacks for rows introduced after an older local game-data cache.
    // They are never used as identity: a present Item row always supplies the localized name.
    private static readonly Dictionary<uint, string> FallbackItemNames = new() {
        [21073] = "Ixali Oaknot",
        [21074] = "Vanu Whitebone",
        [21075] = "Sylphic Goldleaf",
        [21076] = "Steel Amalj'ok",
        [21077] = "Rainbowtide Psashp",
        [21078] = "Titan Cobaltpiece",
        [21079] = "Black Copper Gil",
        [21080] = "Carved Kupo Nut",
        [21081] = "Kojin Sango",
        [21935] = "Ananta Dreamstaff",
        [22525] = "Namazu Koban",
        [28186] = "Fae Fancy",
        [28187] = "Qitari Compliment",
        [28188] = "Hammered Frogment",
        [36657] = "Arkasodara Pana",
        [37549] = "Seafarer's Cowrie",
        [37550] = "Islander's Cowrie",
        [37854] = "Omicron Omnitoken",
        [38534] = "Sil'dihn Silver",
        [38952] = "Loporrit Carat",
        [39885] = "Shishu Coin",
        [41079] = "Aloalo Coin",
        [41668] = "Felicitous Token",
        [44472] = "Pelu Pelplume",
        [45043] = "Enlightenment Silver Piece",
        [45044] = "Enlightenment Gold Piece",
        [45690] = "Cosmocredit",
        [45691] = "Lunar Credit",
        [46178] = "Yok Huy Ward",
        [47739] = "Sanguine Cipher",
        [48084] = "Mamool Ja Nanook",
        [48146] = "Phaenna Credit",
        [48147] = "Oizys Credit",
        [48148] = "Auxesia Credit",
        [49125] = "Corvosi Manuscript",
        [51975] = "Enlightenment Silver Obol",
        [51976] = "Enlightenment Gold Obol",
        [51977] = "Arcane Amulet",
        [52322] = "MGC",
    };

    private static readonly Dictionary<CurrencyType, string> SpecialCurrencyKeys = new() {
        [CurrencyType.NonLimitedTomestone] = "special:non-limited-tomestone",
        [CurrencyType.LimitedTomestone] = "special:limited-tomestone",
        [CurrencyType.EvergreenTomestone] = "special:evergreen-tomestone",
        [CurrencyType.DiscontinuedTomestone] = "special:discontinued-tomestone",
        [CurrencyType.DiscontinuedCraftersScrip] = "special:discontinued-crafters-scrip",
        [CurrencyType.DiscontinuedGatherersScrip] = "special:discontinued-gatherers-scrip",
        [CurrencyType.PreviousCraftersScrip] = "special:previous-crafters-scrip",
        [CurrencyType.PreviousGatherersScrip] = "special:previous-gatherers-scrip",
        [CurrencyType.CurrentCraftersScrip] = "special:current-crafters-scrip",
        [CurrencyType.CurrentGatherersScrip] = "special:current-gatherers-scrip",
    };

    private static IReadOnlyList<CurrencyDefinition>? allDefinitions;
    private static IReadOnlyList<CurrencyDefinition>? currentDefinitions;
    private static Dictionary<string, CurrencyDefinition>? definitionsByKey;
    private static Dictionary<uint, CurrencyDefinition>? definitionsByItemId;

    /// <summary>Gets cached curated definitions, optionally including legacy/future/unsupported rows.</summary>
    public static IReadOnlyList<CurrencyDefinition> GetDefinitions(bool includeUnavailable) {
        EnsureBuilt();
        return includeUnavailable ? allDefinitions! : currentDefinitions!;
    }

    public static bool IsTracked(TrackedCurrency currency, CurrencyDefinition definition) {
        if (!string.IsNullOrEmpty(currency.CurrencyKey) && string.Equals(currency.CurrencyKey, definition.Key, StringComparison.Ordinal)) {
            return true;
        }

        // Preserve the existing plugin's no-duplicate-by-item behavior, while still
        // allowing a special definition with no Item row to use its stable key.
        return definition.ItemId != 0 && currency.ItemId == definition.ItemId;
    }

    public static CurrencyDefinition? GetDefinition(TrackedCurrency currency) {
        EnsureBuilt();

        if (!string.IsNullOrEmpty(currency.CurrencyKey) && definitionsByKey!.TryGetValue(currency.CurrencyKey, out var keyedDefinition)) {
            return keyedDefinition;
        }

        if (SpecialCurrencyKeys.TryGetValue(currency.Type, out var specialKey) && definitionsByKey!.TryGetValue(specialKey, out var specialDefinition)) {
            return specialDefinition;
        }

        return currency.ItemId != 0 && definitionsByItemId!.TryGetValue(currency.ItemId, out var itemDefinition)
            ? itemDefinition
            : null;
    }

    public static uint GetSpecialItemId(CurrencyType type) => type switch {
        CurrencyType.DiscontinuedCraftersScrip => 25199,
        CurrencyType.DiscontinuedGatherersScrip => 25200,
        CurrencyType.PreviousCraftersScrip => 33913,
        CurrencyType.PreviousGatherersScrip => 33914,
        CurrencyType.CurrentCraftersScrip => 41784,
        CurrencyType.CurrentGatherersScrip => 41785,
        CurrencyType.NonLimitedTomestone => GetTomestoneItemId(item => item.Tomestones.RowId == 2),
        CurrencyType.LimitedTomestone => GetTomestoneItemId(item => item.Tomestones.ValueNullable is { WeeklyLimit: > 0 }),
        CurrencyType.EvergreenTomestone => GetTomestoneItemId(item => item.Tomestones.RowId == 1),
        CurrencyType.DiscontinuedTomestone => GetTomestoneItemId(item => item.Tomestones.RowId == 4),
        _ => 0,
    };

    /// <summary>
    /// CurrencyManager's special bucket keeps scrip identities stable even when a patch changes
    /// the underlying Item row.  Zero means this CurrencyType is not a special-bucket scrip.
    /// </summary>
    public static byte GetCurrencyManagerSpecialId(CurrencyType type) => type switch {
        CurrencyType.DiscontinuedCraftersScrip => 1,
        CurrencyType.PreviousCraftersScrip => 2,
        CurrencyType.DiscontinuedGatherersScrip => 3,
        CurrencyType.PreviousGatherersScrip => 4,
        CurrencyType.CurrentCraftersScrip => 6,
        CurrencyType.CurrentGatherersScrip => 7,
        _ => 0,
    };

    public static bool IsSpecialCurrencyType(CurrencyType type)
        => SpecialCurrencyKeys.ContainsKey(type);

    private static void EnsureBuilt() {
        if (allDefinitions is not null) return;

        var definitions = new List<CurrencyDefinition>();
        var explicitItemIds = new HashSet<uint>();

        void Add(CurrencyDefinition definition) {
            definitions.Add(definition);
            if (definition.ItemId != 0) explicitItemIds.Add(definition.ItemId);
        }

        Add(new CurrencyDefinition("item:1", 1, "Common", CurrencyCounterKind.Gil));
        Add(new CurrencyDefinition("item:29", 29, "Common", CurrencyCounterKind.Mgp));

        Add(new CurrencyDefinition("item:20", 20, "Grand Companies", CurrencyCounterKind.CompanySeal, nativeArgument: 1));
        Add(new CurrencyDefinition("item:21", 21, "Grand Companies", CurrencyCounterKind.CompanySeal, nativeArgument: 2));
        Add(new CurrencyDefinition("item:22", 22, "Grand Companies", CurrencyCounterKind.CompanySeal, nativeArgument: 3));
        Add(new CurrencyDefinition("item:25", 25, "PvP", CurrencyCounterKind.WolfMarks));
        Add(new CurrencyDefinition("item:27", 27, "Hunts", CurrencyCounterKind.AlliedSeals));

        AddSpecial(CurrencyType.EvergreenTomestone, "Tomestones", CurrencyCounterKind.Tomestone);
        AddSpecial(CurrencyType.NonLimitedTomestone, "Tomestones", CurrencyCounterKind.Tomestone);
        AddSpecial(CurrencyType.LimitedTomestone, "Tomestones", CurrencyCounterKind.Tomestone);
        AddSpecial(CurrencyType.DiscontinuedTomestone, "Tomestones", CurrencyCounterKind.Tomestone, CurrencyAvailability.Obsolete);

        AddSpecial(CurrencyType.CurrentCraftersScrip, "Crafting & Gathering", CurrencyCounterKind.CurrencyManager);
        AddSpecial(CurrencyType.PreviousCraftersScrip, "Crafting & Gathering", CurrencyCounterKind.CurrencyManager);
        AddSpecial(CurrencyType.CurrentGatherersScrip, "Crafting & Gathering", CurrencyCounterKind.CurrencyManager);
        AddSpecial(CurrencyType.PreviousGatherersScrip, "Crafting & Gathering", CurrencyCounterKind.CurrencyManager);
        AddSpecial(CurrencyType.DiscontinuedCraftersScrip, "Crafting & Gathering", CurrencyCounterKind.CurrencyManager, CurrencyAvailability.Legacy);
        AddSpecial(CurrencyType.DiscontinuedGatherersScrip, "Crafting & Gathering", CurrencyCounterKind.CurrencyManager, CurrencyAvailability.Legacy);

        foreach (var (itemId, metadata) in CuratedItemMetadata.OrderBy(pair => pair.Key)) {
            if (explicitItemIds.Contains(itemId)) continue;

            Add(CreateItemDefinition(itemId, metadata));
        }

        // Formal Currency rows are discovered from the game data, so ordinary new
        // currencies added by a later patch naturally enter the browser.  Explicit
        // metadata above wins where a nonstandard reader or a better category exists.
        foreach (var item in Service.DataManager.GetExcelSheet<Item>()) {
            if (item.RowId == 0 || item.ItemUICategory.RowId != CurrencyItemUiCategoryId || explicitItemIds.Contains(item.RowId)) continue;

            var metadata = CuratedItemMetadata.TryGetValue(item.RowId, out var curated)
                ? curated
                : new CurrencyMetadata("Other Currencies", CurrencyCounterKind.CurrencyManager);
            Add(CreateItemDefinition(item.RowId, metadata));
        }

        // Free Company credits have no maintained player-balance API in the referenced
        // client structures.  Keep the limitation visible only when unavailable rows are
        // requested instead of manufacturing a zero balance.
        Add(new CurrencyDefinition(
            "special:company-credits",
            0,
            "Special / Event",
            CurrencyCounterKind.Unsupported,
            CurrencyAvailability.Unsupported,
            fallbackName: "Company Credits",
            fallbackIconId: UnavailableIconId));

        allDefinitions = definitions
            .OrderBy(definition => definition.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        currentDefinitions = allDefinitions.Where(definition => definition.Availability is CurrencyAvailability.Current).ToList();
        definitionsByKey = allDefinitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);
        definitionsByItemId = allDefinitions
            .Where(definition => definition.ItemId != 0)
            .GroupBy(definition => definition.ItemId)
            .ToDictionary(group => group.Key, group => group.First());

        void AddSpecial(CurrencyType type, string category, CurrencyCounterKind counterKind, CurrencyAvailability availability = CurrencyAvailability.Current) {
            var itemId = GetSpecialItemId(type);
            Add(new CurrencyDefinition(
                SpecialCurrencyKeys[type],
                itemId,
                category,
                counterKind,
                availability,
                type,
                fallbackName: GetSpecialFallbackName(type),
                fallbackIconId: UnavailableIconId));
        }
    }

    private static CurrencyDefinition CreateItemDefinition(uint itemId, CurrencyMetadata metadata) => new(
        $"item:{itemId}",
        itemId,
        metadata.Category,
        metadata.CounterKind,
        AvailabilityOverrides.GetValueOrDefault(itemId, CurrencyAvailability.Current),
        fallbackName: FallbackItemNames.GetValueOrDefault(itemId),
        fallbackIconId: UnavailableIconId,
        nativeArgument: metadata.NativeArgument);

    private static uint GetTomestoneItemId(Func<TomestonesItem, bool> predicate)
        => Service.DataManager.GetExcelSheet<TomestonesItem>().FirstOrDefault(predicate).Item.RowId;

    private static string GetSpecialFallbackName(CurrencyType type) => type switch {
        CurrencyType.NonLimitedTomestone => "Current Tomestone",
        CurrencyType.LimitedTomestone => "Weekly Tomestone",
        CurrencyType.EvergreenTomestone => "Allagan Tomestone of Poetics",
        CurrencyType.DiscontinuedTomestone => "Discontinued Tomestone",
        CurrencyType.DiscontinuedCraftersScrip => "White Crafters' Scrip",
        CurrencyType.DiscontinuedGatherersScrip => "White Gatherers' Scrip",
        CurrencyType.PreviousCraftersScrip => "Previous Crafters' Scrip",
        CurrencyType.PreviousGatherersScrip => "Previous Gatherers' Scrip",
        CurrencyType.CurrentCraftersScrip => "Current Crafters' Scrip",
        CurrencyType.CurrentGatherersScrip => "Current Gatherers' Scrip",
        _ => type.ToString(),
    };

    private readonly record struct CurrencyMetadata(string Category, CurrencyCounterKind CounterKind, byte NativeArgument = 0);
}
