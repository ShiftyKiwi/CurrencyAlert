using System.Collections.Generic;
using System.Linq;
using CurrencyAlert.Classes;
using CurrencyAlert.Windows;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiLib.CommandManager;
using KamiLib.Window;
using KamiToolKit;
using Lumina.Excel.Sheets;

namespace CurrencyAlert;

public sealed class CurrencyAlertPlugin : IDalamudPlugin {
    private const int ConfigVersion = 9;
    private const long ChatWarningDebounceMilliseconds = 5000;

    private ushort lastChatWarningTerritoryId;
    private long lastChatWarningTimestamp;

    public CurrencyAlertPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();

        System.Config = Configuration.Load();
        System.CommandManager = new CommandManager(Service.PluginInterface, "currencyalert", "calert");
        var configChanged = false;

        if (System.Config is { Currencies.Count: 0 } or { Currencies: null }) {
            Service.Log.Verbose("Generating initial currency list.");

            System.Config.Currencies = GenerateInitialList();
            configChanged = true;
        } else if (System.Config.Version != ConfigVersion) {
            Service.Log.Verbose("Migrating currency configuration.");
            MigrateConfiguration();
            configChanged = true;
        }

        if (NormalizeCurrencyOrder()) {
            configChanged = true;
        }

        if (configChanged) {
            System.Config.Version = ConfigVersion;
            System.Config.Save();
        }

        System.NativeController = new NativeController(Service.PluginInterface);
        System.WindowManager = new WindowManager(Service.PluginInterface);

        System.ConfigurationWindow = new ConfigurationWindow();
        System.WindowManager.AddWindow(System.ConfigurationWindow, WindowFlags.IsConfigWindow | WindowFlags.RequireLoggedIn);

        System.OverlayController = new OverlayController();

        Service.ClientState.TerritoryChanged += OnZoneChange;
        Service.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose() {
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        Service.Framework.Update -= OnFrameworkUpdate;

        System.CommandManager.Dispose();
        System.WindowManager.Dispose();
        System.OverlayController.Dispose();
        System.NativeController.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework) {
        if (!Service.ClientState.IsLoggedIn) return;

        System.OverlayController.Update();
    }

    private void OnZoneChange(ushort e) {
        if (System.Config is { ChatWarning: false }) return;

        var warningMessages = System.Config.Currencies
            .Where(currency => currency is { HasWarning: true, ChatWarning: true, Enabled: true })
            .Select(currency => $"{currency.Name} is {(currency.Invert ? "below" : "above")} threshold.")
            .Distinct()
            .ToList();

        if (warningMessages.Count == 0) return;

        var now = global::System.Environment.TickCount64;
        if (lastChatWarningTerritoryId == e && now - lastChatWarningTimestamp < ChatWarningDebounceMilliseconds) {
            return;
        }

        lastChatWarningTerritoryId = e;
        lastChatWarningTimestamp = now;

        foreach (var warningMessage in warningMessages) {
            Service.ChatGui.Print(warningMessage, "CurrencyAlert", 43);
        }
    }

    private static void MigrateConfiguration() {
        var sheet = Service.DataManager.GetExcelSheet<Item>();
        var obsoleteTomestoneIds = Service.DataManager
            .GetExcelSheet<TomestonesItem>()
            .Where(item => item.Tomestones.RowId is 0)
            .Select(item => item.Item.RowId)
            .ToHashSet();
        var managedSpecialCurrencyItemIds = GetManagedSpecialCurrencyTypes()
            .Select(type => new TrackedCurrency {
                Type = type, Threshold = 0,
            }.ItemId)
            .Where(itemId => itemId != 0)
            .ToHashSet();

        // Remove invalid/deprecated item entries for regular item tracking,
        // including legacy manual entries that now have dedicated special trackers.
        System.Config.Currencies.RemoveAll(currency => currency.Type switch {
            CurrencyType.Item or CurrencyType.HighQualityItem or CurrencyType.Collectable =>
                currency.ItemId == 0 ||
                sheet.GetRow(currency.ItemId).RowId == 0 ||
                obsoleteTomestoneIds.Contains(currency.ItemId) ||
                managedSpecialCurrencyItemIds.Contains(currency.ItemId),
            _ => false,
        });

        AddDefaultCurrencyIfMissing(CurrencyType.NonLimitedTomestone, 1400);
        AddDefaultCurrencyIfMissing(CurrencyType.LimitedTomestone, 1400);
        AddDefaultCurrencyIfMissing(CurrencyType.EvergreenTomestone, 1400);

        AddDefaultCurrencyIfMissing(CurrencyType.CurrentCraftersScrip, 3400);
        AddDefaultCurrencyIfMissing(CurrencyType.PreviousCraftersScrip, 3400);
        AddDefaultCurrencyIfMissing(CurrencyType.CurrentGatherersScrip, 3400);
        AddDefaultCurrencyIfMissing(CurrencyType.PreviousGatherersScrip, 3400);
    }

