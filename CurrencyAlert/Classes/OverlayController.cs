using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiLib.Extensions;
using KamiToolKit;
using KamiToolKit.Classes;

namespace CurrencyAlert.Classes;

public unsafe class OverlayController : NameplateAddonController {

    public OverlayListNode? OverlayListNode;

    private static string ListNodeConfigPath => Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "ListNode.style.json");
    
    public readonly CurrencyWarningNode SampleNode = new() {
        Height = 32.0f,
        IsVisible = true,
        EnableEventFlags = true,
        Tooltip = "Overlay from CurrencyAlert plugin",
    };

    public OverlayController() : base(Service.PluginInterface) {
        SampleNode.Load();
        
        OnAttach += AttachNodes;
        OnDetach += DetachNodes;

        Enable();
    }

    public override void Dispose() {
        OnAttach -= AttachNodes;
        OnDetach -= DetachNodes;
        
        base.Dispose();
    }

    private void AttachNodes(AddonNamePlate* addonNamePlate) {
        OverlayListNode = new OverlayListNode {
            NodeId = 100000001,
            Size = new Vector2(600.0f, 200.0f),
            Position = new Vector2(1920.0f, 1024.0f) / 2.0f,
            BackgroundColor = KnownColor.Aqua.Vector() with { W = 0.40f },
            IsVisible = true,
        };
        OverlayListNode.Load(ListNodeConfigPath);
        System.NativeController.AttachNode(OverlayListNode, addonNamePlate->RootNode, NodePosition.AsFirstChild);
    }

    private void DetachNodes(AddonNamePlate* addonNamePlate) {
        if (OverlayListNode is null) return;

        System.NativeController.DetachNode(OverlayListNode, () => {
            OverlayListNode.Dispose();
            OverlayListNode = null;
        });
    }

    public void Update() {
        if (OverlayListNode is null) return;

        var activeWarnings = System.Config.Currencies
            .Where(currency => currency is { HasWarning: true, Enabled: true, ShowInOverlay: true })
            .ToList();

        var overlayAllowed = System.Config.OverlayEnabled && !(Service.Condition.IsBoundByDuty() && System.Config.HideInDuties);
        var showOverlayContainer = activeWarnings.Count > 0 || System.ConfigurationWindow.IsOpen;
        OverlayListNode.IsVisible = overlayAllowed && showOverlayContainer;
        
        OverlayListNode.UpdateWarnings(activeWarnings);
    }

    public void Save() {
        OverlayListNode?.Save(ListNodeConfigPath);
        SampleNode.Save();
    }
}
