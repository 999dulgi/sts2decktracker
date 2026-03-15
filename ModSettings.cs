using System;
using System.IO;
using System.Text.Json;

namespace sts2modtest
{
	/// <summary>
	/// Deck Tracker mod settings - Edit the JSON config file to customize
	/// Config file location: %APPDATA%/Roaming/SlaytheSpire2/DeckTracker.config.json
	/// 
	/// Available settings:
	/// - panelWidth/panelHeight: Size of deck tracker panels (default: 250x400)
	/// - drawPileX/drawPileY: Position of draw pile panel (default: 0, 140)
	/// - discardPileX/discardPileY: Position of discard pile panel (default: -250, 140)
	/// - cardSize: Overall card size - affects card height and all text sizes (default: 24)
	/// </summary>
	public class ModSettings
	{
		// Panel positions
		[System.Text.Json.Serialization.JsonPropertyName("drawPileX")]
		public int DrawPileX { get; set; } = 0;
		
		[System.Text.Json.Serialization.JsonPropertyName("drawPileY")]
		public int DrawPileY { get; set; } = 140;
		
		[System.Text.Json.Serialization.JsonPropertyName("discardPileX")]
		public int DiscardPileX { get; set; } = -250;
		
		[System.Text.Json.Serialization.JsonPropertyName("discardPileY")]
		public int DiscardPileY { get; set; } = 140;
		
		// Card size - controls all panel dimensions, card sizes, and text sizes
		[System.Text.Json.Serialization.JsonPropertyName("cardSize")]
		public int CardSize { get; set; } = 24;
		
		// Transparency settings
		[System.Text.Json.Serialization.JsonPropertyName("idleOpacity")]
		public float IdleOpacity { get; set; } = 0.3f;  // Semi-transparent when idle (0.0 = fully transparent, 1.0 = fully opaque)
		
		[System.Text.Json.Serialization.JsonPropertyName("activeOpacity")]
		public float ActiveOpacity { get; set; } = 1.0f;  // Fully opaque when cards are drawn
		
		[System.Text.Json.Serialization.JsonPropertyName("idleDelaySeconds")]
		public float IdleDelaySeconds { get; set; } = 2.0f;  // Seconds to wait before fading to idle opacity
		
		// Computed properties based on CardSize - everything scales automatically
		public int PanelWidth => CardSize * 11 + 8;  // Panel width scales with card size (24 -> 240px)
		public int PanelHeight => CardSize * 20;  // Panel height scales with card size (24 -> 480px)
		public int CardHeight => CardSize + 8;  // Card height scales with size
		public int CardWidth => CardSize * 10;  // Card width = panel width (24 -> 240px)
		public int CardImageWidth => CardWidth - CostIconSize * 2;  // Image width = card width - (2 icons)
		public int CardCountFontSize => CardSize + 4;  // Slightly larger for count
		public int CardNameFontSize => CardSize;  // Base size for name
		public int EnergyCostFontSize => CardSize;  // Slightly larger for cost
		public int CostIconSize => CardSize + 8;  // Cost icon size matches card size

		private static readonly string ConfigPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"SlaytheSpire2",
			"DeckTracker.config.json"
		);

		/// <summary>
		/// Load settings from JSON file, or return defaults if file doesn't exist
		/// </summary>
		public static ModSettings Load()
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					string json = File.ReadAllText(ConfigPath);
					var loadedSettings = JsonSerializer.Deserialize<ModSettings>(json);
					if (loadedSettings != null)
					{
						return loadedSettings;
					}
					else
					{
						Godot.GD.PrintErr($"[ModSettings] Deserialization returned null");
					}
				}
			}
			catch (Exception ex)
			{
				Godot.GD.PrintErr($"[ModSettings] Failed to load settings: {ex.Message}");
				Godot.GD.PrintErr($"[ModSettings] Stack trace: {ex.StackTrace}");
			}
			return new ModSettings();
		}

		/// <summary>
		/// Save current settings to JSON file
		/// </summary>
		public void Save()
		{
			try
			{
				// Ensure directory exists
				string directory = Path.GetDirectoryName(ConfigPath);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				// Save only user-editable fields
				var saveData = new
				{
					drawPileX = DrawPileX,
					drawPileY = DrawPileY,
					discardPileX = DiscardPileX,
					discardPileY = DiscardPileY,
					cardSize = CardSize,
					idleOpacity = IdleOpacity,
					activeOpacity = ActiveOpacity,
					idleDelaySeconds = IdleDelaySeconds
				};

				var options = new JsonSerializerOptions { WriteIndented = true };
				string json = JsonSerializer.Serialize(saveData, options);
				File.WriteAllText(ConfigPath, json);
			}
			catch (Exception ex)
			{
				Godot.GD.PrintErr($"[ModSettings] Failed to save settings: {ex.Message}");
			}
		}
	}
}
