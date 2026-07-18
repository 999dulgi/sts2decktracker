using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace sts2decktracker
{
    // CardModel의 상태 변화(강화/코스트/인챈트/디버프/키워드/단발 Retain·Sly)를 전역으로 감지해서
    // 구독자(CardListPanel 등)에게 알리는 브릿지. 아래 Harmony 패치들이 상태를 바꾸는 CardModel
    // 메서드에 걸려서 이 이벤트를 발동시킨다.
    public static class CardStateBridge
    {
        public static event Action<CardModel> CardStateChanged;

        internal static void Raise(CardModel card) => CardStateChanged?.Invoke(card);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.GiveSingleTurnRetain))]
    internal static class GiveSingleTurnRetainPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.GiveSingleTurnSly))]
    internal static class GiveSingleTurnSlyPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    // 카드가 실제로 지울 상태(단발 Retain/Sly, 임시 스타 코스트, 로컬 코스트 수정자)를 갖고 있었을
    // 때만 브로드캐스트한다. 훅을 순회하지 않는 값만 확인해서 저렴하게 판단한다.
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.EndOfTurnCleanup))]
    internal static class EndOfTurnCleanupPatch
    {
        private static readonly System.Reflection.FieldInfo HasSingleTurnRetainField =
            AccessTools.Field(typeof(CardModel), "_hasSingleTurnRetain");
        private static readonly System.Reflection.FieldInfo HasSingleTurnSlyField =
            AccessTools.Field(typeof(CardModel), "_hasSingleTurnSly");

        public static void Prefix(CardModel __instance, out bool __state)
        {
            bool hadSingleTurnFlag = (bool)HasSingleTurnRetainField.GetValue(__instance)
                || (bool)HasSingleTurnSlyField.GetValue(__instance);
            bool hadTemporaryStarCost = __instance.TemporaryStarCost != null;
            bool hadEnergyModifiers = __instance.EnergyCost != null && __instance.EnergyCost.HasLocalModifiers;
            __state = hadSingleTurnFlag || hadTemporaryStarCost || hadEnergyModifiers;
        }

        public static void Postfix(CardModel __instance, bool __state)
        {
            if (__state)
                CardStateBridge.Raise(__instance);
        }
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.InvokeEnergyCostChanged))]
    internal static class InvokeEnergyCostChangedPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), "BaseStarCost", MethodType.Setter)]
    internal static class BaseStarCostSetterPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), "AddTemporaryStarCost")]
    internal static class AddTemporaryStarCostPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.AddKeyword))]
    internal static class AddKeywordPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.RemoveKeyword))]
    internal static class RemoveKeywordPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.EnchantInternal))]
    internal static class EnchantInternalPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ClearEnchantmentInternal))]
    internal static class ClearEnchantmentInternalPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.AfflictInternal))]
    internal static class AfflictInternalPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ClearAfflictionInternal))]
    internal static class ClearAfflictionInternalPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
    internal static class UpgradeInternalPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.DowngradeInternal))]
    internal static class DowngradeInternalPatch
    {
        public static void Postfix(CardModel __instance) => CardStateBridge.Raise(__instance);
    }
}
