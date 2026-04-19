using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BobsBuddy.Simulation;
using BobsBuddyPlayer = BobsBuddy.Simulation.Player;
using Minion = BobsBuddy.Minion;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Windows;
using Entity = Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity;

namespace Hearthstone_Deck_Tracker.BobsBuddy
{
	public class PositioningResult
	{
		public int Turn { get; set; }
		public List<Entity> ActualOrder { get; set; } = new();
		public float ActualWinRate { get; set; }
		public List<Entity> OptimalOrder { get; set; } = new();
		public float OptimalWinRate { get; set; }
		public int ActualRank { get; set; }
		public int TotalPermutations { get; set; }
		public HashSet<int> HarmfulMinionIndices { get; set; } = new();
	}

	internal class PositioningSimulator
	{
		private const int ScreenIterations = 50;
		private const int ConfirmIterations = 500;
		private const int TopKCandidates = 15;
		private const int MaxTimePerPermutationMs = 100;
		private const float RemovalBenefitThreshold = 0.03f;
		private const int RemovalIterations = 1000;

		// Each parallel task uses 1 thread; we run ProcessorCount tasks concurrently.
		// This saturates all cores without over-subscribing the ThreadPool.
		private static readonly int _parallelDegree = Environment.ProcessorCount;
		private static readonly MethodInfo _memberwiseClone =
			typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;

		private static readonly string _shownFile = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"HearthstoneDeckTracker", "positioning_shown.txt");

		// Persisted across HDT restarts so replayed turns are not re-shown.
		private static readonly HashSet<(Guid, int)> _displayed = LoadDisplayed();

		private static HashSet<(Guid, int)> LoadDisplayed()
		{
			var set = new HashSet<(Guid, int)>();
			try
			{
				if(!File.Exists(_shownFile))
					return set;
				foreach(var line in File.ReadAllLines(_shownFile))
				{
					var parts = line.Split(':');
					if(parts.Length == 2 && Guid.TryParse(parts[0], out var g) && int.TryParse(parts[1], out var t))
						set.Add((g, t));
				}
			}
			catch(Exception e)
			{
				Log.Warn($"PositioningSimulator: could not load shown file: {e.Message}");
			}
			return set;
		}

		private static void PersistDisplayed(Guid gameId, int turn)
		{
			try
			{
				File.AppendAllText(_shownFile, $"{gameId}:{turn}{Environment.NewLine}");
			}
			catch(Exception e)
			{
				Log.Warn($"PositioningSimulator: could not save shown file: {e.Message}");
			}
		}

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
			Log.Info($"PositioningSimulator: player={n} minions, opponent={capturedInput.Opponent.Side.Count} minions");

			var indices = Enumerable.Range(0, n).ToArray();
			var allPerms = BobsBuddyUtils.Permutations(indices).ToList();

			// Pre-warm the ThreadPool so it doesn't throttle us with its hill-climbing delay
			// (default: injects ~1 new thread per 500ms, which is too slow for short simulations)
			ThreadPool.GetMinThreads(out var minWorker, out var minIo);
			ThreadPool.SetMinThreads(minWorker + _parallelDegree, minIo);

			List<(int[] perm, float winRate)> screenResults;
			List<(int[] perm, float winRate)> confirmResults;
			List<(int[] perm, float winRate)> screenForLog = new();
			List<(int[] perm, float winRate)> confirmForLog = new();
			(int[] perm, float winRate) actualResult = (Array.Empty<int>(), 0f);
			(int[] perm, float winRate) optimalResult = (Array.Empty<int>(), 0f);
			var harmfulIndices = new HashSet<int>();
			int actualIdx = 0;
			int totalPermutations = 0;

			using(var semaphore = new SemaphoreSlim(_parallelDegree))
			{
				try
				{
					// --- Phase 1: screen all permutations at low iteration count, in parallel ---
					Log.Info($"PositioningSimulator: screening {allPerms.Count} permutations at {ScreenIterations} iters, degree={_parallelDegree}");

					var screenTasks = allPerms.Select(perm => RunPermutationAsync(
						capturedInput, originalSide, perm, ScreenIterations, semaphore, "screen")).ToList();
					screenResults = (await Task.WhenAll(screenTasks)).ToList();
					screenForLog = screenResults.Select(r => ((int[])r.perm.Clone(), r.winRate)).ToList();

					// --- Phase 2: confirm top-K candidates at full iteration count ---
					screenResults.Sort((a, b) => b.winRate.CompareTo(a.winRate));

					var identityPerm = Enumerable.Range(0, n).ToArray();
					var topK = screenResults.Take(TopKCandidates).ToList();
					if(!topK.Any(r => r.perm.SequenceEqual(identityPerm)))
						topK.Add(screenResults.First(r => r.perm.SequenceEqual(identityPerm)));

					Log.Info($"PositioningSimulator: confirming top {topK.Count} candidates at {ConfirmIterations} iters");

					var confirmTasks = topK.Select(candidate => RunPermutationAsync(
						capturedInput, originalSide, candidate.perm, ConfirmIterations, semaphore, "confirm")).ToList();
					confirmResults = (await Task.WhenAll(confirmTasks)).ToList();
					confirmForLog = confirmResults.Select(r => ((int[])r.perm.Clone(), r.winRate)).ToList();

					// Merge: confirmed win rates take priority over screen win rates
					var confirmedPerms = new HashSet<string>(confirmResults.Select(r => string.Join(",", r.perm)));
					var allResults = confirmResults
						.Concat(screenResults.Where(r => !confirmedPerms.Contains(string.Join(",", r.perm))))
						.ToList();
					allResults.Sort((a, b) => b.winRate.CompareTo(a.winRate));

					if(allResults.Count == 0)
						return null;

					var identity = Enumerable.Range(0, n).ToArray();
					actualIdx = allResults.FindIndex(r => r.perm.SequenceEqual(identity));
					if(actualIdx < 0)
						actualIdx = allResults.Count - 1;

					actualResult = allResults[actualIdx];
					optimalResult = allResults[0];
					totalPermutations = allResults.Count;

					// --- Removal phase: flag minions whose removal improves win rate ---
					if(n >= 2)
					{
						Log.Info($"PositioningSimulator: removal phase, {n} simulations at {RemovalIterations} iters");
						var removalTasks = Enumerable.Range(0, n).Select(removeIdx =>
						{
							var reducedSide = originalSide.Where((_, i) => i != removeIdx).ToList();
							var reducedIdentity = Enumerable.Range(0, reducedSide.Count).ToArray();
							return (removeIdx, task: RunPermutationAsync(
								capturedInput, reducedSide, reducedIdentity, RemovalIterations, semaphore, "removal"));
						}).ToList();

						await Task.WhenAll(removalTasks.Select(x => x.task));

						foreach(var (removeIdx, task) in removalTasks)
						{
							var (_, winRate) = task.Result;
							if(winRate > actualResult.winRate + RemovalBenefitThreshold)
								harmfulIndices.Add(removeIdx);
						}
					}
				}
				finally
				{
					ThreadPool.SetMinThreads(minWorker, minIo);
				}
			}

