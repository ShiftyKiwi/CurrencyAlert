using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using CurrencyAlert.Classes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Window;
using KamiLib.Window.SelectionWindows;
using KamiToolKit.Classes;
using Lumina.Excel.Sheets;

namespace CurrencyAlert.Windows;

public class ConfigurationWindow : TabbedSelectionWindow<TrackedCurrency> {

    public ConfigurationWindow() : base("CurrencyAlert Configuration Window", new Vector2(450.0f, 400.0f)) {
        System.CommandManager.RegisterCommand(new CommandHandler {
            Delegate = _ => Toggle(), ActivationPath = "/",
        });
    }

    protected override List<TrackedCurrency> Options => System.Config.Currencies;
    
    protected override float SelectionListWidth => 150.0f;
    
    protected override float SelectionItemHeight => 20.0f;
    
    protected override bool AllowChildScroll => false;
    
    protected override string SelectionListTabName => "Tracked Currencies";
    
    protected override bool ShowListButton => true;

    protected override List<ITabItem> Tabs { get; } = [
        new GeneralSettingsTab(),
        new ListNodeSettingsTab(),
        new CurrencyNodeSettingsTab(),
    ];

    protected override void DrawListOption(TrackedCurrency option) {
        if (option.IsUnavailableSpecialCurrency) {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 3.0f * ImGuiHelpers.GlobalScale);
            ImGui.Image(Service.TextureProvider.GetFromGameIcon(60071).GetWrapOrEmpty().Handle, ImGuiHelpers.ScaledVector2(24.0f));

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(option.Name);

            return;
        }

