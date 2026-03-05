using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.System;
using Newtonsoft.Json;

namespace CurrencyAlert.Classes;

[JsonObject(MemberSerialization.OptIn)]
public class CurrencyWarningNode : SimpleComponentNode {
    [JsonProperty] private readonly BackgroundImageNode background;
    [JsonProperty] private readonly TextNode warningText;
    [JsonProperty] private readonly CurrencyIconNode currencyIcon;

	private static string CurrencyNodeConfigPath => Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "CurrencyNode.style.json");
    
    public CurrencyWarningNode() {
        Margin = new Spacing(5.0f);

        background = new BackgroundImageNode {
            NodeId = 2,
            Position = new Vector2(-5.0f, -5.0f),
            Size = new Vector2(610.0f, 42.0f),
            Color = KnownColor.Black.Vector() with { W = 0.30f },
            WrapMode = 1,
            IsVisible = false,
        };
        System.NativeController.AttachNode(background, this);

        currencyIcon = new CurrencyIconNode {
            NodeId = 3,
            Position = new Vector2(5.0f, 0.0f),
            Size = new Vector2(32.0f, 32.0f),
            IsVisible = true,
            Margin = new Spacing { Right = 15.0f },
        };
        System.NativeController.AttachNode(currencyIcon, this);

        warningText = new TextNode {
            NodeId = 4,
            FontSize = 24,
            FontType = FontType.Axis,
            Size = new Vector2(600.0f, 32.0f),
            Position = new Vector2(42.0f, 0.0f),
            TextColor = KnownColor.White.Vector(),
            TextFlags = TextFlags.AutoAdjustNodeSize | TextFlags.Edge,
            TextOutlineColor = KnownColor.Black.Vector(),
            IsVisible = true,
        };
        System.NativeController.AttachNode(warningText, this);
    }

    public TrackedCurrency? Currency {
        get;
        set {
            field = value;
            
            if (value is null) {
                IsVisible = false;
            }
            else {
                UpdateFromCurrency();
            }
        }
    }

    public void UpdateFromCurrency() {
        if (Currency is null) return;
        
        currencyIcon.IconId = Currency.IconId;
        warningText.Text = Currency.ShowItemName ? $"{Currency.Name} {Currency.WarningText}" : $"{Currency.WarningText}";
        // Ensure text sizing follows current content even if older style files saved a large fixed width.
        var textDrawSize = warningText.GetTextDrawSize(warningText.Text);
        warningText.Width = MathF.Max(1.0f, textDrawSize.X + 6.0f);
        currencyIcon.ItemCount = Currency.CurrentCount;
        IsVisible = true;
        
        RecalculateLayout();
    }

    [JsonProperty] public bool ShowIcon {
        get => currencyIcon.IsVisible;
        set => currencyIcon.IsVisible = value;
    }

    [JsonProperty] public bool ShowText {
        get => warningText.IsVisible;
        set => warningText.IsVisible = value;
    }

    [JsonProperty] public int TextSize {
        get => (int) warningText.FontSize;
        set => warningText.FontSize = (uint) value;
    }

    [JsonProperty] public Vector4 TextColor {
        get => warningText.TextColor;
        set => warningText.TextColor = value;
    }

    [JsonProperty] public bool ShowItemCount {
        get => currencyIcon.ShowItemCount;
        set => currencyIcon.ShowItemCount = value;
    }

    [JsonProperty] public FontType LabelFont {
        get => warningText.FontType;
        set => warningText.FontType = value;
    }

    public void RecalculateLayout() {
        var currentPosition = 0.0f;

        if (currencyIcon.IsVisible) {
            currencyIcon.X = currentPosition;
            currentPosition += currencyIcon.LayoutSize.X;
        }

        if (warningText.IsVisible) {
            warningText.X = currentPosition;
            currentPosition += warningText.Width;
        }
        
        background.Width = currentPosition + 10.0f;
        RecalculateSize();
    }

    private void RecalculateSize() {
        var width = 0.0f;

        if (ShowIcon) {
            width += currencyIcon.LayoutSize.X;
        }

        if (ShowText) {
            width += warningText.LayoutSize.X;
        }

        Size = ShowText ? new Vector2(width, MathF.Max(currencyIcon.Height, warningText.Height)) : new Vector2(width, currencyIcon.Height);
        currencyIcon.Y = Height / 2.0f - currencyIcon.Height / 2.0f;
        warningText.Y = Height / 2.0f - warningText.Height / 2.0f;
    }

    public void Load()
        => Load(CurrencyNodeConfigPath);

    public void Save()
        => Save(CurrencyNodeConfigPath);
}
