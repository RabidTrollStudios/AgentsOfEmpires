using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using AgentTestHarness;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Stress tests for cross-engine parity using real IPlanningAgent instances.
	/// Unlike CrossEngineParityTests (which issues commands via CommandProcessor
	/// directly), these tests wire the same agent into both Unity's AgentBridge
	/// and SimGame, letting agents make decisions through IGameState/IAgentActions.
	///
	/// This exercises the full pipeline: pathfinding budget, cooldown system,
	/// buildability filtering, command validation, and deferred dispatch.
	/// </summary>
	public class CrossEngineStressTests : PlayModeTestBase
	{
		private const int MAP_W = 30;
		private const int MAP_H = 30;
		private const int STARTING_GOLD = 5000;
		private const int MINE_GOLD = 10000;

		private float _actualStepDuration;

		private void SetupEngineSync()
		{
			// Register agents with GameManager
			var gm = GameManager.Instance;
			if (gm.Agents == null)
			{
				gm.Agents = new Dictionary<int, GameObject>
				{
					[0] = ctx.Agent0Go,
					[1] = ctx.Agent1Go
				};
			}

			// Sync step duration between engines
			Time.fixedDeltaTime = 0.02f;
			_actualStepDuration = Time.fixedDeltaTime;
		}

		/// <summary>
		/// Wire an IPlanningAgent into a Unity AgentBridge and initialize its adapters.
		/// Must be called AFTER SetupEngineSync() so GameManager.Agents is populated.
		/// </summary>
		private void WireAgent(GameObject agentGo, int agentNbr, IPlanningAgent planningAgent)
		{
			SetupEngineSync(); // Ensure Agents dict exists

			var bridge = agentGo.GetComponent<AgentBridge>();
			bridge.SetPlanningAgent(planningAgent);
			bridge.InitializeAdapters(agentNbr,
				ctx.UnitManager,
				ctx.MapManager,
				(EventDispatcher)typeof(GameManager)
					.GetField("eventDispatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
					.GetValue(GameManager.Instance));
			bridge.UpdateEnemyAgentNbr();
		}

		private SimGame BuildMatchingSimGame(
			IPlanningAgent agent0, IPlanningAgent agent1,
			params (int owner, UnitType type, int x, int y)[] units)
		{
			SetupEngineSync();

			var config = new SimConfig
			{
				MapWidth = MAP_W,
				MapHeight = MAP_H,
				TickDuration = _actualStepDuration
			};
			var builder = new SimGameBuilder()
				.WithConfig(config)
				.WithGold(0, STARTING_GOLD)
				.WithGold(1, STARTING_GOLD);

			foreach (var (owner, type, x, y) in units)
			{
				if (type == UnitType.MINE)
					builder.WithMine(new Position(x, y), MINE_GOLD);
				else
					builder.WithUnit(owner, type, new Position(x, y));
			}

			builder.WithAgent(0, agent0)
			       .WithAgent(1, agent1);

			var sim = builder.Build();
			sim.InitializeMatch();
			sim.InitializeRound();
			return sim;
		}

		#region Stress Scenarios

		[UnityTest]
		public IEnumerator Stress_EconomyAgent_2000Steps()
		{
			// Full economy: gather, build barracks, train warriors.
			// Exercises pathfinding budget heavily (agents query paths every tick).
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			var pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 5, 0));
			var pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 8, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 8, 0));

			WireAgent(ctx.Agent0Go, 0, new StressEconomyAgent());

			var sim = BuildMatchingSimGame(new StressEconomyAgent(), new DoNothingAgent(),
				(0, UnitType.BASE, 5, 5),
				(0, UnitType.PAWN, 8, 5),
				(0, UnitType.PAWN, 8, 8),
				(-1, UnitType.MINE, 20, 8));

			yield return RunStressCheck(sim, 2000, "EconomyAgent");
		}

		[UnityTest]
		public IEnumerator Stress_CombatAgent_1500Steps()
		{
			// Multiple combat units attacking, retargeting, dying.
			// Exercises cooldown system (units die, commands to dead units fail).
			var w0 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));
			var a0 = PlaceUnit(UnitType.ARCHER, new Vector3Int(3, 15, 0));
			var l0 = PlaceUnit(UnitType.LANCER, new Vector3Int(5, 20, 0));
			var w1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(25, 10, 0), ctx.Agent1Go);
			var a1 = PlaceUnit(UnitType.ARCHER, new Vector3Int(27, 15, 0), ctx.Agent1Go);
			var l1 = PlaceUnit(UnitType.LANCER, new Vector3Int(25, 20, 0), ctx.Agent1Go);

			// Separate instances for each engine — agents have no mutable state so this is safe
			WireAgent(ctx.Agent0Go, 0, new StressCombatAgent());
			WireAgent(ctx.Agent1Go, 1, new StressCombatAgent());

			var sim = BuildMatchingSimGame(new StressCombatAgent(), new StressCombatAgent(),
				(0, UnitType.WARRIOR, 5, 10),
				(0, UnitType.ARCHER, 3, 15),
				(0, UnitType.LANCER, 5, 20),
				(1, UnitType.WARRIOR, 25, 10),
				(1, UnitType.ARCHER, 27, 15),
				(1, UnitType.LANCER, 25, 20));

			yield return RunStressCheck(sim, 1500, "CombatAgent");
		}

		[UnityTest]
		public IEnumerator Stress_FullGame_3000Steps()
		{
			// Complete game: economy + military + combat.
			// The ultimate parity stress test.
			var base0 = PlaceUnit(UnitType.BASE, new Vector3Int(3, 5, 0));
			base0.IsBuilt = true;
			var pawn0a = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));
			var pawn0b = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 8, 0));
			var base1 = PlaceUnit(UnitType.BASE, new Vector3Int(25, 25, 0), ctx.Agent1Go);
			base1.IsBuilt = true;
			var pawn1a = PlaceUnit(UnitType.PAWN, new Vector3Int(22, 25, 0), ctx.Agent1Go);
			var pawn1b = PlaceUnit(UnitType.PAWN, new Vector3Int(22, 22, 0), ctx.Agent1Go);
			var mine0 = PlaceUnit(UnitType.MINE, new Vector3Int(15, 10, 0));
			var mine1 = PlaceUnit(UnitType.MINE, new Vector3Int(15, 20, 0));

			WireAgent(ctx.Agent0Go, 0, new StressFullGameAgent());
			WireAgent(ctx.Agent1Go, 1, new StressFullGameAgent());

			var sim = BuildMatchingSimGame(new StressFullGameAgent(), new StressFullGameAgent(),
				(0, UnitType.BASE, 3, 5),
				(0, UnitType.PAWN, 6, 5),
				(0, UnitType.PAWN, 6, 8),
				(1, UnitType.BASE, 25, 25),
				(1, UnitType.PAWN, 22, 25),
				(1, UnitType.PAWN, 22, 22),
				(-1, UnitType.MINE, 15, 10),
				(-1, UnitType.MINE, 15, 20));

			yield return RunStressCheck(sim, 3000, "FullGame");
		}

		[UnityTest]
		public IEnumerator Stress_RapidFireCommands_500Steps()
		{
			// Agent issues many commands per tick to exercise cooldown/dedup.
			var base0 = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			base0.IsBuilt = true;
			for (int i = 0; i < 8; i++)
				PlaceUnit(UnitType.PAWN, new Vector3Int(8 + i, 10, 0));

			var unitSpecs = new List<(int, UnitType, int, int)>();
			unitSpecs.Add((0, UnitType.BASE, 5, 10));
			for (int i = 0; i < 8; i++)
				unitSpecs.Add((0, UnitType.PAWN, 8 + i, 10));

			WireAgent(ctx.Agent0Go, 0, new StressRapidFireAgent());

			var sim = BuildMatchingSimGame(new StressRapidFireAgent(), new DoNothingAgent(), unitSpecs.ToArray());

			yield return RunStressCheck(sim, 500, "RapidFireCommands");
		}

		[UnityTest]
		public IEnumerator Stress_PathfindingFlood_300Steps()
		{
			// Agent calls GetPathBetween 30+ times per tick to hit the budget.
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			WireAgent(ctx.Agent0Go, 0, new StressPathfloodAgent());

			var sim = BuildMatchingSimGame(new StressPathfloodAgent(), new DoNothingAgent(),
				(0, UnitType.PAWN, 5, 5));

			yield return RunStressCheck(sim, 300, "PathfindingFlood");
		}

		#endregion

		#region Stress Test Infrastructure

		private IEnumerator RunStressCheck(SimGame sim, int steps, string scenarioName)
		{
			for (int step = 0; step < steps; step++)
			{
				GameManager.Instance.SimulateTick();
				sim.Tick();

				// Compare gold
				int unityGold0 = ctx.GetAgent(0).Gold;
				int unityGold1 = ctx.GetAgent(1).Gold;
				if (sim.GetGold(0) != unityGold0 || sim.GetGold(1) != unityGold1)
				{
					Assert.Fail($"{scenarioName} step {step}: Gold diverged " +
						$"(sim=[{sim.GetGold(0)},{sim.GetGold(1)}], " +
						$"unity=[{unityGold0},{unityGold1}])");
				}

				// Compare unit count
				var allUnityUnits = ctx.UnitManager.GetAllUnits();
				int unityCount = allUnityUnits.Count;
				int simCount = sim.CountUnits();
				if (simCount != unityCount)
				{
					Assert.Fail($"{scenarioName} step {step}: UnitCount diverged " +
						$"(sim={simCount}, unity={unityCount})");
				}

				// Spot-check unit state every 10 steps (full check every step is too slow for 3000 steps)
				if (step % 10 == 0)
				{
					foreach (var kvp in allUnityUnits)
					{
						var uu = kvp.Value.GetComponent<Unit>();
						var su = sim.GetUnit(uu.UnitNbr);
						if (su == null)
						{
							Assert.Fail($"{scenarioName} step {step}: Unit {uu.UnitNbr} " +
								$"({uu.UnitType}) in Unity but not SimGame");
						}

						var isu = (ISimUnit)uu;
						if (su.GridPosition.X != uu.GridPosition.x ||
						    su.GridPosition.Y != uu.GridPosition.y)
						{
							Assert.Fail($"{scenarioName} step {step} unit {uu.UnitNbr}: " +
								$"Position diverged (sim=({su.GridPosition.X},{su.GridPosition.Y}), " +
								$"unity=({uu.GridPosition.x},{uu.GridPosition.y}))");
						}

						if (System.Math.Abs(su.Health - uu.Health) > 0.01f)
						{
							Assert.Fail($"{scenarioName} step {step} unit {uu.UnitNbr}: " +
								$"Health diverged (sim={su.Health:F2}, unity={uu.Health:F2})");
						}

						if (su.CurrentAction != uu.CurrentAction)
						{
							Assert.Fail($"{scenarioName} step {step} unit {uu.UnitNbr}: " +
								$"Action diverged (sim={su.CurrentAction}, unity={uu.CurrentAction})");
						}
					}
				}

				// Yield every 50 steps
				if (step % 50 == 0)
					yield return null;
			}
		}

		#endregion

		#region Stress Test Agents

		/// <summary>
		/// Economy agent: gathers with all pawns, builds barracks when affordable, trains warriors.
		/// </summary>
		private class StressEconomyAgent : IPlanningAgent
		{
			private bool gatherStarted;
			private bool buildStarted;

			public void InitializeMatch() { gatherStarted = false; buildStarted = false; }
			public void InitializeRound(IGameState state) { }
			public void Learn(IGameState state) { }

			public void Update(IGameState state, IAgentActions actions)
			{
				var pawns = state.GetMyUnits(UnitType.PAWN);
				var bases = state.GetMyUnits(UnitType.BASE);
				var mines = state.GetAllUnits(UnitType.MINE);
				var barracks = state.GetMyUnits(UnitType.BARRACKS);

				// Gather with all idle pawns
				if (mines.Count > 0 && bases.Count > 0)
				{
					foreach (int pNbr in pawns)
					{
						var info = state.GetUnit(pNbr);
						if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
							actions.Gather(pNbr, mines[0], bases[0]);
					}
				}

				// Build barracks with second pawn when we have enough gold
				if (!buildStarted && pawns.Count > 1 &&
				    state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
				{
					actions.Build(pawns[1], new Position(15, 15), UnitType.BARRACKS);
					buildStarted = true;
				}

				// Train warriors from barracks
				foreach (int bNbr in barracks)
				{
					var bInfo = state.GetUnit(bNbr);
					if (bInfo.HasValue && bInfo.Value.IsBuilt &&
					    bInfo.Value.CurrentAction == UnitAction.IDLE &&
					    state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
					{
						actions.Train(bNbr, UnitType.WARRIOR);
					}
				}
			}
		}

		/// <summary>
		/// Combat agent: attacks closest enemy with all combat units.
		/// Reissues attack commands each tick (tests cooldown system with dead targets).
		/// </summary>
		private class StressCombatAgent : IPlanningAgent
		{
			public void InitializeMatch() { }
			public void InitializeRound(IGameState state) { }
			public void Learn(IGameState state) { }

			public void Update(IGameState state, IAgentActions actions)
			{
				// Find first enemy
				int? targetNbr = null;
				foreach (var ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
				                           UnitType.PAWN, UnitType.BASE, UnitType.BARRACKS })
				{
					var enemies = state.GetEnemyUnits(ut);
					if (enemies.Count > 0) { targetNbr = enemies[0]; break; }
				}
				if (!targetNbr.HasValue) return;

				// Attack with all combat units
				foreach (var ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER })
				{
					foreach (int unitNbr in state.GetMyUnits(ut))
					{
						actions.Attack(unitNbr, targetNbr.Value);
					}
				}
			}
		}

		/// <summary>
		/// Full game agent: economy + military. Gathers, builds, trains, attacks.
		/// </summary>
		private class StressFullGameAgent : IPlanningAgent
		{
			private bool gatherStarted;
			private bool barracksStarted;

			public void InitializeMatch() { gatherStarted = false; barracksStarted = false; }
			public void InitializeRound(IGameState state) { }
			public void Learn(IGameState state) { }

			public void Update(IGameState state, IAgentActions actions)
			{
				var pawns = state.GetMyUnits(UnitType.PAWN);
				var bases = state.GetMyUnits(UnitType.BASE);
				var mines = state.GetAllUnits(UnitType.MINE);
				var barracks = state.GetMyUnits(UnitType.BARRACKS);
				var warriors = state.GetMyUnits(UnitType.WARRIOR);

				// Gather with first pawn
				if (!gatherStarted && pawns.Count > 0 && mines.Count > 0 && bases.Count > 0)
				{
					var info = state.GetUnit(pawns[0]);
					if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
					{
						actions.Gather(pawns[0], mines[0], bases[0]);
						gatherStarted = true;
					}
				}

				// Build barracks with second pawn
				if (!barracksStarted && pawns.Count > 1 &&
				    state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
				{
					var info = state.GetUnit(pawns[1]);
					if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
					{
						actions.Build(pawns[1], new Position(15, 15), UnitType.BARRACKS);
						barracksStarted = true;
					}
				}

				// Train warriors
				foreach (int bNbr in barracks)
				{
					var bInfo = state.GetUnit(bNbr);
					if (bInfo.HasValue && bInfo.Value.IsBuilt &&
					    bInfo.Value.CurrentAction == UnitAction.IDLE &&
					    state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
					{
						actions.Train(bNbr, UnitType.WARRIOR);
					}
				}

				// Attack with warriors
				if (warriors.Count > 0)
				{
					int? targetNbr = null;
					foreach (var ut in new[] { UnitType.WARRIOR, UnitType.PAWN, UnitType.BASE })
					{
						var enemies = state.GetEnemyUnits(ut);
						if (enemies.Count > 0) { targetNbr = enemies[0]; break; }
					}
					if (targetNbr.HasValue)
					{
						foreach (int wNbr in warriors)
						{
							var wInfo = state.GetUnit(wNbr);
							if (wInfo.HasValue && wInfo.Value.CurrentAction == UnitAction.IDLE)
								actions.Attack(wNbr, targetNbr.Value);
						}
					}
				}
			}
		}

		/// <summary>
		/// Rapid fire agent: issues many commands per tick to stress cooldown and dedup.
		/// Moves all pawns to random valid positions every tick.
		/// </summary>
		private class StressRapidFireAgent : IPlanningAgent
		{
			private int tick;

			public void InitializeMatch() { tick = 0; }
			public void InitializeRound(IGameState state) { }
			public void Learn(IGameState state) { }

			public void Update(IGameState state, IAgentActions actions)
			{
				tick++;
				var pawns = state.GetMyUnits(UnitType.PAWN);

				// Move all pawns to a new position each tick (deterministic, based on tick number)
				for (int i = 0; i < pawns.Count; i++)
				{
					int x = 5 + ((tick * 7 + i * 3) % 20);
					int y = 5 + ((tick * 11 + i * 5) % 20);
					actions.Move(pawns[i], new Position(x, y));
				}

				// Also try to train from base every tick (will fail most of the time — tests cooldown)
				var bases = state.GetMyUnits(UnitType.BASE);
				foreach (int bNbr in bases)
				{
					actions.Train(bNbr, UnitType.PAWN);
				}
			}
		}

		/// <summary>
		/// Pathfinding flood agent: calls GetPathBetween 30+ times per tick.
		/// Tests the pathfinding budget (20 calls/tick cap).
		/// </summary>
		private class StressPathfloodAgent : IPlanningAgent
		{
			private int tick;

			public void InitializeMatch() { tick = 0; }
			public void InitializeRound(IGameState state) { }
			public void Learn(IGameState state) { }

			public void Update(IGameState state, IAgentActions actions)
			{
				tick++;
				// Query 30 paths per tick — exceeds budget of 20
				for (int i = 0; i < 30; i++)
				{
					int x = 2 + (i % 26);
					int y = 2 + ((tick + i) % 26);
					state.GetPathBetween(new Position(5, 5), new Position(x, y));
				}

				// Move the pawn based on tick to keep things dynamic
				var pawns = state.GetMyUnits(UnitType.PAWN);
				if (pawns.Count > 0)
				{
					int x = 5 + (tick % 20);
					int y = 5 + ((tick * 3) % 20);
					actions.Move(pawns[0], new Position(x, y));
				}
			}
		}

		#endregion
	}
}
