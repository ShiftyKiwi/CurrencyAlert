using System;
using System.Text.Json.Serialization;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace CurrencyAlert.Classes;

// Do not reorder these values: existing configuration files serialize the enum numerically.
public enum CurrencyType {
    Item = 0,
    HighQualityItem = 1,
    Collectable = 2,
    NonLimitedTomestone = 3,
    LimitedTomestone = 4,
    EvergreenTomestone = 5,
    DiscontinuedTomestone = 6,
    DiscontinuedCraftersScrip = 7,
    DiscontinuedGatherersScrip = 8,
    PreviousCraftersScrip = 9,
    PreviousGatherersScrip = 10,
    CurrentCraftersScrip = 11,
    CurrentGatherersScrip = 12,

    // Alias while preserving old config integer values.
    StandardTomestone = NonLimitedTomestone,
}

public enum CurrencyCountStatus {
    Available,
    NotLoaded,
    Unavailable,
    Unsupported,
}

/// <summary>
/// A count is only meaningful when <see cref="Status"/> is <see cref="CurrencyCountStatus.Available"/>.
/// The explicit state prevents content-only currencies from masquerading as a balance of zero.
/// </summary>
public readonly record struct CurrencyBalance(CurrencyCountStatus Status, int Count = 0) {
    public bool IsAvailable => Status is CurrencyCountStatus.Available;

    public static CurrencyBalance Available(int count) => new(CurrencyCountStatus.Available, count);
    public static CurrencyBalance NotLoaded => new(CurrencyCountStatus.NotLoaded);
    public static CurrencyBalance Unavailable => new(CurrencyCountStatus.Unavailable);
    public static CurrencyBalance Unsupported => new(CurrencyCountStatus.Unsupported);
}

public unsafe class TrackedCurrency {
    private const uint UnavailableSpecialIconId = 60071;

    private uint? iconId;
    private uint? itemId;
    private long lastDynamicScripResolveTimestamp;

    public required CurrencyType Type { get; init; }

    /// <summary>
    /// Optional stable catalog key.  Old configurations do not need it: their existing item ID
    /// and CurrencyType continue to resolve through the runtime catalog.
    /// </summary>
    public string? CurrencyKey;