        if (option is { Name: var name, Icon: { } icon }) {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 3.0f * ImGuiHelpers.GlobalScale);
            ImGui.Image(icon.Handle, ImGuiHelpers.ScaledVector2(24.0f));

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(name);
        }
        else {
            ImGui.TextColored(KnownColor.OrangeRed.Vector(), $"Error, unable to display currency. ItemId: {option.ItemId}");
        }
    }

    protected override void DrawSelectedOption(TrackedCurrency selectedOption) {
        DrawHeaderAndWatermark(selectedOption);
        DrawCurrentStatus(selectedOption);
        DrawSettings(selectedOption);
    }

    private void DrawHeaderAndWatermark(TrackedCurrency currency) {
        var region = ImGui.GetContentRegionAvail();
        var minDimension = Math.Min(region.X, region.Y);

        if (currency.IsUnavailableSpecialCurrency) {
            var text = currency.Name;
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2.0f - textSize.X / 2.0f);
            ImGui.Text(text);
            ImGui.Separator();

            var areaStart = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(region.X / 2.0f - minDimension / 2.0f);
            ImGui.Image(Service.TextureProvider.GetFromGameIcon(60071).GetWrapOrEmpty().Handle, new Vector2(minDimension), Vector2.Zero, Vector2.One, Vector4.One with {
                W = 0.10f,
            });
            ImGui.SetCursorPos(areaStart);

            return;
        }

        if (currency is { Name: var name }) {
            var textSize = ImGui.CalcTextSize(name);
            ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2.0f - textSize.X / 2.0f);
            ImGui.Text(name);
            ImGui.Separator();

            var areaStart = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(region.X / 2.0f - minDimension / 2.0f);
            ImGui.Image(currency.Icon.Handle, new Vector2(minDimension), Vector2.Zero, Vector2.One, Vector4.One with {
                W = 0.10f,
            });
            ImGui.SetCursorPos(areaStart);
        }
    }

    private void DrawCurrentStatus(TrackedCurrency currency) {
        if (currency is not { CurrentCount: var currentCount, Threshold: var threshold }) return;

        var color = ((float) currentCount / threshold) switch {
            < 0.75f => currency.Invert ? KnownColor.Red.Vector() : KnownColor.White.Vector(),
            < 0.85f => currency.Invert ? KnownColor.Red.Vector() : KnownColor.Orange.Vector(),
            < 0.95f => currency.Invert ? KnownColor.Red.Vector() : KnownColor.OrangeRed.Vector(),
            > 0.95f and < 1.00f => KnownColor.Red.Vector(),
            >= 1.00f and < 1.05f => currency.Invert ? KnownColor.OrangeRed.Vector() : KnownColor.Red.Vector(),
            >= 1.05f and < 1.15f => currency.Invert ? KnownColor.Orange.Vector() : KnownColor.Red.Vector(),
            >= 1.15f => currency.Invert ? KnownColor.White.Vector() : KnownColor.Red.Vector(),
            _ => KnownColor.White.Vector(),
        };

        DrawCurrentStatusTable(currentCount, color, threshold);

        ImGuiHelpers.ScaledDummy(5.0f);
    }

    private static void DrawCurrentStatusTable(int currentCount, Vector4 color, int threshold) {
        using var table = ImRaii.Table("CurrentStatusTable", 3);
        if (!table) return;
        
        ImGui.TableSetupColumn("##CurrentAmount", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##Slash", ImGuiTableColumnFlags.WidthFixed, 5.0f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("##ThresholdAmount", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        var text = $"{currentCount}";
        var currentCountSize = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - currentCountSize.X);
        ImGui.TextColored(color, text);

        ImGui.TableNextColumn();
        ImGui.Text("/");

        ImGui.TableNextColumn();
        ImGui.Text(threshold.ToString());
    }

    private void DrawSettings(TrackedCurrency currency) {
        using var id = ImRaii.PushId(currency.ItemId.ToString());
        
        var configChanged = false;

        configChanged |= ImGui.Checkbox($"Enable", ref currency.Enabled);

        ImGuiHelpers.ScaledDummy(5.0f);

        configChanged |= ImGuiTweaks.Checkbox("Chat Warning", ref currency.ChatWarning, "When amount is above threshold, print a message to chat when changing zones");
        configChanged |= ImGuiTweaks.Checkbox("Invert", ref currency.Invert, "Warn when below the threshold instead of above");
        configChanged |= ImGuiTweaks.Checkbox("Overlay", ref currency.ShowInOverlay, "Allows this currency to show in the overlay");
        configChanged |= ImGuiTweaks.Checkbox("Overlay Show Name", ref currency.ShowItemName, "Show item name in the overlay");

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.InputTextWithHint("##WarningText","Warning Text", ref currency.WarningText, 1024)) {
            configChanged = true;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.PushItemWidth(50.0f * ImGuiHelpers.GlobalScale);
        configChanged |= ImGui.InputInt($"Threshold", ref currency.Threshold, 0, 0);

        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - 23.0f * ImGuiHelpers.GlobalScale);
        using (ImRaii.Disabled(!(ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl && currency.CanRemove))) {
            if (ImGuiTweaks.IconButtonWithSize(Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle, FontAwesomeIcon.Trash, "Delete", new Vector2(ImGui.GetContentRegionAvail().X, 23.0f * ImGuiHelpers.GlobalScale))) {
                System.Config.Currencies.Remove(currency);
                DeselectItem();
                System.Config.Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            ImGui.SetTooltip(currency.CanRemove ? "Hold Shift + Control while clicking to delete this currency" : "Special currencies cannot be removed");
        }

        if (configChanged) {
            System.Config.Save();
        }
    }

    protected override void DrawExtraButton() {
        using var spacingTable = ImRaii.Table("buttonTable", 3, ImGuiTableFlags.SizingStretchSame);
        if (!spacingTable) return;

        ImGui.TableNextColumn();
        if (ImGuiTweaks.IconButtonWithSize(Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle, FontAwesomeIcon.Plus, "addNewCurrency", ImGui.GetContentRegionAvail(), "Add New Normal Item")) {
            TryCloseAndRemoveItemListWindow();

            System.WindowManager.AddWindow(new ItemSelectionWindow(Service.PluginInterface) {
                MultiSelectionCallback = selected => AddSelectedItems(selected, CurrencyType.Item),
            }, WindowFlags.OpenImmediately);
        }

        ImGui.TableNextColumn();
        if (ImGui.Button($"{SeIconChar.HighQuality.ToIconString()}##hqSearch", ImGui.GetContentRegionAvail())) {
            TryCloseAndRemoveItemListWindow();

            System.WindowManager.AddWindow(new HighQualityItemSelectionWindow(Service.PluginInterface) {
                MultiSelectionCallback = selected => AddSelectedItems(selected, CurrencyType.HighQualityItem),
            }, WindowFlags.OpenImmediately);
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Add New High Quality Item");
        }

        ImGui.TableNextColumn();
        if (ImGui.Button($"{SeIconChar.Collectible.ToIconString()}##collectableSearch", ImGui.GetContentRegionAvail())) {
            TryCloseAndRemoveItemListWindow();

            System.WindowManager.AddWindow(new CollectableItemSelectionWindow(Service.PluginInterface) {
                MultiSelectionCallback = selected => AddSelectedItems(selected, CurrencyType.Collectable),
            }, WindowFlags.OpenImmediately);
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Add New Collectable Item");
        }
    }

    private static void AddSelectedItems(List<Item> selected, CurrencyType type) {
        foreach (var item in selected.Where(item => !System.Config.Currencies.Any(currency => currency.ItemId == item.RowId))) {
            System.Config.Currencies.Add(new TrackedCurrency {
                Enabled = true, Threshold = 1000, Type = type, ItemId = item.RowId,
            });
        }
    }

    private void TryCloseAndRemoveItemListWindow() {
        if (System.WindowManager.GetWindow<SelectionWindowBase<Item>>() is { } existingWindow) {
            existingWindow.Close();
            System.WindowManager.RemoveWindow(existingWindow);
        }
    }

    public override void OnClose() {
        System.OverlayController.Save();
        System.Config.Save();
    }

    public override void OnTabChanged() {
        System.OverlayController.Save();
        System.Config.Save();
    }
}

