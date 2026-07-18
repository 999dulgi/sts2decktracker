using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace sts2decktracker
{
    // CardCmd.ApplyKeyword는 SculptingStrike, GhostSeed, PhantomBladesPower 등 "이 카드에 키워드를
    // 부여한다" 계열 효과들이 전부 거쳐가는 공용 창구. CardModel.AddKeyword로 곧장 LocalKeywords에
    // 꽂히기 때문에, 최종 상태만 보면 원래 카드에 있던 키워드와 구분이 안 된다 - 부여되는 이 시점을
    // 가로채서 별도로 기록해야 함.
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.ApplyKeyword))]
    public static class AppliedKeywordTrackerPatch
    {
        private static readonly ConditionalWeakTable<CardModel, HashSet<CardKeyword>> _appliedKeywords = new();

        public static void Postfix(CardModel card, CardKeyword[] keywords)
        {
            var set = _appliedKeywords.GetOrCreateValue(card);
            foreach (var keyword in keywords)
                set.Add(keyword);
        }

        public static bool WasAppliedByEffect(CardModel card, CardKeyword keyword)
        {
            return _appliedKeywords.TryGetValue(card, out var set) && set.Contains(keyword);
        }

        internal static void CarryForward(CardModel oldCard, CardModel newCard)
        {
            if (_appliedKeywords.TryGetValue(oldCard, out var set) && set.Count > 0)
            {
                _appliedKeywords.Remove(newCard);
                _appliedKeywords.Add(newCard, new HashSet<CardKeyword>(set));
            }
        }
    }

    // CardModel은 상태가 바뀔 때마다 MemberwiseClone으로 통째로 새 인스턴스가 되는데,
    // ConditionalWeakTable은 참조 기준이라 clone된 새 인스턴스는 추적 정보가 비어있는 채로 시작한다.
    // MutableClone은 (원본, 새 clone) 둘 다 손에 쥘 수 있는 유일한 지점이라 여기서 이어붙인다.
    [HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
    public static class AppliedKeywordCloneCarryPatch
    {
        public static void Postfix(AbstractModel __instance, AbstractModel __result)
        {
            if (__instance is CardModel oldCard && __result is CardModel newCard)
                AppliedKeywordTrackerPatch.CarryForward(oldCard, newCard);
        }
    }
}
