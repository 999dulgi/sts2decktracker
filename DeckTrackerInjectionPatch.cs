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
		internal static Vector2? _savedDrawCustomPos;
		internal static Vector2? _savedDiscardCustomPos;
		internal static bool _isReturningToMainMenu = false;

		public static void Postfix(NCombatUi __instance)
		{
			try
			{
				_isReturningToMainMenu = false;
				var settings = ModSettings.Load();

				var drawPos = new Vector2(settings.DrawPileX, settings.DrawPileY);
				_drawPilePanel = new CardListPanel();
				_drawPilePanel.SetPileType(PileType.Draw);
				_drawPilePanel.SetSettings(settings);
				_drawPilePanel.SetDefaultPosition(drawPos);
				_drawPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_drawPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_drawPilePanel.Position = drawPos;
				__instance.AddChild(_drawPilePanel);

				_topCardPanel = new TopCardPanel();
				_topCardPanel.SetSettings(settings);
				_topCardPanel.SetDrawPilePanel(_drawPilePanel);
				_topCardPanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_topCardPanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				__instance.AddChild(_topCardPanel);

				var discardPos = new Vector2(settings.DiscardPileX, settings.DiscardPileY);
				_discardPilePanel = new CardListPanel();
				_discardPilePanel.SetPileType(PileType.Discard);
				_discardPilePanel.SetSettings(settings);
				_discardPilePanel.SetDefaultPosition(discardPos);
				_discardPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_discardPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				_discardPilePanel.Position = discardPos;
				__instance.AddChild(_discardPilePanel);

				if (_savedDrawCustomPos.HasValue)
					_drawPilePanel.SetCustomPosition(_savedDrawCustomPos.Value);
				if (_savedDiscardCustomPos.HasValue)
					_discardPilePanel.SetCustomPosition(_savedDiscardCustomPos.Value);
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
					_drawPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_drawPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_drawPilePanel.SetSettings(settings);
					_drawPilePanel.SetDefaultPosition(new Vector2(settings.DrawPileX, settings.DrawPileY));
					Godot.Callable.From(_drawPilePanel.UpdatePositionPublic).CallDeferred();
				}

				if (_discardPilePanel != null && IsNodeValid(_discardPilePanel))
				{
					_discardPilePanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_discardPilePanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_discardPilePanel.SetSettings(settings);
					_discardPilePanel.SetDefaultPosition(new Vector2(settings.DiscardPileX, settings.DiscardPileY));
					Godot.Callable.From(_discardPilePanel.UpdatePositionPublic).CallDeferred();
				}

				if (_topCardPanel != null && GodotObject.IsInstanceValid(_topCardPanel))
				{
					_topCardPanel.SetSettings(settings);
					_topCardPanel.CustomMinimumSize = new Vector2(settings.PanelWidth, settings.PanelHeight);
					_topCardPanel.Size = new Vector2(settings.PanelWidth, settings.PanelHeight);
				}
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerInjectionPatch] Failed to apply settings: {ex.Message}");
			}
		}

		internal static void SaveCustomPosition(PileType pileType, Vector2 pos)
		{
			if (pileType == PileType.Draw)
				_savedDrawCustomPos = pos;
			else
				_savedDiscardCustomPos = pos;
		}

		internal static void ClearCustomPosition(PileType pileType)
		{
			if (pileType == PileType.Draw)
				_savedDrawCustomPos = null;
			else
				_savedDiscardCustomPos = null;
		}

		internal static void OnReturnToMainMenu()
		{
			try
			{
				_isReturningToMainMenu = true;
				_drawPilePanel?.ResetTemporaryState();
				_discardPilePanel?.ResetTemporaryState();
				var settings = ModSettings.Load();
				if (settings.RememberCustomPosition)
				{
					var drawPos = _drawPilePanel?.GetCustomPosition();
					var discardPos = _discardPilePanel?.GetCustomPosition();
					if (drawPos.HasValue)
					{
						settings.DrawPileX = (int)drawPos.Value.X;
						settings.DrawPileY = (int)drawPos.Value.Y;
					}
					if (discardPos.HasValue)
					{
						settings.DiscardPileX = (int)discardPos.Value.X;
						settings.DiscardPileY = (int)discardPos.Value.Y;
					}
					settings.Save();
				}
				// 메인 메뉴 복귀 시 임시 위치 초기화 (런 종료)
				_savedDrawCustomPos = null;
				_savedDiscardCustomPos = null;
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerInjectionPatch] OnReturnToMainMenu failed: {ex.Message}");
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
	public static class NGameReturnToMenuPatch
	{
		static System.Reflection.MethodInfo TargetMethod()
		{
			var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.NGame");
			return AccessTools.Method(type, "ReturnToMainMenu");
		}

		static void Prefix()
		{
			DeckTrackerInjectionPatch.OnReturnToMainMenu();
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
