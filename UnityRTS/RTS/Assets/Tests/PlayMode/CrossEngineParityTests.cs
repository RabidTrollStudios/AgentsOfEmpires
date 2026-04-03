using System.Collections;
using AgentSDK;
using AgentTestHarness;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Cross-engine parity tests: sets up identical game state in both Unity
	/// and SimGame, issues the same commands via CommandProcessor, advances
	/// both engines in lockstep, and asserts state matches at every step.
	///
	/// These tests verify that Unity and SimGame produce identical gameplay
	/// outcomes — not just that SimGame is internally deterministic (which
	/// the record-replay ParityTests already cover).
	/// </summary>
	public class CrossEngineParityTests : PlayModeTestBase
	{
		private const int MAP_W = 30;
		private const int MAP_H = 30;
		private const int STARTING_GOLD = 5000;
		private const int MINE_GOLD = 10000;

		/// <summary>
		/// Actual step duration read back from Unity after setting Time.fixedDeltaTime.
		/// Unity quantizes this value, so it may differ from the literal 0.02f.
		/// SimGame must use this exact value for bit-identical parity.
		/// </summary>
		private float _actualStepDuration;

		/// <summary>
		/// Ensure GameManager.Agents is populated so UnitySimWorld.GetGold/SpawnUnit work.
		/// The test helper creates agent GameObjects but doesn't register them with GameManager.
		/// </summary>
		private void EnsureAgentsRegistered()
		{
			var gm = GameManager.Instance;
			if (gm.Agents == null)
			{
				gm.Agents = new System.Collections.Generic.Dictionary<int, GameObject>
				{
					[0] = ctx.Agent0Go,
					[1] = ctx.Agent1Go
				};
			}

			// UnitySimWorld.StepDuration reads Time.fixedDeltaTime. The test helper
			// skips Awake() (which sets it to 0.02), so it stays at Unity's default
			// 0.02 — matching SimConfig. Set explicitly and log for debugging.
			// Unity quantizes fixedDeltaTime internally, so the value read back
			// differs from what we set by a few ULPs. Use Unity's actual value for
			// SimConfig so both engines have bit-identical step durations.
			Time.fixedDeltaTime = 0.02f;
			_actualStepDuration = Time.fixedDeltaTime;
		}

		#region Test Scenarios

		[UnityTest]
		public IEnumerator Parity_IdleUnits_IdenticalState()
		{
			var pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.PAWN, 5, 5),
				(1, UnitType.PAWN, 15, 15));

			yield return RunParityCheck(sim, 60, "IdleUnits");
		}

		[UnityTest]
		public IEnumerator Parity_PawnMovement_IdenticalState()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 10, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.PAWN, 5, 10));

			// Issue move command to both engines
			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessMove(
				(ISimUnit)pawn, new Position(25, 10), unityWorld);
			CommandProcessor.ProcessMove(
				sim.GetUnit(0), new Position(25, 10), simWorld);

			yield return RunParityCheck(sim, 600, "PawnMovement");
		}

		[UnityTest]
		public IEnumerator Parity_WarriorCombat_IdenticalState()
		{
			var warrior0 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 15, 0));
			var warrior1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(20, 15, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.WARRIOR, 10, 15),
				(1, UnitType.WARRIOR, 20, 15));

			// Both attack each other
			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessAttack((ISimUnit)warrior0, warrior1.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)warrior1, warrior0.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(0), 1, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(1), 0, simWorld);

			yield return RunParityCheck(sim, 900, "WarriorCombat");
		}

		[UnityTest]
		public IEnumerator Parity_ArcherVsWarrior_IdenticalState()
		{
			var archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 15, 0));
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(25, 15, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.ARCHER, 5, 15),
				(1, UnitType.WARRIOR, 25, 15));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessAttack((ISimUnit)archer, warrior.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)warrior, archer.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(0), 1, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(1), 0, simWorld);

			yield return RunParityCheck(sim, 900, "ArcherVsWarrior");
		}

		[UnityTest]
		public IEnumerator Parity_TrainPawn_IdenticalState()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 5, 10));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessTrain((ISimUnit)baseUnit, UnitType.PAWN, unityWorld);
			CommandProcessor.ProcessTrain(sim.GetUnit(0), UnitType.PAWN, simWorld);

			yield return RunParityCheck(sim, 300, "TrainPawn");
		}

		[UnityTest]
		public IEnumerator Parity_BuildBarracks_IdenticalState()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(3, 8, 0));
			baseUnit.IsBuilt = true;
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 3, 8),
				(0, UnitType.PAWN, 6, 5));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessBuild(
				(ISimUnit)pawn, new Position(15, 15), UnitType.BARRACKS, unityWorld);
			CommandProcessor.ProcessBuild(
				sim.GetUnit(1), new Position(15, 15), UnitType.BARRACKS, simWorld);

			yield return RunParityCheck(sim, 1500, "BuildBarracks");
		}

		[UnityTest]
		public IEnumerator Parity_GatherCycle_IdenticalState()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(3, 8, 0));
			baseUnit.IsBuilt = true;
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 8, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 3, 8),
				(0, UnitType.PAWN, 6, 5),
				(-1, UnitType.MINE, 20, 8));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessGather(
				(ISimUnit)pawn, mine.UnitNbr, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessGather(
				sim.GetUnit(1), sim.GetUnit(2).UnitNbr, sim.GetUnit(0).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 1500, "GatherCycle");
		}

		[UnityTest]
		public IEnumerator Parity_RepairBuilding_IdenticalState()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;
			baseUnit.Health = GameConstants.HEALTH[UnitType.BASE] * 0.5f;
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 5, 10),
				(0, UnitType.PAWN, 8, 10));
			// Damage the sim base to match
			sim.GetUnit(0).Health = GameConstants.HEALTH[UnitType.BASE] * 0.5f;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessRepair(
				(ISimUnit)pawn, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessRepair(
				sim.GetUnit(1), sim.GetUnit(0).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 600, "RepairBuilding");
		}

		[UnityTest]
		public IEnumerator Parity_MonkHeal_IdenticalState()
		{
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			warrior.Health = GameConstants.HEALTH[UnitType.WARRIOR] * 0.5f;
			var monk = PlaceUnit(UnitType.MONK, new Vector3Int(8, 10, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.WARRIOR, 10, 10),
				(0, UnitType.MONK, 8, 10));
			sim.GetUnit(0).Health = GameConstants.HEALTH[UnitType.WARRIOR] * 0.5f;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessHeal(
				(ISimUnit)monk, warrior.UnitNbr, unityWorld);
			CommandProcessor.ProcessHeal(
				sim.GetUnit(1), sim.GetUnit(0).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 300, "MonkHeal");
		}

		[UnityTest]
		public IEnumerator Parity_LancerVsArcher_IdenticalState()
		{
			var lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(5, 15, 0));
			var archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(25, 15, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.LANCER, 5, 15),
				(1, UnitType.ARCHER, 25, 15));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessAttack((ISimUnit)lancer, archer.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)archer, lancer.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(0), 1, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(1), 0, simWorld);

			yield return RunParityCheck(sim, 900, "LancerVsArcher");
		}

		// ── Edge Case: Death During Gather ────────────────────────────────

		[UnityTest]
		public IEnumerator Parity_GathererKilledMidTrip_IdenticalState()
		{
			// Pawn gathers gold, enemy warrior kills it while carrying gold.
			// Carried gold must be lost identically in both engines.
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(3, 8, 0));
			baseUnit.IsBuilt = true;
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(12, 8, 0));
			var enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(9, 8, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 3, 8),
				(0, UnitType.PAWN, 6, 5),
				(-1, UnitType.MINE, 12, 8),
				(1, UnitType.WARRIOR, 9, 8));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// Pawn gathers, enemy attacks the pawn
			CommandProcessor.ProcessGather((ISimUnit)pawn, mine.UnitNbr, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)enemy, pawn.UnitNbr, unityWorld);
			CommandProcessor.ProcessGather(sim.GetUnit(1), sim.GetUnit(2).UnitNbr, sim.GetUnit(0).UnitNbr, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(3), sim.GetUnit(1).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 750, "GathererKilledMidTrip");
		}

		// ── Edge Case: Mine Depletion During Mining ───────────────────────

		[UnityTest]
		public IEnumerator Parity_MineDepletedDuringMining_IdenticalState()
		{
			// Mine has very low health — depletes while pawns are mining.
			// Gold extraction and phase transitions must match.
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(3, 8, 0));
			baseUnit.IsBuilt = true;
			var pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));
			var pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 10, 0));
			var mine = PlaceUnit(UnitType.MINE, new Vector3Int(12, 8, 0));
			mine.Health = 30; // Very low — will deplete quickly with 2 pawns

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 3, 8),
				(0, UnitType.PAWN, 6, 5),
				(0, UnitType.PAWN, 6, 10),
				(-1, UnitType.MINE, 12, 8));
			sim.GetUnit(3).Health = 30;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessGather((ISimUnit)pawn0, mine.UnitNbr, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessGather((ISimUnit)pawn1, mine.UnitNbr, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessGather(sim.GetUnit(1), sim.GetUnit(3).UnitNbr, sim.GetUnit(0).UnitNbr, simWorld);
			CommandProcessor.ProcessGather(sim.GetUnit(2), sim.GetUnit(3).UnitNbr, sim.GetUnit(0).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 1500, "MineDepletedDuringMining");
		}

		// ── Edge Case: Training With Blocked Spawn ────────────────────────

		[UnityTest]
		public IEnumerator Parity_TrainWithBlockedSpawn_IdenticalState()
		{
			// Base surrounded by pawns — training completes but no spawn cell.
			// Timer should reset and retry. Units must spawn identically when a
			// cell opens (pawn moves away).
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;
			// Surround with pawns (BASE is 3x3, neighbors are the ring around it)
			var blocker0 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 7, 0));
			var blocker1 = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 7, 0));
			var blocker2 = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 7, 0));
			var blocker3 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 7, 0));
			var blocker4 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 8, 0));
			var blocker5 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 8, 0));
			var blocker6 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 9, 0));
			var blocker7 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 9, 0));
			var blocker8 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			var blocker9 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 10, 10),
				(0, UnitType.PAWN, 9, 7),
				(0, UnitType.PAWN, 10, 7),
				(0, UnitType.PAWN, 11, 7),
				(0, UnitType.PAWN, 12, 7),
				(0, UnitType.PAWN, 9, 8),
				(0, UnitType.PAWN, 12, 8),
				(0, UnitType.PAWN, 9, 9),
				(0, UnitType.PAWN, 12, 9),
				(0, UnitType.PAWN, 9, 10),
				(0, UnitType.PAWN, 12, 10));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// Train a pawn — spawn should be blocked initially
			CommandProcessor.ProcessTrain((ISimUnit)baseUnit, UnitType.PAWN, unityWorld);
			CommandProcessor.ProcessTrain(sim.GetUnit(0), UnitType.PAWN, simWorld);

			// After some ticks, move one blocker away to free a spawn cell
			// We run 100 steps with blocked spawn, then move blocker0 away
			for (int step = 0; step < 100; step++)
			{
				GameManager.Instance.SimulateTick();
				sim.Tick();
				yield return null;
			}

			// Move blocker out of the way on both engines
			CommandProcessor.ProcessMove((ISimUnit)blocker0, new Position(5, 5), unityWorld);
			CommandProcessor.ProcessMove(sim.GetUnit(1), new Position(5, 5), simWorld);

			yield return RunParityCheck(sim, 500, "TrainWithBlockedSpawn");
		}

		// ── Edge Case: Unbuilt Building Under Attack ──────────────────────

		[UnityTest]
		public IEnumerator Parity_UnbuiltBuildingAttacked_IdenticalState()
		{
			// Pawn builds barracks while enemy archer attacks it.
			// Building takes damage while under construction.
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(3, 8, 0));
			baseUnit.IsBuilt = true;
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));
			var archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(20, 15, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 3, 8),
				(0, UnitType.PAWN, 6, 5),
				(1, UnitType.ARCHER, 20, 15));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// Pawn builds barracks near the archer
			CommandProcessor.ProcessBuild((ISimUnit)pawn, new Position(15, 15), UnitType.BARRACKS, unityWorld);
			CommandProcessor.ProcessBuild(sim.GetUnit(1), new Position(15, 15), UnitType.BARRACKS, simWorld);

			// Archer attacks the unbuilt barracks (unitNbr 3 in both engines — spawned by ProcessBuild)
			// Need to wait a tick for the building to be spawned first
			GameManager.Instance.SimulateTick();
			sim.Tick();

			var unbuiltBarracks = ctx.UnitManager.GetUnit(3);
			if (unbuiltBarracks != null)
			{
				CommandProcessor.ProcessAttack((ISimUnit)archer, 3, unityWorld);
				CommandProcessor.ProcessAttack(sim.GetUnit(2), 3, simWorld);
			}

			yield return RunParityCheck(sim, 1500, "UnbuiltBuildingAttacked");
		}

		// ── Edge Case: Multiple Pawns Repair Same Building ────────────────

		[UnityTest]
		public IEnumerator Parity_TwoPawnsRepairSameBuilding_IdenticalState()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;
			baseUnit.Health = GameConstants.HEALTH[UnitType.BASE] * 0.3f;
			var pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			var pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 8, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.BASE, 5, 10),
				(0, UnitType.PAWN, 8, 10),
				(0, UnitType.PAWN, 8, 8));
			sim.GetUnit(0).Health = GameConstants.HEALTH[UnitType.BASE] * 0.3f;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessRepair((ISimUnit)pawn0, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessRepair((ISimUnit)pawn1, baseUnit.UnitNbr, unityWorld);
			CommandProcessor.ProcessRepair(sim.GetUnit(1), sim.GetUnit(0).UnitNbr, simWorld);
			CommandProcessor.ProcessRepair(sim.GetUnit(2), sim.GetUnit(0).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 750, "TwoPawnsRepairSameBuilding");
		}

		// ── Edge Case: Monk Heal at Exact Mana Boundary ───────────────────

		[UnityTest]
		public IEnumerator Parity_MonkHealExactMana_IdenticalState()
		{
			// Monk starts with exactly MANA_COST mana — heal should succeed.
			// After heal, mana = 0 and regen must tick identically.
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			warrior.Health = GameConstants.HEALTH[UnitType.WARRIOR] * 0.3f;
			var monk = PlaceUnit(UnitType.MONK, new Vector3Int(8, 10, 0));
			monk.Mana = GameConstants.MANA_COST; // Exactly 10

			var sim = BuildMatchingSimGame(
				(0, UnitType.WARRIOR, 10, 10),
				(0, UnitType.MONK, 8, 10));
			sim.GetUnit(0).Health = GameConstants.HEALTH[UnitType.WARRIOR] * 0.3f;
			sim.GetUnit(1).Mana = GameConstants.MANA_COST;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessHeal((ISimUnit)monk, warrior.UnitNbr, unityWorld);
			CommandProcessor.ProcessHeal(sim.GetUnit(1), sim.GetUnit(0).UnitNbr, simWorld);

			// Run long enough for mana to regen and potentially heal again
			yield return RunParityCheck(sim, 500, "MonkHealExactMana");
		}

		// ── Edge Case: Attacker Kills Target, Another Enemy In Range ──────

		[UnityTest]
		public IEnumerator Parity_AttackerKillsTargetWithOtherEnemyNearby_IdenticalState()
		{
			// Warrior attacks weak enemy. Another full-health enemy is adjacent.
			// After killing target, attacker should go IDLE (not auto-retarget).
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			var weakEnemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			weakEnemy.Health = 1f; // Dies in one hit
			var strongEnemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(12, 10, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.WARRIOR, 10, 10),
				(1, UnitType.PAWN, 11, 10),
				(1, UnitType.WARRIOR, 12, 10));
			sim.GetUnit(1).Health = 1f;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// Warrior attacks weak pawn
			CommandProcessor.ProcessAttack((ISimUnit)warrior, weakEnemy.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(0), sim.GetUnit(1).UnitNbr, simWorld);

			yield return RunParityCheck(sim, 300, "AttackerKillsTargetWithOtherEnemyNearby");
		}

		// ── Edge Case: Two Units Move To Same Cell ────────────────────────

		[UnityTest]
		public IEnumerator Parity_TwoUnitsMoveSameDestination_IdenticalState()
		{
			var pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 10, 0));
			var pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(25, 10, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.PAWN, 5, 10),
				(0, UnitType.PAWN, 25, 10));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// Both move to same cell
			CommandProcessor.ProcessMove((ISimUnit)pawn0, new Position(15, 10), unityWorld);
			CommandProcessor.ProcessMove((ISimUnit)pawn1, new Position(15, 10), unityWorld);
			CommandProcessor.ProcessMove(sim.GetUnit(0), new Position(15, 10), simWorld);
			CommandProcessor.ProcessMove(sim.GetUnit(1), new Position(15, 10), simWorld);

			yield return RunParityCheck(sim, 750, "TwoUnitsMoveSameDestination");
		}

		// ── Edge Case: Long Diagonal Path ─────────────────────────────────

		[UnityTest]
		public IEnumerator Parity_LongDiagonalPath_IdenticalState()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(2, 2, 0));

			var sim = BuildMatchingSimGame(
				(0, UnitType.PAWN, 2, 2));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// Move to far diagonal corner
			CommandProcessor.ProcessMove((ISimUnit)pawn, new Position(28, 28), unityWorld);
			CommandProcessor.ProcessMove(sim.GetUnit(0), new Position(28, 28), simWorld);

			yield return RunParityCheck(sim, 1500, "LongDiagonalPath");
		}

		// ── Edge Case: Simultaneous Death (Both Units Kill Each Other) ────

		[UnityTest]
		public IEnumerator Parity_SimultaneousDeath_IdenticalState()
		{
			// Two warriors at low health, adjacent, attacking each other.
			// Both should die on the same step.
			var w0 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			w0.Health = 1f;
			var w1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			w1.Health = 1f;

			var sim = BuildMatchingSimGame(
				(0, UnitType.WARRIOR, 10, 10),
				(1, UnitType.WARRIOR, 11, 10));
			sim.GetUnit(0).Health = 1f;
			sim.GetUnit(1).Health = 1f;

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			CommandProcessor.ProcessAttack((ISimUnit)w0, w1.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)w1, w0.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(0), 1, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(1), 0, simWorld);

			yield return RunParityCheck(sim, 100, "SimultaneousDeath");
		}

		// ── Edge Case: Multi-Unit Combat With Mixed Types ─────────────────

		[UnityTest]
		public IEnumerator Parity_LargeArmyCombat_IdenticalState()
		{
			// 4v4 combat with all military unit types.
			// Tests ordering, retargeting, death cascades.
			var w0 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 12, 0));
			var a0 = PlaceUnit(UnitType.ARCHER, new Vector3Int(3, 15, 0));
			var l0 = PlaceUnit(UnitType.LANCER, new Vector3Int(5, 18, 0));
			var m0 = PlaceUnit(UnitType.MONK, new Vector3Int(2, 15, 0));
			var w1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(25, 12, 0), ctx.Agent1Go);
			var a1 = PlaceUnit(UnitType.ARCHER, new Vector3Int(27, 15, 0), ctx.Agent1Go);
			var l1 = PlaceUnit(UnitType.LANCER, new Vector3Int(25, 18, 0), ctx.Agent1Go);
			var m1 = PlaceUnit(UnitType.MONK, new Vector3Int(28, 15, 0), ctx.Agent1Go);

			var sim = BuildMatchingSimGame(
				(0, UnitType.WARRIOR, 5, 12),
				(0, UnitType.ARCHER, 3, 15),
				(0, UnitType.LANCER, 5, 18),
				(0, UnitType.MONK, 2, 15),
				(1, UnitType.WARRIOR, 25, 12),
				(1, UnitType.ARCHER, 27, 15),
				(1, UnitType.LANCER, 25, 18),
				(1, UnitType.MONK, 28, 15));

			var unityWorld = GameManager.Instance.GetTickWorld();
			var simWorld = sim.GetSimWorld();

			// All military units attack the first enemy of each type
			CommandProcessor.ProcessAttack((ISimUnit)w0, w1.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)a0, a1.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)l0, l1.UnitNbr, unityWorld);
			CommandProcessor.ProcessHeal((ISimUnit)m0, w0.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)w1, w0.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)a1, a0.UnitNbr, unityWorld);
			CommandProcessor.ProcessAttack((ISimUnit)l1, l0.UnitNbr, unityWorld);
			CommandProcessor.ProcessHeal((ISimUnit)m1, w1.UnitNbr, unityWorld);

			CommandProcessor.ProcessAttack(sim.GetUnit(0), 4, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(1), 5, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(2), 6, simWorld);
			CommandProcessor.ProcessHeal(sim.GetUnit(3), 0, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(4), 0, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(5), 1, simWorld);
			CommandProcessor.ProcessAttack(sim.GetUnit(6), 2, simWorld);
			CommandProcessor.ProcessHeal(sim.GetUnit(7), 4, simWorld);

			yield return RunParityCheck(sim, 1500, "LargeArmyCombat");
		}

		#endregion

		#region Parity Infrastructure

		/// <summary>
		/// Build a SimGame with matching map size, gold, and units.
		/// Units must be specified in the same order they were placed in Unity
		/// so UnitNbr assignment matches.
		/// </summary>
		private SimGame BuildMatchingSimGame(
			params (int owner, UnitType type, int x, int y)[] units)
		{
			EnsureAgentsRegistered();

			// Use Unity's actual quantized fixedDeltaTime for SimConfig so both
			// engines have bit-identical step durations.
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

			builder.WithAgent(0, new DoNothingAgent())
			       .WithAgent(1, new DoNothingAgent());

			var sim = builder.Build();
			sim.InitializeMatch();
			sim.InitializeRound();
			return sim;
		}

		/// <summary>
		/// Advance both engines in lockstep for the given number of steps,
		/// comparing state after each step. Fails on first divergence.
		/// </summary>
		private IEnumerator RunParityCheck(SimGame sim, int steps, string scenarioName)
		{
			var allUnityUnits = ctx.UnitManager.GetAllUnits();
			var diag = new System.Text.StringBuilder();

			for (int step = 0; step < steps; step++)
			{
				// Collect pre-tick diagnostics for combat scenarios
				if (step < 100)
				{
					diag.Append($"[step {step}] ");
					foreach (var kvp in ctx.UnitManager.GetAllUnits())
					{
						var uu = kvp.Value.GetComponent<Unit>();
						var su = sim.GetUnit(uu.UnitNbr);
						if (su != null)
						{
							var iuu = (ISimUnit)uu;
							diag.Append($"u{uu.UnitNbr}(uH={uu.Health:F2} sH={su.Health:F2} " +
								$"uA={uu.CurrentAction} sA={su.CurrentAction} " +
								$"uPos={uu.GridPosition.x},{uu.GridPosition.y} " +
								$"sPos={su.GridPosition.X},{su.GridPosition.Y} " +
								$"uPP={iuu.PathProgress:F3} sPP={su.PathProgress:F3}) ");
						}
					}
					diag.AppendLine();
				}

				// Advance both engines one step
				GameManager.Instance.SimulateTick();
				sim.Tick();

				// Compare gold
				int unityGold0 = ctx.GetAgent(0).Gold;
				int unityGold1 = ctx.GetAgent(1).Gold;
				Assert.AreEqual(sim.GetGold(0), unityGold0,
					$"{scenarioName} step {step}: Gold[0] diverged (sim={sim.GetGold(0)}, unity={unityGold0})");
				Assert.AreEqual(sim.GetGold(1), unityGold1,
					$"{scenarioName} step {step}: Gold[1] diverged (sim={sim.GetGold(1)}, unity={unityGold1})");

				// Compare unit count
				allUnityUnits = ctx.UnitManager.GetAllUnits();
				int unityCount = allUnityUnits.Count;
				int simCount = sim.CountUnits();
				if (simCount != unityCount)
				{
					// Detail: list which units exist in each engine
					var extraInfo = new System.Text.StringBuilder();
					extraInfo.AppendLine($"Unity units ({unityCount}):");
					foreach (var kvp2 in allUnityUnits)
					{
						var uu2 = kvp2.Value.GetComponent<Unit>();
						extraInfo.AppendLine($"  #{uu2.UnitNbr} {uu2.UnitType} hp={uu2.Health:F2} action={uu2.CurrentAction}");
					}
					extraInfo.AppendLine($"SimGame units ({simCount}):");
					for (int j = 0; j < 20; j++)
					{
						var su = sim.GetUnit(j);
						if (su != null)
							extraInfo.AppendLine($"  #{su.UnitNbr} {su.UnitType} hp={su.Health:F2} action={su.CurrentAction}");
					}
					Assert.Fail($"{scenarioName} step {step}: UnitCount diverged " +
						$"(sim={simCount}, unity={unityCount})\n{extraInfo}\nDiagnostics (last 15 steps):\n{GetLastLines(diag, 15)}");
				}

				// Compare each unit
				foreach (var kvp in allUnityUnits)
				{
					var unityUnit = kvp.Value.GetComponent<Unit>();
					var simUnit = sim.GetUnit(unityUnit.UnitNbr);

					Assert.IsNotNull(simUnit,
						$"{scenarioName} step {step}: Unit {unityUnit.UnitNbr} ({unityUnit.UnitType}) exists in Unity but not SimGame");

					AssertUnitParity(unityUnit, simUnit, step, scenarioName);
				}

				// Check for SimGame units not in Unity
				for (int i = 0; i < sim.CountUnits() + 10; i++)
				{
					var simUnit = sim.GetUnit(i);
					if (simUnit != null && !allUnityUnits.ContainsKey(i))
					{
						Assert.Fail($"{scenarioName} step {step}: Unit {i} ({simUnit.UnitType}) " +
							"exists in SimGame but not Unity");
					}
				}

				// Yield every 60 steps to avoid Unity editor timeout
				if (step % 60 == 0)
					yield return null;
			}
		}

		/// <summary>
		/// Compare a single unit's state between Unity and SimGame.
		/// </summary>
		private void AssertUnitParity(Unit unity, SimUnit sim, int step, string scenario)
		{
			string id = $"{scenario} step {step} unit {unity.UnitNbr} ({unity.UnitType})";
			var isu = (ISimUnit)unity;

			Assert.AreEqual(sim.GridPosition.X, unity.GridPosition.x,
				$"{id}: GridPosition.X diverged (sim={sim.GridPosition.X}, unity={unity.GridPosition.x})");
			Assert.AreEqual(sim.GridPosition.Y, unity.GridPosition.y,
				$"{id}: GridPosition.Y diverged (sim={sim.GridPosition.Y}, unity={unity.GridPosition.y})");

			Assert.AreEqual(sim.Health, unity.Health, 0.01f,
				$"{id}: Health diverged (sim={sim.Health:F2}, unity={unity.Health:F2})");

			Assert.AreEqual(sim.CurrentAction, unity.CurrentAction,
				$"{id}: Action diverged (sim={sim.CurrentAction}, unity={unity.CurrentAction})");

			Assert.AreEqual(sim.IsBuilt, unity.IsBuilt,
				$"{id}: IsBuilt diverged (sim={sim.IsBuilt}, unity={unity.IsBuilt})");

			Assert.AreEqual(sim.PathProgress, isu.PathProgress, 0.001f,
				$"{id}: PathProgress diverged (sim={sim.PathProgress:F4}, unity={isu.PathProgress:F4})");

			Assert.AreEqual(sim.PathIndex, isu.PathIndex,
				$"{id}: PathIndex diverged (sim={sim.PathIndex}, unity={isu.PathIndex})");
		}

		private static string GetLastLines(System.Text.StringBuilder sb, int count)
		{
			var lines = sb.ToString().Split('\n');
			int start = lines.Length > count ? lines.Length - count : 0;
			return string.Join("\n", lines, start, lines.Length - start);
		}

		#endregion
	}
}
