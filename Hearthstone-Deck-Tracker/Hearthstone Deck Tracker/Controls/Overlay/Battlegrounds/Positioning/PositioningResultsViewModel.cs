using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Positioning
{
	internal class PositioningResultsViewModel
	{
		public PositioningResultsViewModel(PositioningResult result)
		{
			Turn = result.Turn;
			OptimalOrdering = result.OptimalOrder.Select(e => ToViewModel(e)).ToList();
			ActualOrdering = result.ActualOrder
				.Select((entity, i) => ToViewModel(entity, result.HarmfulMinionIndices.Contains(i)))
				.ToList();
			OptimalWinRate = FormatWinRate(result.OptimalWinRate);
			ActualWinRate = FormatWinRate(result.ActualWinRate);
			ActualRankText = $"ranked #{result.ActualRank} of {result.TotalPermutations}";
			var diff = result.OptimalWinRate - result.ActualWinRate;
			WinRateDiff = diff > 0.001f ? $"−{FormatWinRate(diff)}" : string.Empty;
			IsOptimal = result.ActualRank == 1;
		}

		public int Turn { get; }
		public List<BattlegroundsMinionViewModel> OptimalOrdering { get; }
		public List<BattlegroundsMinionViewModel> ActualOrdering { get; }
		public string OptimalWinRate { get; }
		public string ActualWinRate { get; }
		public string ActualRankText { get; }
		public string WinRateDiff { get; }
		public bool IsOptimal { get; }
		public string ActualWinRateLine
		{
			get
			{
				var s = $"Win {ActualWinRate}  ({ActualRankText}";
				if(!string.IsNullOrEmpty(WinRateDiff))
					s += $", {WinRateDiff}";
				return s + ")";
			}
		}

		private static string FormatWinRate(float rate) => $"{(int)(rate * 100)}%";

		private static BattlegroundsMinionViewModel ToViewModel(Entity entity, bool isHarmful = false) =>
			new BattlegroundsMinionViewModel
			{
				HasPoisonous = entity.HasTag(GameTag.POISONOUS),
				HasVenomous = entity.HasTag(GameTag.VENOMOUS),
				HasDivineShield = entity.HasTag(GameTag.DIVINE_SHIELD),
				HasDeathrattle = entity.HasTag(GameTag.DEATHRATTLE),
				HasReborn = entity.HasTag(GameTag.REBORN),
				IsPremium = entity.HasTag(GameTag.PREMIUM),
				HasTaunt = entity.HasTag(GameTag.TAUNT),
				Attack = entity.Attack,
				Health = entity.Health,
				Card = entity.Card,
				IsHarmful = isHarmful,
			};
	}
}