			// Restore original side (defensive; individual inputs are independent but just in case)
			capturedInput.Player.Side.Clear();
			capturedInput.Player.Side.AddRange(originalSide);

			// entity[i] corresponds to originalSide[i] (same ZONE_POSITION order)
			// perm[j] = i means "position j gets the minion originally at index i"
			var optimalEntities = optimalResult.perm.Select(i => entities[Math.Min(i, entities.Length - 1)]).ToList();
			var actualEntities = entities.ToList();

			Log.Info($"PositioningSimulator: turn={_turn}, actualRank={actualIdx + 1}/{totalPermutations}, " +
			         $"actualWin={actualResult.winRate:P0}, optimalWin={optimalResult.winRate:P0}");
			Log.Info($"  Actual:  [{string.Join(", ", actualEntities.Select(e => e.Card?.Name ?? e.CardId))}]");
			Log.Info($"  Optimal: [{string.Join(", ", optimalEntities.Select(e => e.Card?.Name ?? e.CardId))}]");

			var result = new PositioningResult
			{
				Turn = _turn,
				ActualOrder = actualEntities,
				ActualWinRate = actualResult.winRate,
				OptimalOrder = optimalEntities,
				OptimalWinRate = optimalResult.winRate,
				ActualRank = actualIdx + 1,
				TotalPermutations = totalPermutations,
				HarmfulMinionIndices = harmfulIndices,
			};

			var logInvoker = BobsBuddyInvoker.GetInstance(_gameId, _turn, createInstanceIfNoneFound: false);
			if(logInvoker != null)
			{
				var attachedEntitiesPerMinion = entities
					.Select(e => logInvoker.GetAttachedEntities(e.Id))
					.ToList();
				logInvoker.PositioningLogWritten = true;
				_ = PositioningLogger.WriteAsync(
					_gameId, _turn,
					isDuos: capturedInput.PlayerTeammate != null,
					capturedInput,
					entities,
					attachedEntitiesPerMinion,
					originalSide,
					screenForLog,
					confirmForLog,
					result);
			}

			return result;
		}

		private static async Task<(int[] perm, float winRate)> RunPermutationAsync(
			Input template, List<Minion> originalSide, int[] perm,
			int iterations, SemaphoreSlim semaphore, string phase)
		{
			await semaphore.WaitAsync();
			try
			{
				var permutedSide = perm.Select(i => originalSide[i]).ToList();
				var input = CreateInputForPermutation(template, permutedSide);
				var output = await Task.Factory.StartNew(
					() => new SimulationRunner().SimulateMultiThreaded(input, iterations, 1, MaxTimePerPermutationMs).GetAwaiter().GetResult(),
					TaskCreationOptions.LongRunning);
				if(output == null)
					Log.Warn($"PositioningSimulator: {phase} output was null (timeout?)");
				return ((int[])perm.Clone(), output?.winRate ?? 0f);
			}
			catch(Exception e)
			{
				Log.Error($"PositioningSimulator {phase} error: {e.Message}");
				return ((int[])perm.Clone(), 0f);
			}
			finally
			{
				semaphore.Release();
			}
		}

		// Shallow-clones Player (copies all value-type counters) then replaces Side.
		// SimulateMultiThreaded clones minions internally before each battle iteration,
		// so sharing the same Minion objects across parallel simulations is safe.
		private static Input CreateInputForPermutation(Input template, List<Minion> permutedSide)
		{
			var playerClone = (BobsBuddyPlayer)_memberwiseClone.Invoke(template.Player, null);
			playerClone.Side = permutedSide;
			return new Input
			{
				Player = playerClone,
				Opponent = template.Opponent,
				PlayerTeammate = template.PlayerTeammate,
				OpponentTeammate = template.OpponentTeammate,
			};
		}

		public async void RunAndDisplayAsync()
		{
			var key = (_gameId, _turn);
			if(!_displayed.Add(key))
			{
				Log.Debug($"PositioningSimulator: already displayed turn={_turn}, skipping");
				return;
			}
			PersistDisplayed(_gameId, _turn);
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
