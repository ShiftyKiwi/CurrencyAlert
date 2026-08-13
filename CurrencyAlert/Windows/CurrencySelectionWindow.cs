using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CurrencyAlert.Classes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using KamiLib.Window;

namespace CurrencyAlert.Windows;

/// <summary>
/// A curated, category-aware picker for currency definitions.  This intentionally does not
/// derive from <see cref="SelectionWindowBase{T}"/>: that base class owns the complete draw
/// flow and has no extension point for the catalog-wide availability checkbox or category
/// headers required by this browser.
/// </summary>
public sealed class CurrencySelectionWindow : Window {
    private const float RowHeight = 42.0f;

    private readonly List<CurrencyDefinition> definitions = [];
    private readonly List<CategoryGroup> visibleCategories = [];
    private readonly HashSet<string> selectedKeys = new(StringComparer.Ordinal);

    private string searchText = string.Empty;
    private bool showUnavailable;
    private bool needsVisibleRefresh = true;

    /// <summary>
    /// Lets callers choose the familiar Add Currency behavior (many selections followed by a
    /// single confirmation) or a one-at-a-time picker.
    /// </summary>
    public bool AllowMultiSelect { get; init; } = true;

    /// <summary>
    /// Optional duplicate check supplied by the configuration owner.  It is deliberately based
    /// on <see cref="CurrencyDefinition"/> so special currencies can use their stable key rather
    /// than relying solely on an item id.
    /// </summary>
    public Func<CurrencyDefinition, bool>? IsAlreadyTracked { get; init; }

    /// <summary>
    /// Called with the selected catalog definitions.  This is useful when the caller wants to
    /// decide how to add or migrate the resulting tracked currencies.
    /// </summary>
    public Action<List<CurrencyDefinition>>? MultiSelectionCallback { get; init; }

    /// <summary>Called with the single selected definition when one-at-a-time behavior is used.</summary>
    public Action<CurrencyDefinition?>? SingleSelectionCallback { get; init; }

    /// <summary>
    /// Convenience callback for callers that want the catalog to construct tracked currencies.
    /// It is invoked alongside <see cref="MultiSelectionCallback"/> on confirmation.
    /// </summary>
    public Action<List<TrackedCurrency>>? MultiTrackedCurrencySelectionCallback { get; init; }

    /// <summary>
    /// Convenience single-selection counterpart to
    /// <see cref="MultiTrackedCurrencySelectionCallback"/>.
    /// </summary>
    public Action<TrackedCurrency?>? SingleTrackedCurrencySelectionCallback { get; init; }

    public CurrencySelectionWindow() : base("Add Currency", new Vector2(560.0f, 650.0f)) {
        ReloadCatalog();
        UnCollapseOrShow();
    }

    /// <summary>
    /// Re-evaluates the visible list after a caller updates its duplicate predicate or changes
    /// its tracked-currency collection while this window remains open.
    /// </summary>
    public void Refresh() => needsVisibleRefresh = true;

    public override void OnClose() {
        base.OnClose();
        ParentWindowManager.RemoveWindow(this);
    }

    protected override void DrawContents() {
        DrawFilters();
        EnsureVisibleDefinitions();
        DrawResults();
        DrawConfirmationButtons();
    }

    private void DrawFilters() {
        if (ImGui.IsWindowAppearing()) {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint("##currencySearch", "Search currencies or categories...", ref searchText, 256, ImGuiInputTextFlags.AutoSelectAll)) {
            needsVisibleRefresh = true;
        }

        if (ImGui.Checkbox("Show legacy / unavailable currencies", ref showUnavailable)) {
            // The catalog owns the availability classification. Passing false deliberately
            // keeps Future, Legacy, and Obsolete definitions out of the normal browser.
            ReloadCatalog();
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Includes legacy, obsolete, and future-gated catalog entries.");
        }

        if ((ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)) && visibleCategories.Count > 0) {
            var first = visibleCategories[0].Definitions[0];
            ToggleSelection(first);
        }

