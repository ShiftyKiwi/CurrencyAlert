using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace CurrencyAlert.Classes;

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

public unsafe class TrackedCurrency {
    private const uint UnavailableSpecialIconId = 60071;

    private const uint DiscontinuedCraftersScripId = 25199;
    private const uint DiscontinuedGatherersScripId = 25200;
    private const uint PreviousCraftersScripId = 33913;
    private const uint PreviousGatherersScripId = 33914;
    private const uint CurrentCraftersScripId = 41784;
    private const uint CurrentGatherersScripId = 41785;

    private uint? iconId;
    private uint? itemId;

    public required CurrencyType Type { get; init; }

    [JsonIgnore] public IDalamudTextureWrap Icon => Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
        HiRes = true, ItemHq = Type is CurrencyType.HighQualityItem, IconId = IconId,
    }).GetWrapOrEmpty();

    public uint ItemId {
        get => GetItemId();
        init => itemId = IsSpecialCurrency() ? GetItemId() : value;
    }

    // Don't save iconId because special currencies change between patches.
    [JsonIgnore] public uint IconId {
        get {
            if (ItemId == 0 && IsSpecialCurrency()) {
                return UnavailableSpecialIconId;
            }

            return iconId ??= Service.DataManager.GetExcelSheet<Item>().GetRow(ItemId).Icon;
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

    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    public string Name {
        get {
            if (ItemId == 0 && IsSpecialCurrency()) {
                return $"{Type} (Currently Unavailable)";
            }

            return field ??= Service.DataManager.GetExcelSheet<Item>().GetRow(ItemId).Name.ExtractText();
        }
    }

    [JsonIgnore] public bool CanRemove => !IsSpecialCurrency();

    [JsonIgnore] public int CurrentCount => ItemId == 0 ? 0 : InventoryManager.Instance()->GetInventoryItemCount(ItemId, Type is CurrencyType.HighQualityItem, false, false);

    [JsonIgnore] public bool HasWarning => Invert ? CurrentCount < Threshold : CurrentCount > Threshold;

    [JsonIgnore] public bool IsUnavailableSpecialCurrency => IsSpecialCurrency() && ItemId == 0;

    private uint GetItemId() {
        if (Type switch {
                CurrencyType.DiscontinuedCraftersScrip => true,
                CurrencyType.DiscontinuedGatherersScrip => true,
                CurrencyType.PreviousCraftersScrip => true,
                CurrencyType.PreviousGatherersScrip => true,
                CurrencyType.CurrentCraftersScrip => true,
                CurrencyType.CurrentGatherersScrip => true,
                _ => false,
            }) {
            return Type switch {
                CurrencyType.DiscontinuedCraftersScrip => DiscontinuedCraftersScripId,
                CurrencyType.DiscontinuedGatherersScrip => DiscontinuedGatherersScripId,
                CurrencyType.PreviousCraftersScrip => PreviousCraftersScripId,
                CurrencyType.PreviousGatherersScrip => PreviousGatherersScripId,
                CurrencyType.CurrentCraftersScrip => CurrentCraftersScripId,
                CurrencyType.CurrentGatherersScrip => CurrentGatherersScripId,
                _ => 0,
            };
        }

        // Force regenerate itemId for special currencies.
        if (IsSpecialCurrency() && itemId is 0 or null) {
            itemId = Type switch {
                CurrencyType.NonLimitedTomestone => Service.DataManager.GetExcelSheet<TomestonesItem>().FirstOrDefault(item => item.Tomestones.RowId is 2).Item.RowId,
                CurrencyType.LimitedTomestone => Service.DataManager.GetExcelSheet<TomestonesItem>().FirstOrDefault(item => item.Tomestones.ValueNullable is { WeeklyLimit: > 0 }).Item.RowId,
                CurrencyType.EvergreenTomestone => Service.DataManager.GetExcelSheet<TomestonesItem>().FirstOrDefault(item => item.Tomestones.RowId is 1).Item.RowId,
                CurrencyType.DiscontinuedTomestone => Service.DataManager.GetExcelSheet<TomestonesItem>().FirstOrDefault(item => item.Tomestones.RowId is 4).Item.RowId,
                _ => throw new Exception($"ItemId not initialized for type: {Type}"),
            };
        }

        return itemId ?? 0;
    }

    private bool IsSpecialCurrency() => Type switch {
        CurrencyType.NonLimitedTomestone => true,
        CurrencyType.LimitedTomestone => true,
        CurrencyType.EvergreenTomestone => true,
        CurrencyType.DiscontinuedTomestone => true,
        CurrencyType.DiscontinuedCraftersScrip => true,
        CurrencyType.DiscontinuedGatherersScrip => true,
        CurrencyType.PreviousCraftersScrip => true,
        CurrencyType.PreviousGatherersScrip => true,
        CurrencyType.CurrentCraftersScrip => true,
        CurrencyType.CurrentGatherersScrip => true,
        _ => false,
    };
}
