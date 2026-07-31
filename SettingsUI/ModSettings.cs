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

	public enum CardSortMode
	{
		Random = 0,        // 기존 방식: 무작위 섞기
		Alphabetical = 1,  // 이름순
		Cost = 2           // 코스트순 (동일 코스트면 이름순)
	}

	public class ModSettings
	{
		[JsonPropertyName("drawPileX")]
		public int DrawPileX { get; set; } = 0;

		[JsonPropertyName("drawPileY")]
		public int DrawPileY { get; set; } = 140;

		[JsonPropertyName("discardPileX")]
		public int DiscardPileX { get; set; } = 1660;

		[JsonPropertyName("discardPileY")]
		public int DiscardPileY { get; set; } = 140;

		[JsonPropertyName("topCardX")]
		public int TopCardX { get; set; } = -1;

		[JsonPropertyName("topCardY")]
		public int TopCardY { get; set; } = -1;

		[JsonPropertyName("draggable")]
		public bool Draggable { get; set; } = false;

		[JsonPropertyName("cardWidth")]
		public int CardWidthInt { get; set; } = 280;

		[JsonPropertyName("cardHeight")]
		public int CardHeightInt { get; set; } = 36;

		[JsonPropertyName("idleOpacity")]
		public float IdleOpacity { get; set; } = 0.3f;

		[JsonPropertyName("activeOpacity")]
		public float ActiveOpacity { get; set; } = 1.0f;

		[JsonPropertyName("idleDelaySeconds")]
		public float IdleDelaySeconds { get; set; } = 1.0f;

		[JsonPropertyName("intentTransparency")]
		public bool IntentTransparency { get; set; } = false;

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

		[JsonPropertyName("cardSortMode")]
		public int CardSortModeInt { get; set; } = (int)CardSortMode.Random;

		[JsonIgnore]
		public CardSortMode CardSortMode
		{
			get => (CardSortMode)CardSortModeInt;
			set => CardSortModeInt = (int)value;
		}

		[JsonPropertyName("scrollable")]
		public bool Scrollable { get; set; } = false;

		[JsonPropertyName("scrollableAutoHeight")]
		public bool ScrollableAutoHeight { get; set; } = true;

		[JsonPropertyName("scrollableHeight")]
		public int ScrollableHeight { get; set; } = 400;

		public int CardHeight => CardHeightInt;
		public int CardWidth => CardWidthInt;
		public int PanelWidth => CardWidth + 20;
		public int PanelHeight => (CardHeight + 3) * 14 + 17;
		public int CardImageWidth => CardWidth - CostIconSize * 2;
		public int CardCountFontSize => CardHeight - 4;
		public int CardNameFontSize => CardHeight - 8;
		public int EnergyCostFontSize => CardHeight - 8;
		public int CostIconSize => CardHeight;

		private static readonly string ConfigPath = Path.Combine(
			Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
			"DeckTracker.cfg"
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
					cardSortMode = CardSortModeInt,
					scrollable = Scrollable,
					scrollableAutoHeight = ScrollableAutoHeight,
					scrollableHeight = ScrollableHeight,
					cardWidth = CardWidthInt,
					cardHeight = CardHeightInt,
					idleOpacity = IdleOpacity,
					activeOpacity = ActiveOpacity,
					idleDelaySeconds = IdleDelaySeconds,
					intentTransparency = IntentTransparency
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
			CardWidthInt = 280;
			CardHeightInt = 36;
			IdleOpacity = 0.3f;
			ActiveOpacity = 1.0f;
			IdleDelaySeconds = 1.0f;
			IntentTransparency = false;
			ShowCardTooltip = true;
			Draggable = false;
			RememberCustomPosition = false;
			CardColorMode = CardColorMode.Full;
			CardSortMode = CardSortMode.Random;
			Scrollable = false;
			ScrollableAutoHeight = true;
			ScrollableHeight = 400;
		}
	}
}
