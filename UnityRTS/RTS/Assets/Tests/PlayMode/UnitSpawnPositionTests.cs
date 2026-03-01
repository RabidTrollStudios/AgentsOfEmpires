using System.Collections;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that trained units spawn correctly:
	/// they appear in UnitManager and are owned by the training agent.
	/// </summary>
	[TestFixture]
	public class UnitSpawnPositionTests : PlayModeTestBase
	{
		#region SOLDIER Spawn from BARRACKS

		/// <summary>
		/// After BARRACKS finishes training a SOLDIER, the SOLDIER appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedSoldier_AppearsInUnitManager()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			// Count soldiers before training
			int soldiersBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER).Count;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"BARRACKS should enter TRAIN state");

			// Wait for training to complete (barracks returns to IDLE)
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BARRACKS did not complete SOLDIER training");

			int soldiersAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER).Count;
			Assert.Greater(soldiersAfter, soldiersBefore,
				"A new SOLDIER should appear in UnitManager after training completes");
		}

		/// <summary>
		/// The newly spawned SOLDIER is owned by agent 0 (same agent as the BARRACKS).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedSoldier_OwnedByCorrectAgent()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			int agent0Nbr = GetAgent0().AgentNbr;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));

			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BARRACKS did not complete training");

			// Find the new SOLDIER
			Unit newSoldier = BuildingTestHelper.FindNewestUnitOfType(ctx, UnitType.SOLDIER);
			Assert.IsNotNull(newSoldier, "A SOLDIER should exist after training");

			// Verify ownership via agent-filtered query
			var agent0Soldiers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER, agent0Nbr);
			Assert.IsTrue(agent0Soldiers.Contains(newSoldier.UnitNbr),
				"Newly trained SOLDIER should belong to agent 0");
		}

		#endregion

		#region WORKER Spawn from BASE

		/// <summary>
		/// After BASE finishes training a WORKER, the WORKER appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedWorker_AppearsInUnitManager()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			int workersBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER).Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.WORKER));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"BASE should enter TRAIN state");

			yield return WaitUntil(
				() => baseUnit.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BASE did not complete WORKER training");

			int workersAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER).Count;
			Assert.Greater(workersAfter, workersBefore,
				"A new WORKER should appear after BASE completes training");
		}

		#endregion

		#region ARCHER Spawn from BARRACKS

		/// <summary>
		/// BARRACKS can train an ARCHER; the ARCHER appears in UnitManager after training.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedArcher_AppearsInUnitManager()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			int archersBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.ARCHER).Count;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.ARCHER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction);

			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BARRACKS did not complete ARCHER training");

			int archersAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.ARCHER).Count;
			Assert.Greater(archersAfter, archersBefore,
				"A new ARCHER should appear in UnitManager after training");
		}

		#endregion

		#region Spawn Count

		/// <summary>
		/// Training two units sequentially produces two new units in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoSequentialTrains_ProduceTwoNewUnits()
		{
			Agent agent = GetAgent0();
			agent.Gold = (int)(Constants.COST[UnitType.SOLDIER] * 3);

			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			int soldiersBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER).Count;

			// First training
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "First training did not complete");

			// Second training
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.SOLDIER));
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Second training did not complete");

			int soldiersAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER).Count;
			Assert.AreEqual(soldiersBefore + 2, soldiersAfter,
				"Two sequential trainings should produce exactly 2 new SOLDIERs");
		}

		#endregion
	}
}