        ImGui.Separator();
    }

    private void DrawResults() {
        var available = ImGui.GetContentRegionAvail();
        var footerHeight = 38.0f * ImGuiHelpers.GlobalScale;
        var resultsHeight = Math.Max(100.0f * ImGuiHelpers.GlobalScale, available.Y - footerHeight);

        using var results = ImRaii.Child("currencyResults", new Vector2(available.X, resultsHeight), true, ImGuiWindowFlags.NoMove);
        if (!results) return;

        if (visibleCategories.Count is 0) {
            var text = "No currencies match the current filters.";
            var textSize = ImGui.CalcTextSize(text);
            var center = ImGui.GetContentRegionAvail() / 2.0f;
            ImGui.SetCursorPos(center - textSize / 2.0f);
            ImGui.TextUnformatted(text);
            return;
        }

        foreach (var category in visibleCategories) {
            using var categoryId = ImRaii.PushId(category.Name);
            ImGui.TextColored(new Vector4(0.72f, 0.76f, 0.86f, 1.0f), category.Name);
            ImGui.Separator();

            foreach (var definition in category.Definitions) {
                DrawDefinition(definition);
            }

            ImGuiHelpers.ScaledDummy(4.0f);
        }
    }

    private void DrawDefinition(CurrencyDefinition definition) {
        var key = definition.Key;
        var selected = selectedKeys.Contains(key);
        var rowSize = new Vector2(ImGui.GetContentRegionAvail().X, RowHeight * ImGuiHelpers.GlobalScale);
        var cursor = ImGui.GetCursorPos();

        using var id = ImRaii.PushId(key);
        if (ImGui.Selectable("##currency", selected, ImGuiSelectableFlags.AllowItemOverlap, rowSize)) {
            // A second click must select this row before confirming it.  Toggling first
            // would otherwise remove the selection and invoke the single-selection
            // callback with null.
            if (!AllowMultiSelect && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                selectedKeys.Clear();
                selectedKeys.Add(key);
                ConfirmSelection();
                return;
            }

            ToggleSelection(definition);
        }

        ImGui.SetCursorPos(cursor);
        using var row = ImRaii.Child("currencyDetails", rowSize, false, ImGuiWindowFlags.NoInputs);
        if (!row) return;

        var rowStart = ImGui.GetCursorPos();
        DrawIcon(definition);

        // Do not rely on SameLine here: the nested no-input child can report an
        // inline cursor position that overlaps the 32px icon.  A fixed icon
        // column also keeps both text lines aligned across DPI scales.
        var textX = rowStart.X + 40.0f * ImGuiHelpers.GlobalScale;
        var textStart = new Vector2(textX, rowStart.Y);
        ImGui.SetCursorPos(new Vector2(textX, textStart.Y + 2.0f * ImGuiHelpers.GlobalScale));
        ImGui.TextUnformatted(definition.Name);

        var availability = definition.Availability.ToString();
        if (!string.Equals(availability, "Current", StringComparison.OrdinalIgnoreCase)) {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.72f, 0.62f, 0.32f, 1.0f), $"({availability})");
        }

        ImGui.SetCursorPos(new Vector2(textX, textStart.Y + 20.0f * ImGuiHelpers.GlobalScale));
        ImGui.TextColored(new Vector4(0.60f, 0.60f, 0.60f, 1.0f), definition.Category);
    }

    private static void DrawIcon(CurrencyDefinition definition) {
        var iconSize = ImGuiHelpers.ScaledVector2(32.0f, 32.0f);
        if (definition.IconId is not 0) {
            var texture = Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                IconId = definition.IconId,
                HiRes = true,
            }).GetWrapOrEmpty();
            ImGui.Image(texture.Handle, iconSize);
            return;
        }

        // Keep text aligned for native currencies that do not have a game icon yet.
        ImGuiHelpers.ScaledDummy(32.0f);
    }

    private void DrawConfirmationButtons() {
        ImGuiHelpers.ScaledDummy(5.0f);

        using (ImRaii.Disabled(selectedKeys.Count is 0)) {
            if (ImGui.Button(AllowMultiSelect ? "Add selected" : "Add currency", ImGuiHelpers.ScaledVector2(120.0f, 25.0f))) {
                ConfirmSelection();
            }
        }

        ImGui.SameLine();
        if (AllowMultiSelect && ImGui.Button("Clear selection", ImGuiHelpers.ScaledVector2(110.0f, 25.0f))) {
            selectedKeys.Clear();
        }

        var cancelWidth = 100.0f * ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - cancelWidth);
        if (ImGui.Button("Cancel", ImGuiHelpers.ScaledVector2(100.0f, 25.0f))) {
            CancelSelection();
        }
    }

    private void ReloadCatalog() {
        definitions.Clear();
        definitions.AddRange(CurrencyCatalog.GetDefinitions(showUnavailable));
        selectedKeys.IntersectWith(definitions.Select(definition => definition.Key));
        needsVisibleRefresh = true;
    }

    private void EnsureVisibleDefinitions() {
        if (!needsVisibleRefresh) return;

        visibleCategories.Clear();
        var matchingDefinitions = definitions
            .Where(definition => !IsTracked(definition) && MatchesSearch(definition))
            .OrderBy(definition => definition.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase);

        CategoryGroup? currentCategory = null;
        foreach (var definition in matchingDefinitions) {
            if (currentCategory is null || !string.Equals(currentCategory.Name, definition.Category, StringComparison.Ordinal)) {
                currentCategory = new CategoryGroup(definition.Category);
                visibleCategories.Add(currentCategory);
            }

            currentCategory.Definitions.Add(definition);
        }

        needsVisibleRefresh = false;
    }

    private bool IsTracked(CurrencyDefinition definition)
        => IsAlreadyTracked?.Invoke(definition) ?? false;

    private bool MatchesSearch(CurrencyDefinition definition) {
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        return definition.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || definition.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || definition.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || definition.ItemId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || definition.Availability.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || selectedKeys.Contains(definition.Key);
    }

    private void ToggleSelection(CurrencyDefinition definition) {
        if (selectedKeys.Contains(definition.Key)) {
            selectedKeys.Remove(definition.Key);
            return;
        }

        if (!AllowMultiSelect) {
            selectedKeys.Clear();
        }

        selectedKeys.Add(definition.Key);
    }

    private void ConfirmSelection() {
        var selection = definitions
            .Where(definition => selectedKeys.Contains(definition.Key) && !IsTracked(definition))
            .ToList();

        MultiSelectionCallback?.Invoke(selection);
        MultiTrackedCurrencySelectionCallback?.Invoke(selection.Select(definition => definition.CreateTrackedCurrency()).ToList());

        CurrencyDefinition? first = selection.Count is 0 ? default : selection[0];
        SingleSelectionCallback?.Invoke(first);
        SingleTrackedCurrencySelectionCallback?.Invoke(selection.Count is 0 ? null : selection[0].CreateTrackedCurrency());
        Close();
    }

    private void CancelSelection() {
        MultiSelectionCallback?.Invoke([]);
        MultiTrackedCurrencySelectionCallback?.Invoke([]);
        SingleSelectionCallback?.Invoke(null);
        SingleTrackedCurrencySelectionCallback?.Invoke(null);
        Close();
    }

    private sealed class CategoryGroup(string name) {
        public string Name { get; } = name;
        public List<CurrencyDefinition> Definitions { get; } = [];
    }
}
