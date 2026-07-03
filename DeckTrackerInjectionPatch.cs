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
					{
						panel.Visible = false;
						panel.QueueFree();
					}
					else if (child is TopCardPanel topPanel)
					{
						topPanel.Visible = false;
						topPanel.QueueFree();
					}
				}
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[DeckTrackerCombatWonPatch] Failed to remove panels: {ex.Message}");
			}
		}
	}

	// CardPileCmd.Add 호출 시점에 이미 실제 position과 대상 cards를 알 수 있으므로, index로
	// CardPile.AddInternal을 역추적할 필요 없이 여기서 바로 "명시적으로 CardPilePosition.Top을
	// 지정한 경우"만 마킹한다. CardPileCmd.Add를 거치지 않는 직접 호출(Transform 등)은 여기서
	// 다루지 않고 아래의 identity 기반 훅에서 별도로 처리한다.
	[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool))]
	public static class CardPileTopTrackPatch
	{
		public static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, CardPilePosition position)
		{
			if (newPile?.Type != PileType.Draw || position != CardPilePosition.Top)
				return;
			foreach (var card in cards)
				TopCardTracker.MarkAsIntendedTop(card);
		}
	}

	// CardCmd.Transform은 원본 카드를 CardModel.RemoveFromCurrentPile()로 먼저 지운 뒤(=CardPile.RemoveInternal이
	// ContentsChanged를 동기적으로 발생시켜 TopCardTracker.PruneCards가 즉시 실행됨) 한참 뒤에야
	// AfterTransformedFrom()을 호출한다. 즉, AfterTransformedFrom 시점에는 이미 원본 카드가 pile에서
	// 빠져나가 PruneCards에 의해 마킹이 지워진 뒤라 TopCardTracker.IsIntendedTop이 항상 False로 나온다.
	// 그래서 마킹 유무를 "지워지기 직전"인 RemoveFromCurrentPile Prefix 시점에 미리 스냅샷해둔다.
	[HarmonyPatch(typeof(CardModel), nameof(CardModel.RemoveFromCurrentPile))]
	public static class CardRemoveFromPileSnapshotPatch
	{
		internal static readonly HashSet<CardModel> _wasIntendedTopAtRemoval = new();

		public static void Prefix(CardModel __instance)
		{
			if (__instance != null && TopCardTracker.IsIntendedTop(__instance))
				_wasIntendedTopAtRemoval.Add(__instance);
		}
	}

	// CardCmd.Transform은 한 이터레이션 안에서 original.AfterTransformedFrom() 직후 바로
	// replacement.AfterTransformedTo()를 호출한다(다른 카드의 add/remove가 끼어들 수 없음).
	// 이 두 훅을 이용해 "지금 NEXT로 표시 중이던 카드"가 transform된 것인지를 카드 identity로
	// 정확히 판정하고, 그렇다면 물리적 인덱스와 상관없이 새 카드도 그대로 NEXT로 이어서 추적한다.
	[HarmonyPatch(typeof(CardModel), nameof(CardModel.AfterTransformedFrom))]
	public static class CardTransformFromTrackPatch
	{
		internal static CardModel _pendingTransformSource;

		public static void Prefix(CardModel __instance)
		{
			bool wasTop = __instance != null && CardRemoveFromPileSnapshotPatch._wasIntendedTopAtRemoval.Remove(__instance);
			_pendingTransformSource = wasTop ? __instance : null;
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.AfterTransformedTo))]
	public static class CardTransformToTrackPatch
	{
		public static void Prefix(CardModel __instance)
		{
			if (CardTransformFromTrackPatch._pendingTransformSource == null)
				return;
			TopCardTracker.MarkAsIntendedTop(__instance);
			CardTransformFromTrackPatch._pendingTransformSource = null;
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
