using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace sts2decktracker
{
	public static class TopCardTracker
	{
		private static readonly HashSet<CardModel> _intendedTopCards = new();

		public static void MarkAsIntendedTop(CardModel card) => _intendedTopCards.Add(card);

		public static bool IsIntendedTop(CardModel card) => _intendedTopCards.Contains(card);

		public static void PruneCards(CardPile drawPile)
		{
			_intendedTopCards.RemoveWhere(c => !drawPile.Cards.Contains(c));
		}

		public static void Clear() => _intendedTopCards.Clear();
	}
}