public class GeneralSettingsTab : ITabItem {
    public string Name => "Settings";
    
    public bool Disabled => false;

    public void Draw() {
        var configChanged = false;

        ImGuiTweaks.Header("General Settings");
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox("Enable Chat Warnings", ref System.Config.ChatWarning);
        }

        ImGuiTweaks.Header("Overlay Settings");
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox("Hide in Duties", ref System.Config.HideInDuties);
        }
        
        if (configChanged) {
            System.Config.Save();
        }
    }
}

public class ListNodeSettingsTab : ITabItem {
    public string Name => "Warning List Style";
    
    public bool Disabled => false;
    
    public void Draw() {
        var listNode = System.OverlayController.OverlayListNode;
        if (listNode is null) return;
                
        ImGuiTweaks.Header("General Settings");

        ImGuiTweaks.SetFullWidth();
        if (ImGui.Checkbox("Enable", ref System.Config.OverlayEnabled)) {
            listNode.IsVisible = System.Config.OverlayEnabled;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        var enableMoving = listNode.EnableMoving;
        if (ImGui.Checkbox("Allow Moving", ref enableMoving)) {
            listNode.EnableMoving = enableMoving;
        }
        
        var enableResizing = listNode.EnableResizing;
        if (ImGui.Checkbox("Allow Resizing", ref enableResizing)) {
            listNode.EnableResizing = enableResizing;
        }
        
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Checkbox("Disable Interaction", ref System.Config.DisableInteraction)) {
            listNode.EnableListEvents = !System.Config.DisableInteraction;
        }
        ImGuiComponents.HelpMarker("Disables the tooltip 'Overlay from CurrencyAlert Plugin'\n" +
                                   "and disables click interactability on the main list node\n\n" +
                                   "Does not effect individual warning nodes");
        
        ImGuiTweaks.Header("Warning List Overlay Style");
        DrawSimpleModeConfig(listNode);
    }

    private static void DrawSimpleModeConfig(OverlayListNode listNode) {
        using var table = ImRaii.Table("simple_mode_table", 2);
        if (!table) return;
        
        ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("##config", ImGuiTableColumnFlags.WidthStretch, 2.0f);
                
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Text("Position");
                        
        ImGui.TableNextColumn();
        var position = listNode.Position;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.DragFloat2("##position", ref position, 0.75f, 0.0f, 5000.0f)) {
            listNode.Position = position;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("Size");
                
        ImGui.TableNextColumn();
        var size = listNode.Size;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.DragFloat2("##size", ref size, 0.75f, 0.0f, 5000.0f)) {
            listNode.Size = size;
        }
             
        ImGui.TableNextColumn();
        ImGui.Text("Scale");
        
        ImGui.TableNextColumn();
        ImGuiTweaks.SetFullWidth();
        var scale = listNode.Scale.X;
        if (ImGui.DragFloat("##Scale", ref scale, 0.005f, 0.10f, 3.0f)) {
            listNode.ScaleX = scale;
            listNode.ScaleY = scale;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("Background Color");
                
        ImGui.TableNextColumn();
        var backgroundColor = listNode.BackgroundColor;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.ColorEdit4("##BackgroundColor", ref backgroundColor, ImGuiColorEditFlags.AlphaPreviewHalf)) {
            listNode.BackgroundColor = backgroundColor;
        }
                
        ImGui.TableNextColumn();
        ImGui.Text("List Orientation");
                
