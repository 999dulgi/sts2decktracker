using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;

namespace sts2decktracker
{
	[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi._Ready))]
	public static class DeckTrackerInjectionPatch
	{
		private static CardListPanel _drawPilePanel;
		private static CardListPanel _discardPilePanel;
		private static TopCardPanel _topCardPanel;

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

				// Create Top Card panel (to the right of draw pile)
				_topCardPanel = new TopCardPanel();
				_topCardPanel.SetSettings(settings);
				_topCardPanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_topCardPanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_topCardPanel.Position = new Vector2(settings.DrawPileX + settings.PanelWidth + 4, settings.DrawPileY);
				__instance.AddChild(_topCardPanel);

				// Create Discard Pile panel (right side)
				_discardPilePanel = new CardListPanel();
				_discardPilePanel.SetPileType(PileType.Discard);
				_discardPilePanel.SetSettings(settings);
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

				if (_topCardPanel != null && GodotObject.IsInstanceValid(_topCardPanel))
				{
					_topCardPanel.SetSettings(settings);
					_topCardPanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_topCardPanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_topCardPanel.Position = new Vector2(settings.DrawPileX + settings.PanelWidth + 4, settings.DrawPileY);
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
						panel.Visible = false;
					else if (child is TopCardPanel topPanel)
						topPanel.Visible = false;
				}
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerCombatWonPatch] Failed to hide panels: {ex.Message}");
			}
		}
	}

	[HarmonyPatch]
	public static class CardPileTopTrackPatch
	{
		static System.Reflection.MethodInfo TargetMethod()
		{
			var abstractModelType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.AbstractModel");
			GD.Print($"[CardPileTopTrackPatch] AbstractModel type found: {abstractModelType != null}");
			var method = AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
				new[] { typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), abstractModelType, typeof(bool) });
			GD.Print($"[CardPileTopTrackPatch] TargetMethod found: {method != null}");
			return method;
		}

		public static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, CardPilePosition position)
		{
			GD.Print($"[CardPileTopTrackPatch] Prefix called: position={position}, pileType={newPile?.Type}");
			if (position == CardPilePosition.Top && newPile?.Type == PileType.Draw)
			{
				foreach (var card in cards)
				{
					GD.Print($"[CardPileTopTrackPatch] Marking top card: {card.Title}");
					TopCardTracker.MarkAsIntendedTop(card);
				}
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
				ModSettings.RefreshForSelection(__instance, mod);
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[ModdingScreenSettingsPatch] Failed to inject settings UI: {ex.Message}");
				GD.PrintErr($"[ModdingScreenSettingsPatch] Stack trace: {ex.StackTrace}");
			}
		}
	}
}
