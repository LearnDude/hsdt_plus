using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BobsBuddy.Simulation;
using BobsBuddyPlayer = BobsBuddy.Simulation.Player;
using Minion = BobsBuddy.Minion;
using Hearthstone_Deck_Tracker.Utility.Logging;
using HearthDb;
using Entity = Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity;
using Newtonsoft.Json;

namespace Hearthstone_Deck_Tracker.BobsBuddy
{
	internal static class PositioningLogger
	{
		private static readonly string LogDir = Path.Combine(
			Hearthstone_Deck_Tracker.Config.Instance.DataDir, "PositioningLogs");

		public static async Task WriteAsync(
			Guid gameId,
			int turn,
			bool isDuos,
			Input capturedInput,
			Entity[] playerEntities,
			List<IEnumerable<Entity>> attachedEntitiesPerMinion,
			List<Minion> originalSide,
			List<(int[] perm, float winRate)> screenResults,
			List<(int[] perm, float winRate)> confirmResults,
			PositioningResult result)
		{
			try
			{
				Directory.CreateDirectory(LogDir);

				var log = new PositioningCombatLog
				{
					GameId    = gameId.ToString(),
					Turn      = turn,
					Timestamp = DateTime.UtcNow.ToString("o"),
					IsDuos    = isDuos,

					Player   = BuildPlayerContext(capturedInput.Player),
					Opponent = BuildPlayerContext(capturedInput.Opponent),

					PlayerMinions   = originalSide
						.Select((m, i) => SnapshotPlayer(m, playerEntities[i], attachedEntitiesPerMinion[i]))
						.ToList(),
					OpponentMinions = capturedInput.Opponent.Side
						.Select(SnapshotOpponent)
						.ToList(),
					PlayerTeammateMinions   = capturedInput.PlayerTeammate?.Side.Select(SnapshotOpponent).ToList(),
					OpponentTeammateMinions = capturedInput.OpponentTeammate?.Side.Select(SnapshotOpponent).ToList(),

					ScreenPhaseResults  = screenResults.Select(ToEntry).ToList(),
					ConfirmPhaseResults = confirmResults.Select(ToEntry).ToList(),

					ActualRank     = result.ActualRank,
					TotalPerms     = result.TotalPermutations,
					ActualWinRate  = result.ActualWinRate,
					OptimalWinRate = result.OptimalWinRate,
				};

				var path = Path.Combine(LogDir, $"{gameId}_t{turn}.json");
				var json = JsonConvert.SerializeObject(log, Formatting.Indented);
				await Task.Run(() => File.WriteAllText(path, json));
				Log.Info($"PositioningLogger: wrote {path}");
			}
			catch(Exception e)
			{
				Log.Error($"PositioningLogger.WriteAsync: {e.Message}");
			}
		}

		public static async Task RecordOutcomeAsync(Guid gameId, int turn, string outcome, int? damageReceived)
		{
			try
			{
				Directory.CreateDirectory(LogDir);
				var record = new { Outcome = outcome, DamageReceived = damageReceived };
				var path = Path.Combine(LogDir, $"{gameId}_t{turn}_outcome.json");
				var json = JsonConvert.SerializeObject(record, Formatting.Indented);
				await Task.Run(() => File.WriteAllText(path, json));
				Log.Info($"PositioningLogger: outcome={outcome} damage={damageReceived} → {path}");
			}
			catch(Exception e)
			{
				Log.Error($"PositioningLogger.RecordOutcomeAsync: {e.Message}");
			}
		}

		private static PermutationEntry ToEntry((int[] perm, float winRate) r)
			=> new() { Ordering = r.perm.ToList(), WinRate = r.winRate };

