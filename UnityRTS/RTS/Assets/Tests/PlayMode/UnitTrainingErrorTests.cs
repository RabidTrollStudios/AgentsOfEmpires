using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for unit training error conditions and stress scenarios:
	/// blocked spawn (all neighbors occupied), duplicate commands, invalid types,
	/// and sequential back-to-back training.
	/// </summary>
	[TestFixture]
	public class UnitTrainingErrorTests : PlayModeTestBase
	{
		// ── Helpers ────────────────────────────────────────────────────────────

		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		private void TickUnit(Unit unit) { unit.TickFixedUpdate(); unit.Update(); }

		// ── Error conditions ───────────────────────────────────────────────────

		/// <summary>
		/// When all spawn cells near the base are occupied the base remains in TRAIN
		/// indefinitely without crashing.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWithAllNeighborsOccupied_StaysInTrain()
		{
			Vector3Int basePos = new Vector3Int(15, 15, 0);
			Unit baseUnit = PlaceBuiltBase(basePos);

			// Occupy the actual SPAWN candidate cells. Training spawns via the shared
			// Grid.GetBuildablePositionsNearUnit (OPEN cells only) — NOT MapManager's
			// GetBuildableGridPositionsNearUnit, which also returns the building's
			// walkable passage row (those can't be "occupied away" by placing a pawn,
			// so using that query here left the passage cells and failed the precondition).
			var anchor = new Position(basePos.x, basePos.y);
			var spawnCells = ctx.MapManager.Grid.GetBuildablePositionsNearUnit(UnitType.BASE, anchor);

			foreach (var p in spawnCells)
				PlaceUnit(UnitType.PAWN, new Vector3Int(p.X, p.Y, 0));

			var remaining = ctx.MapManager.Grid.GetBuildablePositionsNearUnit(UnitType.BASE, anchor);
			Assert.AreEqual(0, remaining.Count,
				"All spawn candidate cells should be occupied before training");

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			int unitCountBefore = ctx.UnitManager.GetAllUnits().Count;

			float waited = 0f;
			while (waited < 3f)
			{
				TickUnit(baseUnit);
				waited += Time.deltaTime;
				yield return null;
			}

			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"Base should remain in TRAIN when no spawn cell is available");
			Assert.AreEqual(unitCountBefore, ctx.UnitManager.GetAllUnits().Count,
				"No new unit should have been created");
		}

		/// <summary>
		/// A second train command while already training should be rejected;
		/// gold is deducted only once.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainWhileAlreadyTraining_SecondCommandRejected()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;
			int pawnCost = (int)Constants.COST[UnitType.PAWN];

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);
			Assert.AreEqual(goldBefore - pawnCost, agent.Gold,
				"Gold should be deducted for the first train command");

			int goldAfterFirst = agent.Gold;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
			Assert.AreEqual(goldAfterFirst, agent.Gold,
				"Gold should NOT be deducted for a rejected second train command");

			yield return null;
		}

		/// <summary>
		/// Training a unit type the building cannot produce should be rejected with gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainInvalidUnitType_CommandRejectedGoldUnchanged()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();

			int goldBefore = agent.Gold;

			// BASE can only train PAWN, not WARRIOR
			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WARRIOR));

			Assert.AreEqual(UnitAction.IDLE, baseUnit.CurrentAction,
				"Base should remain IDLE after invalid train command");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not change when training an invalid unit type");

			yield return null;
		}

		// ── Stress ────────────────────────────────────────────────────────────

		/// <summary>
		/// Train 5 pawns sequentially from the same base; verify all 5 exist
		/// on distinct cells.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainFivePawnsSequentially_AllExistOnDistinctCells()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			int initialUnitCount = ctx.UnitManager.GetAllUnits().Count;

			for (int i = 0; i < 5; i++)
			{
				baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
				Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
					$"Training iteration {i}: base should be in TRAIN");

				yield return WaitUntil(() =>
				{
					TickUnit(baseUnit);
					return baseUnit.CurrentAction == UnitAction.IDLE;
				}, timeoutSeconds: 5f, failMessage: $"Training iteration {i} did not complete");
			}

			int finalUnitCount = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(initialUnitCount + 5, finalUnitCount,
				"5 additional pawns should exist after training");

			List<Unit> pawns = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.PAWN)
				.ToList();

			HashSet<Vector3Int> positions = new HashSet<Vector3Int>();
			foreach (Unit w in pawns)
			{
				Assert.IsTrue(positions.Add(w.GridPosition),
					$"Pawn at {w.GridPosition} shares a cell with another pawn");
			}
		}
	}
}