    [JsonIgnore] public IDalamudTextureWrap Icon => Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
        HiRes = true, ItemHq = Type is CurrencyType.HighQualityItem, IconId = IconId,
    }).GetWrapOrEmpty();

    public uint ItemId {
        get => GetItemId();
        init => itemId = CurrencyCatalog.IsSpecialCurrencyType(Type) ? GetItemId() : value;
    }

    // Don't save iconId because special currencies and game data can change between patches.
    [JsonIgnore] public uint IconId {
        get {
            var resolvedItemId = ItemId;
            if (resolvedItemId != 0) {
                var item = Service.DataManager.GetExcelSheet<Item>().GetRow(resolvedItemId);
                if (item.RowId != 0 && item.Icon != 0) return iconId ??= item.Icon;
            }

            var definition = CurrencyCatalog.GetDefinition(this);
            return definition is { IconId: not 0 } catalogIcon ? catalogIcon.IconId : UnavailableSpecialIconId;
        }
        set => iconId = value;
    }

    public required int Threshold;

    public bool Enabled = true;

    public bool ChatWarning;

    public bool ShowInOverlay;

    public bool ShowItemName = true;

    public bool Invert;

    public string WarningText = "Above Threshold";

    [JsonIgnore] public string Name {
        get {
            var resolvedItemId = ItemId;
            if (resolvedItemId != 0) {
                var item = Service.DataManager.GetExcelSheet<Item>().GetRow(resolvedItemId);
                if (item.RowId != 0 && !string.IsNullOrWhiteSpace(item.Name.ExtractText())) return item.Name.ExtractText();
            }

            var definition = CurrencyCatalog.GetDefinition(this);
            return definition?.Name ?? (resolvedItemId == 0 ? $"{Type} (Currently Unavailable)" : $"Currency #{resolvedItemId}");
        }
    }

    [JsonIgnore] public bool CanRemove => !CurrencyCatalog.IsSpecialCurrencyType(Type);

    /// <summary>Stable ImGui/config identity which avoids collisions between special trackers.</summary>
    [JsonIgnore] public string IdentityKey => CurrencyKey ?? CurrencyCatalog.GetDefinition(this)?.Key ?? $"{(int)Type}:{ItemId}";

    /// <summary>Reads the currency through the definition's verified counter route.</summary>
    [JsonIgnore] public CurrencyBalance Balance => GetBalance();

    // Kept for the existing overlay nodes and any external consumers.  Callers that make a
    // decision from the count must use Balance/IsBalanceAvailable so an unavailable count is
    // never interpreted as an actual zero.
    [JsonIgnore] public int CurrentCount => Balance.Count;

    [JsonIgnore] public bool IsBalanceAvailable => Balance.IsAvailable;

    [JsonIgnore] public string BalanceStatusText => Balance.Status switch {
        CurrencyCountStatus.NotLoaded => "Not loaded",
        CurrencyCountStatus.Unavailable => "Unavailable",
        CurrencyCountStatus.Unsupported => "Unsupported by this client version",
        _ => string.Empty,
    };

    [JsonIgnore] public bool HasWarning {
        get {
            var balance = Balance;
            return balance.IsAvailable && (Invert ? balance.Count < Threshold : balance.Count > Threshold);
        }
    }

    [JsonIgnore] public bool IsUnavailableSpecialCurrency => CurrencyCatalog.IsSpecialCurrencyType(Type) && ItemId == 0;

    private CurrencyBalance GetBalance() {
        if (!Service.ClientState.IsLoggedIn) return CurrencyBalance.NotLoaded;

        var definition = CurrencyCatalog.GetDefinition(this);
        var itemIdForCount = ItemId;

        try {
            if (definition is null) return GetInventoryItemBalance(itemIdForCount);

            return definition.CounterKind switch {
                CurrencyCounterKind.Gil => GetGilBalance(),
                CurrencyCounterKind.Mgp => GetMgpBalance(),
                CurrencyCounterKind.CompanySeal => GetCompanySealBalance(definition.NativeArgument),
                CurrencyCounterKind.WolfMarks => GetWolfMarksBalance(),
                CurrencyCounterKind.AlliedSeals => GetAlliedSealsBalance(),
                CurrencyCounterKind.Tomestone => GetTomestoneBalance(itemIdForCount),
                CurrencyCounterKind.CurrencyManager => GetCurrencyManagerBalance(itemIdForCount),
                CurrencyCounterKind.CosmicExploration => GetCosmicExplorationBalance(itemIdForCount),
                CurrencyCounterKind.OccultCrescent => GetOccultCrescentBalance(itemIdForCount),
                CurrencyCounterKind.Unsupported => CurrencyBalance.Unsupported,
                _ => GetInventoryItemBalance(itemIdForCount),
            };
        }
        // Signature resolution and content managers can transiently be unavailable while a
        // character changes state.  Treat that as unavailable instead of emitting a false zero.
        catch (Exception) {
            return CurrencyBalance.Unavailable;
        }
    }

    private CurrencyBalance GetInventoryItemBalance(uint resolvedItemId) {
        if (resolvedItemId == 0) return CurrencyBalance.Unavailable;

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return CurrencyBalance.NotLoaded;

        return CurrencyBalance.Available(inventoryManager->GetInventoryItemCount(
            resolvedItemId,
            Type is CurrencyType.HighQualityItem,
            false,
            false));
    }

    private static CurrencyBalance GetGilBalance() {
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null ? CurrencyBalance.NotLoaded : CurrencyBalance.Available((int)inventoryManager->GetGil());
    }

    private static CurrencyBalance GetMgpBalance() {
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null ? CurrencyBalance.NotLoaded : CurrencyBalance.Available((int)inventoryManager->GetGoldSaucerCoin());
    }

    private static CurrencyBalance GetCompanySealBalance(byte grandCompanyId) {
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null ? CurrencyBalance.NotLoaded : CurrencyBalance.Available((int)inventoryManager->GetCompanySeals(grandCompanyId));
    }

    private static CurrencyBalance GetWolfMarksBalance() {
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null ? CurrencyBalance.NotLoaded : CurrencyBalance.Available((int)inventoryManager->GetWolfMarks());
    }

    private static CurrencyBalance GetAlliedSealsBalance() {
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null ? CurrencyBalance.NotLoaded : CurrencyBalance.Available((int)inventoryManager->GetAlliedSeals());
    }

    private static CurrencyBalance GetTomestoneBalance(uint resolvedItemId) {
        if (resolvedItemId == 0) return CurrencyBalance.Unavailable;

        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null ? CurrencyBalance.NotLoaded : CurrencyBalance.Available((int)inventoryManager->GetTomestoneCount(resolvedItemId));
    }

    /// <summary>
    /// CurrencyManager owns normal currency and content-currency buckets.  HasItem is essential:
    /// GetItemCount alone returns zero for a missing bucket and would create incorrect alerts.
    /// </summary>
    private static CurrencyBalance GetCurrencyManagerBalance(uint resolvedItemId) {
        if (resolvedItemId == 0) return CurrencyBalance.Unavailable;

        var currencyManager = CurrencyManager.Instance();
        if (currencyManager == null || !currencyManager->HasItem(resolvedItemId)) return CurrencyBalance.Unavailable;

        return CurrencyBalance.Available((int)currencyManager->GetItemCount(resolvedItemId));
    }

    private static CurrencyBalance GetCosmicExplorationBalance(uint resolvedItemId) {
        var currencyManagerBalance = GetCurrencyManagerBalance(resolvedItemId);
        if (currencyManagerBalance.IsAvailable) return currencyManagerBalance;

        // WKSManager itself does not expose a balance.  AgentWKSHud is the maintained API-15
        // surface and only contains valid values after it reaches its ready state.
        var hud = AgentWKSHud.Instance();
        if (hud == null || hud->Info == null || hud->Info->State != 3) return CurrencyBalance.Unavailable;

        if (resolvedItemId == 45690) return CurrencyBalance.Available(Math.Max(0, hud->Info->CosmoCredits));

        return hud->Info->ZoneCreditsItemId == (int)resolvedItemId
            ? CurrencyBalance.Available(Math.Max(0, hud->Info->ZoneCredits))
            : CurrencyBalance.Unavailable;
    }

    private static CurrencyBalance GetOccultCrescentBalance(uint resolvedItemId) {
        var currencyManagerBalance = GetCurrencyManagerBalance(resolvedItemId);
        if (currencyManagerBalance.IsAvailable) return currencyManagerBalance;

        var data = PublicContentOccultCrescent.GetMKDData();
        var state = PublicContentOccultCrescent.GetState();
        if (data == null || state == null) return CurrencyBalance.Unavailable;

        // The maintained MKD data identifies the two currencies represented by the documented
        // Silver/Gold fields.  This covers South Horn pieces and North Horn obols without using
        // a guessed content-memory offset.
        var currencyItemIds = data->CurrencyItemIds;
        if (currencyItemIds.Length > 0 && currencyItemIds[0] == resolvedItemId) return CurrencyBalance.Available(state->Silver);
        if (currencyItemIds.Length > 1 && currencyItemIds[1] == resolvedItemId) return CurrencyBalance.Available(state->Gold);

        // The third MKD currency (Sanguine Cipher / Arcane Amulet) has no documented count field
        // in the current structures.  It remains explicitly unsupported unless CurrencyManager
        // begins exposing it in a later client version.
        for (var index = 2; index < currencyItemIds.Length; index++) {
            if (currencyItemIds[index] == resolvedItemId) return CurrencyBalance.Unsupported;
        }

        return CurrencyBalance.Unavailable;
    }

    private uint GetItemId() {
        TryRefreshDynamicScripItemId();

        if (CurrencyCatalog.IsSpecialCurrencyType(Type) && itemId is 0 or null) {
            itemId = CurrencyCatalog.GetSpecialItemId(Type);
        }

        return itemId ?? 0;
    }

    private void TryRefreshDynamicScripItemId() {
        var specialId = CurrencyCatalog.GetCurrencyManagerSpecialId(Type);
        if (specialId == 0 || !Service.ClientState.IsLoggedIn) return;

        var now = Environment.TickCount64;
        if (now - lastDynamicScripResolveTimestamp < 1000) return;

        lastDynamicScripResolveTimestamp = now;
        try {
            var currencyManager = CurrencyManager.Instance();
            if (currencyManager is null) return;

            var dynamicItemId = currencyManager->GetItemIdBySpecialId(specialId);
            if (dynamicItemId != 0 && currencyManager->HasItem(dynamicItemId)) {
                itemId = dynamicItemId;
                iconId = null;
            }
        }
        catch (Exception) {
            // The static fallback from CurrencyCatalog remains available until the
            // CurrencyManager special bucket can be read safely.
        }
    }
}