        ImGui.TableNextColumn();
        var orientation = listNode.LayoutOrientation;
        ImGuiTweaks.SetFullWidth();
        if (ComboHelper.EnumCombo("##Orientation", ref orientation)) {
            listNode.LayoutOrientation = orientation;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("List Anchor");
                
        ImGui.TableNextColumn();
        var anchor = listNode.LayoutAnchor;
        ImGuiTweaks.SetFullWidth();
        if (ComboHelper.EnumCombo("##Anchor", ref anchor)) {
            listNode.LayoutAnchor = anchor;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("First Item Spacing");
        
        ImGui.TableNextColumn();
        var firstItemSpacing = listNode.FirstItemSpacing;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.DragFloat("##FirstItemSpacing", ref firstItemSpacing, 0.10f, -30.0f, 5000.0f)) {
            listNode.FirstItemSpacing = firstItemSpacing;
        }

        ImGui.TableNextColumn();
        ImGui.Text("Item Spacing");
        
        ImGui.TableNextColumn();
        var categorySpacing = listNode.ItemSpacing;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.DragFloat("##ItemSpacing", ref categorySpacing, 0.10f, -30.0f, 5000.0f)) {
            listNode.ItemSpacing = categorySpacing;
        }

        ImGui.TableNextColumn();
        ImGui.Text("Show Background");
        
        ImGui.TableNextColumn();
        var background = listNode.ShowBackground;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.Checkbox("##BackgroundVisible", ref background)) {
            listNode.ShowBackground = background;
        }

        ImGui.TableNextColumn();
        ImGui.Text("Show Border");

        ImGui.TableNextColumn();
        var border = listNode.ShowBorder;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.Checkbox("##BorderVisible", ref border)) {
            listNode.ShowBorder = border;
        }
    }
}

public class CurrencyNodeSettingsTab : ITabItem {
    public string Name => "Currency Node Style";
    
    public bool Disabled => false;
    
    public void Draw() {
        var listNode = System.OverlayController.OverlayListNode;
        if (listNode is null) return;

        var sampleNode = System.OverlayController.SampleNode;
                
        ImGuiTweaks.Header("Currency Node Overlay Style");
        DrawSimpleModeConfig(sampleNode, listNode);
    }

    private void DrawSimpleModeConfig(CurrencyWarningNode sampleNode, OverlayListNode overlayNode) {
        using var table = ImRaii.Table("simple_mode_table", 2);
        if (!table) return;
        
        ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("##config", ImGuiTableColumnFlags.WidthStretch, 2.0f);
                
        ImGui.TableNextRow();
        
        ImGui.TableNextColumn();
        ImGui.Text("Text Color");
        
        ImGui.TableNextColumn();
        var textColor = sampleNode.TextColor;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.ColorEdit4("##TextColor", ref textColor, ImGuiColorEditFlags.AlphaPreviewHalf)) {
            sampleNode.TextColor = textColor;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("Text Font");
        
        ImGui.TableNextColumn();
        var textFont = sampleNode.LabelFont;
        ImGuiTweaks.SetFullWidth();
        if (ComboHelper.EnumCombo("##TextFont", ref textFont)) {
            sampleNode.LabelFont = textFont;
        }

        ImGui.TableNextColumn();
        ImGui.Text("Text Size");
        
        ImGui.TableNextColumn();
        var textSize = sampleNode.TextSize;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.InputInt("##TextSize", ref textSize)) {
            sampleNode.TextSize = textSize;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("Show Icon");
        
        ImGui.TableNextColumn();
        var showIcon = sampleNode.ShowIcon;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.Checkbox("##ShowIcon", ref showIcon)) {
            sampleNode.ShowIcon = showIcon;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("Show Text");
        
        ImGui.TableNextColumn();
        var showText = sampleNode.ShowText;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.Checkbox("##ShowText", ref showText)) {
            sampleNode.ShowText = showText;
        }
        
        ImGui.TableNextColumn();
        ImGui.Text("Show Item Count");

        ImGui.TableNextColumn();
        var showItemCount = sampleNode.ShowItemCount;
        ImGuiTweaks.SetFullWidth();
        if (ImGui.Checkbox("##ShowItemCount", ref showItemCount)) {
            sampleNode.ShowItemCount = showItemCount;
        }
        
        ApplyAll(sampleNode, overlayNode);
    }

    private void ApplyAll(CurrencyWarningNode referenceNode, OverlayListNode overlayListNode) {
        foreach (var node in overlayListNode.NodeList) {
            node.Load(referenceNode, "Position", "Size");
            node.RecalculateLayout();
        }
    }
}
