using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Newtonsoft.Json;

namespace CurrencyAlert.Classes;

[JsonObject(MemberSerialization.OptIn)]
public class OverlayListNode : SimpleComponentNode {

	[JsonProperty] private ListBoxNode listNode;

	public readonly List<CurrencyWarningNode> NodeList = [];

	public OverlayListNode() {
		listNode = new ListBoxNode {
			NodeId = 4,
			LayoutAnchor = LayoutAnchor.TopLeft,
			IsVisible = System.Config.OverlayEnabled,
			FirstItemSpacing = 10.0f,
			ItemSpacing = 10.0f,
			ClipListContents = true,
			Tooltip = "Overlay from CurrencyAlert plugin",
		};

		listNode.AddEvent(AddonEventType.MouseClick, OpenConfigurationWindow);
		
		System.NativeController.AttachNode(listNode, this);
	}

	public override float Width {
		get => base.Width;
		set {
			base.Width = value;
			listNode.Width = value;
		}
	}

	public override float Height {
		get => base.Height;
		set {
			base.Height = value;
			listNode.Height = value;
		}
	}

	public Vector4 BackgroundColor {
		get => listNode.BackgroundColor;
		set => listNode.BackgroundColor = value;
	}

	public LayoutOrientation LayoutOrientation {
		get => listNode.LayoutOrientation;
		set => listNode.LayoutOrientation = value;
	}

	public float ItemSpacing {
		get => listNode.ItemSpacing;
		set => listNode.ItemSpacing = value;
	}

	public bool ShowBackground {
		get => listNode.ShowBackground;
		set => listNode.ShowBackground = value;
	}

	public bool ShowBorder {
		get => listNode.ShowBorder;
		set => listNode.ShowBorder = value;
	}

	public LayoutAnchor LayoutAnchor {
		get => listNode.LayoutAnchor;
		set => listNode.LayoutAnchor = value;
	}

	public float FirstItemSpacing {
		get => listNode.FirstItemSpacing;
		set => listNode.FirstItemSpacing = value;
	}

	public bool EnableListEvents {
		get => listNode.EnableEventFlags;
		set => listNode.EnableEventFlags = value;
	}

	public void UpdateWarnings(List<TrackedCurrency> activeWarnings) {
		// Get a list of warnings that we need to remove
		var oldWarnings = NodeList.Where(node => activeWarnings.All(warning => warning != node.Currency)).ToList();

		foreach (var oldWarning in oldWarnings) {
			listNode.RemoveNode(oldWarning);
			NodeList.Remove(oldWarning);
			oldWarning.Dispose();
		}
		
		// Get a list of warnings that we need to add
		var newWarnings = activeWarnings.Where(warning => NodeList.All(node => node.Currency != warning)).ToList();

		foreach (var newWarning in newWarnings) {
			var newWarningNode = new CurrencyWarningNode {
				Height = 32.0f,
				Currency = newWarning,
				IsVisible = true,
				EnableEventFlags = true,
				Tooltip = "Overlay from CurrencyAlert plugin",
			};
			
			newWarningNode.Load();
			newWarningNode.AddEvent(AddonEventType.MouseClick, OpenConfigurationWindow);
			
			listNode.AddNode(newWarningNode);
			NodeList.Add(newWarningNode);
		}
		
		foreach (var node in NodeList) {
			node.UpdateFromCurrency();
			node.RecalculateLayout();
		}

		listNode.EnableEventFlags = NodeList.Count > 0 && !System.Config.DisableInteraction;
		listNode.RecalculateLayout();
	}

	private void OpenConfigurationWindow(AddonEventData _)
		=> System.ConfigurationWindow.UnCollapseOrToggle();
}
