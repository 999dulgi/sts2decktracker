using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace sts2modtest
{
	[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi._Ready))]
	public static class DeckTrackerInjectionPatch
	{
		private static CardListPanel _drawPilePanel;
		private static CardListPanel _discardPilePanel;

		public static void Postfix(NCombatUi __instance)
		{
			try
			{
				var settings = ModSettings.Load();

				// Create Draw Pile panel (left side)
				_drawPilePanel = new CardListPanel();
				_drawPilePanel.SetPileType(PileType.Draw);
				_drawPilePanel.SetSettings(settings);
				_drawPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_drawPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_drawPilePanel.Position = new Vector2(settings.DrawPileX, settings.DrawPileY);
				__instance.AddChild(_drawPilePanel);
				
				// Create Discard Pile panel (right side)
				_discardPilePanel = new CardListPanel();
				_discardPilePanel.SetPileType(PileType.Discard);
				_discardPilePanel.SetSettings(settings);
				_discardPilePanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
				_discardPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_discardPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_discardPilePanel.Position = new Vector2(settings.DiscardPileX, settings.DiscardPileY);
				__instance.AddChild(_discardPilePanel);
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerInjectionPatch] Failed to inject deck tracker: {ex.Message}");
				GD.PrintErr($"[DeckTrackerInjectionPatch] Stack trace: {ex.StackTrace}");
			}
		}

		public static void ApplySettings(ModSettings settings)
		{
			try
			{
				// Apply settings to both panels if they exist and are valid
				if (_drawPilePanel != null && IsNodeValid(_drawPilePanel))
				{
					_drawPilePanel.SetSettings(settings);
					_drawPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_drawPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_drawPilePanel.Position = new Vector2(settings.DrawPileX, settings.DrawPileY);
					GD.Print($"[DeckTrackerInjectionPatch] Applied settings to draw pile panel");
				}
				else if (_drawPilePanel != null)
				{
					GD.Print($"[DeckTrackerInjectionPatch] Draw pile panel is disposed, skipping");
				}

				if (_discardPilePanel != null && IsNodeValid(_discardPilePanel))
				{
					_discardPilePanel.SetSettings(settings);
					_discardPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_discardPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_discardPilePanel.Position = new Vector2(settings.DiscardPileX, settings.DiscardPileY);
					GD.Print($"[DeckTrackerInjectionPatch] Applied settings to discard pile panel");
				}
				else if (_discardPilePanel != null)
				{
					GD.Print($"[DeckTrackerInjectionPatch] Discard pile panel is disposed, skipping");
				}
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerInjectionPatch] Failed to apply settings: {ex.Message}");
			}
		}

		private static bool IsNodeValid(CardListPanel panel)
		{
			if (panel == null)
			{
				return false;
			}

			try
			{
				// Try to access IsInsideTree to check if node is still valid
				return panel.IsInsideTree();
			}
			catch
			{
				return false;
			}
		}
	}

	[HarmonyPatch(typeof(NCombatUi), "OnCombatWon")]
	public static class DeckTrackerCombatWonPatch
	{
		public static void Postfix(NCombatUi __instance)
		{
			try
			{
				// Hide panels on combat won
				foreach (Node child in __instance.GetChildren())
				{
					if (child is CardListPanel panel)
					{
						panel.Visible = false;
					}
				}
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerCombatWonPatch] Failed to hide panels: {ex.Message}");
			}
		}
	}

	[HarmonyPatch]
	public static class ModdingScreenSettingsPatch
	{
		// Patch NModInfoContainer.Fill to inject our settings UI
		static System.Reflection.MethodInfo TargetMethod()
		{
			var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer");
			var modType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.Mod");
			var method = AccessTools.Method(type, "Fill", new[] { modType });
			
			GD.Print($"[ModdingScreenSettingsPatch] TargetMethod called");
			GD.Print($"[ModdingScreenSettingsPatch] NModInfoContainer type found: {type != null}");
			GD.Print($"[ModdingScreenSettingsPatch] Mod type found: {modType != null}");
			GD.Print($"[ModdingScreenSettingsPatch] Fill method found: {method != null}");
			
			return method;
		}

		static void Postfix(object __instance, object mod)
		{
			try
			{
				GD.Print($"[ModdingScreenSettingsPatch] Postfix called!");
				GD.Print($"[ModdingScreenSettingsPatch] __instance type: {__instance?.GetType().Name ?? "null"}");
				GD.Print($"[ModdingScreenSettingsPatch] mod type: {mod?.GetType().Name ?? "null"}");
				
				ModSettingsUI.RefreshForSelection(__instance, mod);
				
				GD.Print($"[ModdingScreenSettingsPatch] RefreshForSelection completed");
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[ModdingScreenSettingsPatch] Failed to inject settings UI: {ex.Message}");
				GD.PrintErr($"[ModdingScreenSettingsPatch] Stack trace: {ex.StackTrace}");
			}
		}
	}
}