    private static void AddDefaultCurrencyIfMissing(CurrencyType type, int threshold) {
        if (System.Config.Currencies.Any(currency => currency.Type == type)) return;

        System.Config.Currencies.Add(new TrackedCurrency {
            Type = type, Threshold = threshold, Enabled = true,
        });
    }

    private static bool NormalizeCurrencyOrder() {
        var orderedCurrencies = System.Config.Currencies
            .OrderBy(GetCurrencySortOrder)
            .ToList();

        if (System.Config.Currencies.SequenceEqual(orderedCurrencies)) {
            return false;
        }

        System.Config.Currencies.Clear();
        System.Config.Currencies.AddRange(orderedCurrencies);
        return true;
    }

    private static int GetCurrencySortOrder(TrackedCurrency currency) => currency.Type switch {
        CurrencyType.Item when currency.ItemId == 20 => 0,
        CurrencyType.Item when currency.ItemId == 21 => 1,
        CurrencyType.Item when currency.ItemId == 22 => 2,
        CurrencyType.Item when currency.ItemId == 25 => 3,
        CurrencyType.Item when currency.ItemId == 36656 => 4,
        CurrencyType.Item when currency.ItemId == 27 => 5,
        CurrencyType.Item when currency.ItemId == 10307 => 6,
        CurrencyType.Item when currency.ItemId == 26533 => 7,
        CurrencyType.Item when currency.ItemId == 26807 => 8,
        CurrencyType.NonLimitedTomestone => 9,
        CurrencyType.LimitedTomestone => 10,
        CurrencyType.EvergreenTomestone => 11,
        CurrencyType.DiscontinuedTomestone => 12,
        CurrencyType.CurrentCraftersScrip => 13,
        CurrencyType.PreviousCraftersScrip => 14,
        CurrencyType.DiscontinuedCraftersScrip => 15,
        CurrencyType.CurrentGatherersScrip => 16,
        CurrencyType.PreviousGatherersScrip => 17,
        CurrencyType.DiscontinuedGatherersScrip => 18,
        CurrencyType.Item when currency.ItemId == 28063 => 19,
        _ => int.MaxValue,
    };

    private static List<CurrencyType> GetManagedSpecialCurrencyTypes() => [
        CurrencyType.NonLimitedTomestone,
        CurrencyType.LimitedTomestone,
        CurrencyType.EvergreenTomestone,
        CurrencyType.CurrentCraftersScrip,
        CurrencyType.PreviousCraftersScrip,
        CurrencyType.CurrentGatherersScrip,
        CurrencyType.PreviousGatherersScrip,
    ];

    private static List<TrackedCurrency> GenerateInitialList() => [
        new() { Type = CurrencyType.Item, ItemId = 20, Threshold = 75000, Enabled = true, }, // StormSeal
        new() { Type = CurrencyType.Item, ItemId = 21, Threshold = 75000, Enabled = true, }, // SerpentSeal
        new() { Type = CurrencyType.Item, ItemId = 22, Threshold = 75000, Enabled = true, }, // FlameSeal

        new() { Type = CurrencyType.Item, ItemId = 25, Threshold = 18000, Enabled = true, }, // WolfMarks
        new() { Type = CurrencyType.Item, ItemId = 36656, Threshold = 18000, Enabled = true, }, // TrophyCrystals

        new() { Type = CurrencyType.Item, ItemId = 27, Threshold = 3500, Enabled = true, }, // AlliedSeals
        new() { Type = CurrencyType.Item, ItemId = 10307, Threshold = 3500, Enabled = true, }, // CenturioSeals
        new() { Type = CurrencyType.Item, ItemId = 26533, Threshold = 3500, Enabled = true, }, // SackOfNuts

        new() { Type = CurrencyType.Item, ItemId = 26807, Threshold = 800, Enabled = true, }, // BicolorGemstones

        new() { Type = CurrencyType.NonLimitedTomestone, Threshold = 1400, Enabled = true, }, // StandardTomestone
        new() { Type = CurrencyType.LimitedTomestone, Threshold = 1400, Enabled = true, }, // LimitedTomestone
        new() { Type = CurrencyType.EvergreenTomestone, Threshold = 1400, Enabled = true, }, // EvergreenTomestone

        new() { Type = CurrencyType.CurrentCraftersScrip, Threshold = 3400, Enabled = true, }, // Current Crafters' Scrip
        new() { Type = CurrencyType.PreviousCraftersScrip, Threshold = 3400, Enabled = true, }, // Previous Crafters' Scrip
        new() { Type = CurrencyType.CurrentGatherersScrip, Threshold = 3400, Enabled = true, }, // Current Gatherers' Scrip
        new() { Type = CurrencyType.PreviousGatherersScrip, Threshold = 3400, Enabled = true, }, // Previous Gatherers' Scrip

        new() { Type = CurrencyType.Item, ItemId = 28063, Threshold = 7500, Enabled = true, }, // Skybuilders scripts
    ];
}
