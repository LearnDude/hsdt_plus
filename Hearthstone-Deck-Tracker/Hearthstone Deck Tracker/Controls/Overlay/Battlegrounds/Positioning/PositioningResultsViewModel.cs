using System;
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

			var total = result.TotalPermutations;
			var pctAbove = FormatExtremePercent(result.PermutationsAbove98, total);
			var pctBelow = FormatExtremePercent(result.PermutationsBelow2,  total);
			HasExtremeOutcomes = result.PermutationsAbove98 > 0 || result.PermutationsBelow2 > 0;
			ExtremeOutcomesLine = HasExtremeOutcomes
				? BuildExtremeOutcomesLine(result.PermutationsAbove98, pctAbove, result.PermutationsBelow2, pctBelow)
				: string.Empty;
		}

		public int Turn { get; }
		public List<BattlegroundsMinionViewModel> OptimalOrdering { get; }
		public List<BattlegroundsMinionViewModel> ActualOrdering { get; }
		public string OptimalWinRate { get; }
		public string ActualWinRate { get; }
		public string ActualRankText { get; }
		public string WinRateDiff { get; }
		public bool IsOptimal { get; }
		public bool HasExtremeOutcomes { get; }
		public string ExtremeOutcomesLine { get; }
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

		private static string FormatExtremePercent(int count, int total)
		{
			if(count == 0 || total == 0)
				return "0%";
			var pct = (int)Math.Round(count * 100.0 / total);
			return pct == 0 ? "<1%" : $"{pct}%";
		}

		private static string BuildExtremeOutcomesLine(int above, string pctAbove, int below, string pctBelow)
		{
			if(above > 0 && below > 0)
				return $"≥98% win: {pctAbove} of orderings  ·  ≤2% win: {pctBelow} of orderings";
			if(above > 0)
				return $"≥98% win: {pctAbove} of orderings";
			return $"≤2% win: {pctBelow} of orderings";
		}

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
