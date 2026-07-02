using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace sts2decktracker
{
    public static class CardColorHelper
    {
        public static (Color titleColor, Color titleOutlineColor) GetTitleColors(CardModel card, CardColorMode colorMode)
        {
            if (colorMode == CardColorMode.None)
                return (StsColors.cream, StsColors.cardTitleOutlineCommon);

            if (card.Enchantment != null)
                return (new Color(0.85f, 0.6f, 1f, 1f), new Color(0.3f, 0.05f, 0.45f, 1f));

            if (card.CurrentUpgradeLevel > 0)
                return (StsColors.green, StsColors.cardTitleOutlineSpecial);

            if (colorMode == CardColorMode.Full)
                return (StsColors.cream, GetTitleOutlineColorByRarity(card.Rarity));

            return (StsColors.cream, StsColors.cardTitleOutlineCommon);
        }

        public static Color GetTitleOutlineColorByRarity(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.None => StsColors.cardTitleOutlineCommon,
                CardRarity.Basic => StsColors.cardTitleOutlineCommon,
                CardRarity.Common => StsColors.cardTitleOutlineCommon,
                CardRarity.Token => StsColors.cardTitleOutlineCommon,
                CardRarity.Uncommon => StsColors.cardTitleOutlineUncommon,
                CardRarity.Rare => StsColors.cardTitleOutlineRare,
                CardRarity.Curse => StsColors.cardTitleOutlineCurse,
                CardRarity.Quest => StsColors.cardTitleOutlineQuest,
                CardRarity.Status => StsColors.cardTitleOutlineStatus,
                CardRarity.Event => StsColors.cardTitleOutlineSpecial,
                CardRarity.Ancient => StsColors.cardTitleOutlineCommon,
                _ => StsColors.cardTitleOutlineCommon
            };
        }
    }
}