		private static PlayerContext BuildPlayerContext(BobsBuddyPlayer p) => new()
		{
			Health     = p.Health,
			TavernTier = p.Tier,
			HeroPowers = p.HeroPowers.Select(hp => new HeroPowerSnapshot
			{
				CardId      = hp.CardId,
				IsActivated = hp.IsActivated,
			}).ToList(),
			QuestCardIds     = p.Quests.Select(q => q.QuestCardId).ToList(),
			TrinketCardIds   = p.Trinkets.Select(t => t.CardID).ToList(),
			ObjectiveCardIds = p.Objectives.Select(o => o.CardID).ToList(),
			EternalKnightCounter          = p.EternalKnightCounter,
			UndeadAttackBonus             = p.UndeadAttackBonus,
			BeastAttackBonus              = p.BeastAttackBonus,
			BeastHealthBonus              = p.BeastHealthBonus,
			WhelpAttackBonus              = p.WhelpAttackBonus,
			WhelpHealthBonus              = p.WhelpHealthBonus,
			BeetlesAtkBuff                = p.BeetlesAtkBuff,
			BeetlesHealthBuff             = p.BeetlesHealthBuff,
			AncestralAutomatonCounter     = p.AncestralAutomatonCounter,
			ElementalPlayCounter          = p.ElementalPlayCounter,
			PiratesSummonCounter          = p.PiratesSummonCounter,
			BeastsSummonCounter           = p.BeastsSummonCounter,
			FriendlyMinionsDeadLastCombat = p.FriendlyMinionsDeadLastCombatCounter,
			BattlecryCounter              = p.BattlecryCounter,
			ResourcesSpentThisGame        = p.ResourcesSpentThisGame,
		};

		private static MinionSnapshot SnapshotPlayer(Minion m, Entity e, IEnumerable<Entity> attachedEntities) => new()
		{
			CardId        = m.CardID,
			Name          = e.Card?.Name ?? m.CardID,
			Attack        = m.baseAttack,
			Health        = m.baseHealth,
			VanillaAttack = m.vanillaAttack,
			VanillaHealth = m.vanillaHealth,
			Race          = m.PrimaryRace.ToString(),
			TavernTier    = m.tier,
			IsGolden      = m.golden,
			Taunt         = m.taunt,
			DivineShield  = m.div > 0,
			Poisonous     = m.poisonous,
			Venomous      = m.venomous,
			Reborn        = m.reborn,
			Windfury      = m.windfury,
			MegaWindfury  = m.megaWindfury,
			Cleave        = m.cleave,
			Stealth       = m.stealth,
			HasWingmen    = m.HasWingmen,
			ScriptDataNum1 = m.ScriptDataNum1,
			ScriptDataNum2 = m.ScriptDataNum2,
			ScriptDataNum3 = m.ScriptDataNum3,
			ScriptDataNum4 = m.ScriptDataNum4,
			EnchantmentCardIds = attachedEntities
				.Where(ae => ae.CardId != null)
				.Select(ae => ae.CardId!)
				.ToList(),
			AdditionalDeathrattleCount = m.AdditionalDeathrattles.Count,
			AdditionalRallyCount       = m.AdditionalRallies.Count,
		};

		private static MinionSnapshot SnapshotOpponent(Minion m) => new()
		{
			CardId        = m.CardID,
			Name          = Cards.All.TryGetValue(m.CardID, out var card) ? card.Name : m.CardID,
			Attack        = m.baseAttack,
			Health        = m.baseHealth,
			VanillaAttack = m.vanillaAttack,
			VanillaHealth = m.vanillaHealth,
			Race          = m.PrimaryRace.ToString(),
			TavernTier    = m.tier,
			IsGolden      = m.golden,
			Taunt         = m.taunt,
			DivineShield  = m.div > 0,
			Poisonous     = m.poisonous,
			Venomous      = m.venomous,
			Reborn        = m.reborn,
			Windfury      = m.windfury,
			MegaWindfury  = m.megaWindfury,
			Cleave        = m.cleave,
			Stealth       = m.stealth,
			HasWingmen    = m.HasWingmen,
			ScriptDataNum1 = m.ScriptDataNum1,
			ScriptDataNum2 = m.ScriptDataNum2,
			ScriptDataNum3 = m.ScriptDataNum3,
			ScriptDataNum4 = m.ScriptDataNum4,
			EnchantmentCardIds         = null,
			AdditionalDeathrattleCount = m.AdditionalDeathrattles.Count,
			AdditionalRallyCount       = m.AdditionalRallies.Count,
		};

