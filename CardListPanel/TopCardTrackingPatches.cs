using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using System.Collections.Generic;
using System.Linq;

namespace sts2decktracker
{
	// CardPileCmd.Add 호출 시점에 이미 실제 position과 대상 cards를 알 수 있으므로, index로
	// CardPile.AddInternal을 역추적할 필요 없이 여기서 바로 "명시적으로 CardPilePosition.Top을
	// 지정한 경우"만 마킹한다. CardPileCmd.Add를 거치지 않는 직접 호출(Transform 등)은 여기서
	// 다루지 않고 아래의 identity 기반 훅에서 별도로 처리한다.
	//
	// CardPileCmd.Add(IEnumerable<CardModel>, CardPile, CardPilePosition, ...)의 trailing 파라미터
	// 개수(예: isChangingOwners)가 게임 버전(베타/정식)마다 다를 수 있어, 타입을 전부 못박은
	// [HarmonyPatch(..., typeof(bool), typeof(bool))] 방식은 한쪽 버전에서 대상 메서드를 못 찾아
	// PatchAll 전체를 실패시킨다. 앞 3개 파라미터 타입만으로 오버로드를 찾아 양쪽 버전 모두 대응한다.
	[HarmonyPatch]
	public static class CardPileTopTrackPatch
	{
		static System.Reflection.MethodInfo TargetMethod()
		{
			return typeof(CardPileCmd)
				.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
				.FirstOrDefault(m =>
				{
					if (m.Name != nameof(CardPileCmd.Add)) return false;
					var p = m.GetParameters();
					return p.Length >= 3
						&& p[0].ParameterType == typeof(IEnumerable<CardModel>)
						&& p[1].ParameterType == typeof(CardPile)
						&& p[2].ParameterType == typeof(CardPilePosition);
				});
		}

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
}
