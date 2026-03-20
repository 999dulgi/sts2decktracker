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

				_drawPilePanel = new CardListPanel();
				_drawPilePanel.SetPileType(PileType.Draw);
				_drawPilePanel.SetSettings(settings);
				_drawPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_drawPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_drawPilePanel.Position = new Vector2(settings.DrawPileX, settings.DrawPileY);
				__instance.AddChild(_drawPilePanel);

				_topCardPanel = new TopCardPanel();
				_topCardPanel.SetSettings(settings);
				_topCardPanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_topCardPanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_topCardPanel.Position = new Vector2(settings.DrawPileX + settings.PanelWidth + 4, settings.DrawPileY);
				__instance.AddChild(_topCardPanel);

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
				if (_drawPilePanel != null && IsNodeValid(_drawPilePanel))
				{
					_drawPilePanel.SetSettings(settings);
					_drawPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_drawPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_drawPilePanel.Position = new Vector2(settings.DrawPileX, settings.DrawPileY);
				}

				if (_discardPilePanel != null && IsNodeValid(_discardPilePanel))
				{
					_discardPilePanel.SetSettings(settings);
					_discardPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_discardPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_discardPilePanel.Position = new Vector2(settings.DiscardPileX, settings.DiscardPileY);
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
				return false;
			try
			{
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
			return AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
				new[] { typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), abstractModelType, typeof(bool) });
		}

		public static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, CardPilePosition position)
		{
			if (position == CardPilePosition.Top && newPile?.Type == PileType.Draw)
			{
				foreach (var card in cards)
					TopCardTracker.MarkAsIntendedTop(card);
			}
		}
	}

	[HarmonyPatch]
	public static class ModdingScreenSettingsPatch
	{
		static System.Reflection.MethodInfo TargetMethod()
		{
			var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer");
			var modType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.Mod");
			return AccessTools.Method(type, "Fill", new[] { modType });
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
