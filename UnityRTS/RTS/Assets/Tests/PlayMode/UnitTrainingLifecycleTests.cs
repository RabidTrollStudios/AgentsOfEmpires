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
	/// PlayMode tests for the unit training happy path and boundary conditions:
	/// completion, state transitions, spawn position, and speed scaling.
	/// </summary>
	[TestFixture]
	public class UnitTrainingLifecycleTests : PlayModeTestBase
	{
		// ── Helpers ────────────────────────────────────────────────────────────

		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		private void TickUnit(Unit unit) => unit.Update();

		// ── Happy path ─────────────────────────────────────────────────────────

		/// <summary>
		/// At GAME_SPEED=20: CREATION_TIME[WORKER] = (1/20)*2 = 0.1 s
		/// </summary>
		[UnityTest]
		public IEnumerator BaseTrainsWorker_NewWorkerAppearsAfterCreationTime()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"Base should be in TRAIN action after StartTraining");

			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 5f, failMessage: "New worker never appeared after training");

			Assert.AreEqual(unitsBefore + 1, ctx.UnitManager.GetAllUnits().Count,
				"Exactly one new unit should have been created");
		}

		[UnityTest]
		public IEnumerator BaseTrainsWorker_ActionIsTrainThenIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction);

			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return baseUnit.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 5f, failMessage: "Base never returned to IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, baseUnit.CurrentAction);
		}

		[UnityTest]
		public IEnumerator TrainedWorker_HasCorrectTypeAndOccupiesCell()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 5f, failMessage: "Trained worker never appeared");

			Unit newWorker = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.WORKER)
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Assert.AreEqual(UnitType.WORKER, newWorker.UnitType, "Trained unit should be a WORKER");
			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(newWorker.GridPosition),
				"Cell occupied by the new worker should not be buildable");
		}

		// ── Boundary conditions ────────────────────────────────────────────────

		/// <summary>
		/// At GAME_SPEED=30 (max): CREATION_TIME[WORKER] ≈ 0.067 s
		/// </summary>
		[UnityTest]
		public IEnumerator TrainAtMaxSpeed_CompletesNearlyInstantly()
		{
			int originalSpeed = Constants.GAME_SPEED;
			Constants.GAME_SPEED = 30;
			Constants.CalculateGameConstants();

			try
			{
				Unit baseUnit = PlaceBuiltBase(new Vector3Int(10, 10, 0));
				int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

				baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

				yield return WaitUntil(() =>
				{
					TickUnit(baseUnit);
					return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
				}, timeoutSeconds: 3f, failMessage: "Training at max speed did not complete quickly");

				Assert.AreEqual(unitsBefore + 1, ctx.UnitManager.GetAllUnits().Count);
			}
			finally
			{
				Constants.GAME_SPEED = originalSpeed;
				Constants.CalculateGameConstants();
			}
		}

		[UnityTest]
		public IEnumerator TrainedWorker_SpawnsOutsideBuildingFootprint()
		{
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Unit baseUnit = PlaceBuiltBase(basePos);
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));

			yield return WaitUntil(() =>
			{
				TickUnit(baseUnit);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 5f, failMessage: "Trained worker never appeared");

			Unit newWorker = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.WORKER)
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Vector3Int baseSize = Constants.UNIT_SIZE[UnitType.BASE];
			HashSet<Vector3Int> baseCells = new HashSet<Vector3Int>();
			for (int i = 0; i < baseSize.x; i++)
				for (int j = 0; j < baseSize.y; j++)
					baseCells.Add(basePos + new Vector3Int(i, -j, 0));

			Assert.IsFalse(baseCells.Contains(newWorker.GridPosition),
				$"Worker spawned at {newWorker.GridPosition} which is inside the base footprint");
		}
	}
}
