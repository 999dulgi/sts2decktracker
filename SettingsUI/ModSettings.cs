using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace sts2decktracker
{
	public enum CardColorMode
	{
		None = 0,       // 무색 (모두 cardTitleOutlineCommon)
		UpgradeEnchant = 1, // 강화/인챈트만 색상 표시
		Full = 2        // 원래 설정대로 (희귀도 포함)
	}

	public class ModSettings
	{
		[JsonPropertyName("drawPileX")]
		public int DrawPileX { get; set; } = 0;

		[JsonPropertyName("drawPileY")]
		public int DrawPileY { get; set; } = 140;

		[JsonPropertyName("discardPileX")]
		public int DiscardPileX { get; set; } = 1670;

		[JsonPropertyName("discardPileY")]
		public int DiscardPileY { get; set; } = 140;

		[JsonPropertyName("topCardX")]
		public int TopCardX { get; set; } = -1;

		[JsonPropertyName("topCardY")]
		public int TopCardY { get; set; } = -1;

		[JsonPropertyName("draggable")]
		public bool Draggable { get; set; } = false;

		[JsonPropertyName("cardSize")]
		public int CardSize { get; set; } = 24;

		[JsonPropertyName("idleOpacity")]
		public float IdleOpacity { get; set; } = 0.3f;

		[JsonPropertyName("activeOpacity")]
		public float ActiveOpacity { get; set; } = 1.0f;

		[JsonPropertyName("idleDelaySeconds")]
		public float IdleDelaySeconds { get; set; } = 1.0f;

		[JsonPropertyName("showCardTooltip")]
		public bool ShowCardTooltip { get; set; } = false;

		[JsonPropertyName("rememberCustomPosition")]
		public bool RememberCustomPosition { get; set; } = false;

		[JsonPropertyName("cardColorMode")]
		public int CardColorModeInt { get; set; } = (int)CardColorMode.Full;

		[JsonIgnore]
		public CardColorMode CardColorMode
		{
			get => (CardColorMode)CardColorModeInt;
			set => CardColorModeInt = (int)value;
		}

		[JsonPropertyName("scrollable")]
		public bool Scrollable { get; set; } = false;

		[JsonPropertyName("scrollableAutoHeight")]
		public bool ScrollableAutoHeight { get; set; } = true;

		[JsonPropertyName("scrollableHeight")]
		public int ScrollableHeight { get; set; } = 400;

		public int PanelWidth => CardSize * 11 + 8;
		public int PanelHeight => CardSize * 20;
		public int CardHeight => CardSize + 8;
		public int CardWidth => CardSize * 10;
		public int CardImageWidth => CardWidth - CostIconSize * 2;
		public int CardCountFontSize => CardSize + 4;
		public int CardNameFontSize => CardSize;
		public int EnergyCostFontSize => CardSize;
		public int CostIconSize => CardSize + 8;

		private static readonly string ConfigPath = Path.Combine(
			System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
			"SlaytheSpire2",
			"DeckTracker.config.json"
		);

		public static ModSettings Load()
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					string json = File.ReadAllText(ConfigPath);
					var loaded = JsonSerializer.Deserialize<ModSettings>(json);
					if (loaded != null)
						return loaded;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettings] Failed to load: {ex.Message}");
			}
			return new ModSettings();
		}

		public void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
				var saveData = new
				{
					drawPileX = DrawPileX,
					drawPileY = DrawPileY,
					discardPileX = DiscardPileX,
					discardPileY = DiscardPileY,
					topCardX = TopCardX,
					topCardY = TopCardY,
					draggable = Draggable,
					showCardTooltip = ShowCardTooltip,
					rememberCustomPosition = RememberCustomPosition,
					cardColorMode = CardColorModeInt,
					scrollable = Scrollable,
					scrollableAutoHeight = ScrollableAutoHeight,
					scrollableHeight = ScrollableHeight,
					cardSize = CardSize,
					idleOpacity = IdleOpacity,
					activeOpacity = ActiveOpacity,
					idleDelaySeconds = IdleDelaySeconds
				};
				string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(ConfigPath, json);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettings] Failed to save: {ex.Message}");
			}
		}

		public void ResetToDefaults()
		{
			DrawPileX = 0;
			DrawPileY = 140;
			DiscardPileX = 1670;
			DiscardPileY = 140;
			CardSize = 28;
			IdleOpacity = 0.3f;
			ActiveOpacity = 1.0f;
			IdleDelaySeconds = 1.0f;
			ShowCardTooltip = true;
			Draggable = false;
			RememberCustomPosition = false;
			CardColorMode = CardColorMode.Full;
			Scrollable = false;
			ScrollableAutoHeight = true;
			ScrollableHeight = 400;
		}

		// ── UI ──────────────────────────────────────────────────────────────

		private static ModSettingsPanelNode _currentPanel = null;

		public static void RefreshForSelection(object infoContainer, object mod)
		{
			try
			{
				var container = (Node)infoContainer;

				if (!IsThisMod(mod))
				{
					SetDefaultNodesVisible(container, true);
					if (IsPanelValid() && _currentPanel.IsInsideTree())
						_currentPanel.Visible = false;
					return;
				}

				SetDefaultNodesVisible(container, false);

				if (!IsPanelValid() || !_currentPanel.IsInsideTree())
				{
					_currentPanel = null;
					_currentPanel = CreateSettingsPanel(container);
				}
				else
				{
					_currentPanel.Refresh();
					_currentPanel.Visible = true;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettings] Error in RefreshForSelection: {ex.Message}");
				GD.PrintErr($"[ModSettings] Stack trace: {ex.StackTrace}");
			}
		}

		private static bool IsThisMod(object mod)
		{
			if (mod == null)
				return false;
			try
			{
				var field = mod.GetType().GetField("assembly",
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (field != null)
				{
					string val = field.GetValue(mod)?.ToString() ?? "";
					if (val.Contains("sts2decktracker") || val.Contains("Slay the Spire 2 Deck Tracker"))
						return true;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettings] Error checking mod: {ex.Message}");
			}
			return false;
		}

		private static bool IsPanelValid() =>
			_currentPanel != null && GodotObject.IsInstanceValid(_currentPanel);

		private static void SetDefaultNodesVisible(Node container, bool visible)
		{
			foreach (var name in new[] { "ModTitle", "ModImage", "ModDescription" })
				container.GetNodeOrNull(name)?.Set("visible", visible);
		}

		private static ModSettingsPanelNode CreateSettingsPanel(Node container)
		{
			var scroll = new ScrollContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

			var panel = new ModSettingsPanelNode { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			scroll.AddChild(panel);

			container.AddChild(scroll);
			return panel;
		}
	}
}