		// --- DTOs ---

		public class PositioningCombatLog
		{
			public string GameId    { get; set; } = "";
			public int    Turn      { get; set; }
			public string Timestamp { get; set; } = "";
			public bool   IsDuos    { get; set; }

			public PlayerContext         Player                  { get; set; } = new();
			public PlayerContext         Opponent                { get; set; } = new();
			public List<MinionSnapshot>  PlayerMinions           { get; set; } = new();
			public List<MinionSnapshot>  OpponentMinions         { get; set; } = new();
			public List<MinionSnapshot>? PlayerTeammateMinions   { get; set; }
			public List<MinionSnapshot>? OpponentTeammateMinions { get; set; }

			public List<PermutationEntry> ScreenPhaseResults  { get; set; } = new();
			public List<PermutationEntry> ConfirmPhaseResults { get; set; } = new();

			public int   ActualRank     { get; set; }
			public int   TotalPerms     { get; set; }
			public float ActualWinRate  { get; set; }
			public float OptimalWinRate { get; set; }
		}

		public class PlayerContext
		{
			public int  Health      { get; set; }
			public int  TavernTier  { get; set; }

			public List<HeroPowerSnapshot> HeroPowers       { get; set; } = new();
			public List<string>            QuestCardIds     { get; set; } = new();
			public List<string>            TrinketCardIds   { get; set; } = new();
			public List<string>            ObjectiveCardIds { get; set; } = new();

			public int EternalKnightCounter          { get; set; }
			public int UndeadAttackBonus             { get; set; }
			public int BeastAttackBonus              { get; set; }
			public int BeastHealthBonus              { get; set; }
			public int WhelpAttackBonus              { get; set; }
			public int WhelpHealthBonus              { get; set; }
			public int BeetlesAtkBuff                { get; set; }
			public int BeetlesHealthBuff             { get; set; }
			public int AncestralAutomatonCounter     { get; set; }
			public int ElementalPlayCounter          { get; set; }
			public int PiratesSummonCounter          { get; set; }
			public int BeastsSummonCounter           { get; set; }
			public int FriendlyMinionsDeadLastCombat { get; set; }
			public int BattlecryCounter              { get; set; }
			public int ResourcesSpentThisGame        { get; set; }
		}

		public class HeroPowerSnapshot
		{
			public string CardId      { get; set; } = "";
			public bool   IsActivated { get; set; }
		}

		public class MinionSnapshot
		{
			public string CardId { get; set; } = "";
			public string Name   { get; set; } = "";

			public int Attack { get; set; }
			public int Health { get; set; }

			public int VanillaAttack { get; set; }
			public int VanillaHealth { get; set; }

			public string Race       { get; set; } = "";
			public int    TavernTier { get; set; }
			public bool   IsGolden   { get; set; }

			public bool Taunt        { get; set; }
			public bool DivineShield { get; set; }
			public bool Poisonous    { get; set; }
			public bool Venomous     { get; set; }
			public bool Reborn       { get; set; }
			public bool Windfury     { get; set; }
			public bool MegaWindfury { get; set; }
			public bool Cleave       { get; set; }
			public bool Stealth      { get; set; }
			public bool HasWingmen   { get; set; }

			public int ScriptDataNum1 { get; set; }
			public int ScriptDataNum2 { get; set; }
			public int ScriptDataNum3 { get; set; }
			public int ScriptDataNum4 { get; set; }

			public List<string>? EnchantmentCardIds         { get; set; }
			public int           AdditionalDeathrattleCount { get; set; }
			public int           AdditionalRallyCount       { get; set; }
		}

		public class PermutationEntry
		{
			public List<int> Ordering { get; set; } = new();
			public float     WinRate  { get; set; }
		}
	}
}
