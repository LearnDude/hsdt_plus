using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BobsBuddy.Simulation;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Windows;

namespace Hearthstone_Deck_Tracker.BobsBuddy
{
	internal class PositioningResult
	{
		public int Turn { get; set; }
		public List<Entity> ActualOrder { get; set; } = new();
		public float ActualWinRate { get; set; }
		public List<Entity> OptimalOrder { get; set; } = new();
		public float OptimalWinRate { get; set; }
		public int ActualRank { get; set; }
		public int TotalPermutations { get; set; }
	}

	internal class PositioningSimulator
	{
		private const int PositioningIterations = 1000;
		private const int MaxTimePerPermutationMs = 500;

		private Guid _gameId;
		private int _turn;
		private Entity[]? _capturedEntities;

		public void CaptureState(IGame game, int turn)
		{
			if(game.CurrentGameStats == null)
				return;
			_gameId = game.CurrentGameStats.GameId;
			_turn = turn;
			_capturedEntities = BobsBuddyUtils.GetOrderedMinions(game.Player.Board)
				.Where(e => e.IsControlledBy(game.Player.Id))
				.ToArray();
			Log.Debug($"PositioningSimulator: captured {_capturedEntities.Length} minions at turn {turn}");
		}

		public async Task<PositioningResult?> RunAsync()
		{
			var entities = _capturedEntities;
			if(entities == null || entities.Length < 2)
			{
				Log.Debug("PositioningSimulator: fewer than 2 minions, skipping");
				return null;
			}

			var invoker = BobsBuddyInvoker.GetInstance(_gameId, _turn, createInstanceIfNoneFound: false);
			var capturedInput = invoker?.CapturedInput;
			if(capturedInput == null)
			{
				Log.Debug("PositioningSimulator: no captured input, skipping");
				return null;
			}

			var originalSide = capturedInput.Player.Side.ToList();
			if(originalSide.Count == 0)
			{
				Log.Debug("PositioningSimulator: player side is empty, skipping");
				return null;
			}

			var n = originalSide.Count;
			var results = new List<(int[] perm, float winRate)>();
			var indices = Enumerable.Range(0, n).ToArray();

			Log.Info($"PositioningSimulator: running {Factorial(n)} permutations for {n} minions at turn {_turn}");

			foreach(var perm in BobsBuddyUtils.Permutations(indices))
			{
				capturedInput.Player.Side.Clear();
				foreach(var i in perm)
					capturedInput.Player.Side.Add(originalSide[i]);

				try
				{
					var output = await new SimulationRunner().SimulateMultiThreaded(
						capturedInput, PositioningIterations, BobsBuddyInvoker.ThreadCount, MaxTimePerPermutationMs);
					results.Add(((int[])perm.Clone(), output?.WinRate ?? 0f));
				}
				catch(Exception e)
				{
					Log.Error($"PositioningSimulator permutation error: {e.Message}");
				}
			}

			capturedInput.Player.Side.Clear();
			capturedInput.Player.Side.AddRange(originalSide);

			if(results.Count == 0)
				return null;

			results.Sort((a, b) => b.winRate.CompareTo(a.winRate));

			var identity = Enumerable.Range(0, n).ToArray();
			var actualIdx = results.FindIndex(r => r.perm.SequenceEqual(identity));
			if(actualIdx < 0)
				actualIdx = results.Count - 1;

			var actualResult = results[actualIdx];
			var optimalResult = results[0];

			// Map permutation indices back to entity objects
			// entity[i] corresponds to originalSide[i] (same ZONE_POSITION order)
			// perm[j] = i means "position j gets the minion originally at index i"
			var optimalEntities = optimalResult.perm.Select(i => entities[Math.Min(i, entities.Length - 1)]).ToList();
			var actualEntities = entities.ToList();

			Log.Info($"PositioningSimulator: turn={_turn}, actualRank={actualIdx + 1}/{results.Count}, " +
			         $"actualWin={actualResult.winRate:P0}, optimalWin={optimalResult.winRate:P0}");
			Log.Info($"  Actual:  [{string.Join(", ", actualEntities.Select(e => e.Card?.Name ?? e.CardId))}]");
			Log.Info($"  Optimal: [{string.Join(", ", optimalEntities.Select(e => e.Card?.Name ?? e.CardId))}]");

			return new PositioningResult
			{
				Turn = _turn,
				ActualOrder = actualEntities,
				ActualWinRate = actualResult.winRate,
				OptimalOrder = optimalEntities,
				OptimalWinRate = optimalResult.winRate,
				ActualRank = actualIdx + 1,
				TotalPermutations = results.Count,
			};
		}

		public async void RunAndDisplayAsync()
		{
			try
			{
				var result = await RunAsync();
				if(result == null)
					return;
				Core.MainWindow.Dispatcher.Invoke(() => new PositioningResultsWindow(result).Show());
			}
			catch(Exception e)
			{
				Log.Error($"PositioningSimulator RunAndDisplayAsync: {e}");
			}
		}

		private static int Factorial(int n)
		{
			var f = 1;
			for(var i = 2; i <= n; i++)
				f *= i;
			return f;
		}
	}
}
