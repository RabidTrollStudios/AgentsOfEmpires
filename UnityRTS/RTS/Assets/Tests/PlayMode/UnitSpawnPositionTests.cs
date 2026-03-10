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
		#region WARRIOR Spawn from BARRACKS

		/// <summary>
		/// After BARRACKS finishes training a WARRIOR, the WARRIOR appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedWarrior_AppearsInUnitManager()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			// Count warriors before training
			int warriorsBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR).Count;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"BARRACKS should enter TRAIN state");

			// Wait for training to complete (barracks returns to IDLE)
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BARRACKS did not complete WARRIOR training");

			int warriorsAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR).Count;
			Assert.Greater(warriorsAfter, warriorsBefore,
				"A new WARRIOR should appear in UnitManager after training completes");
		}

		/// <summary>
		/// The newly spawned WARRIOR is owned by agent 0 (same agent as the BARRACKS).
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedWarrior_OwnedByCorrectAgent()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			int agent0Nbr = GetAgent0().AgentNbr;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));

			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BARRACKS did not complete training");

			// Find the new WARRIOR
			Unit newWarrior = BuildingTestHelper.FindNewestUnitOfType(ctx, UnitType.WARRIOR);
			Assert.IsNotNull(newWarrior, "A WARRIOR should exist after training");

			// Verify ownership via agent-filtered query
			var agent0Warriors = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR, agent0Nbr);
			Assert.IsTrue(agent0Warriors.Contains(newWarrior.UnitNbr),
				"Newly trained WARRIOR should belong to agent 0");
		}

		#endregion

		#region PAWN Spawn from BASE

		/// <summary>
		/// After BASE finishes training a PAWN, the PAWN appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator TrainedPawn_AppearsInUnitManager()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;

			int pawnsBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN).Count;

			baseUnit.StartTraining(new TrainEventArgs(baseUnit, UnitType.PAWN));
			Assert.AreEqual(UnitAction.TRAIN, baseUnit.CurrentAction,
				"BASE should enter TRAIN state");

			yield return WaitUntil(
				() => baseUnit.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "BASE did not complete PAWN training");

			int pawnsAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN).Count;
			Assert.Greater(pawnsAfter, pawnsBefore,
				"A new PAWN should appear after BASE completes training");
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
			agent.Gold = (int)(Constants.COST[UnitType.WARRIOR] * 3);

			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;

			int warriorsBefore = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR).Count;

			// First training
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "First training did not complete");

			// Second training
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			yield return WaitUntil(
				() => barracks.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Second training did not complete");

			int warriorsAfter = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR).Count;
			Assert.AreEqual(warriorsBefore + 2, warriorsAfter,
				"Two sequential trainings should produce exactly 2 new WARRIORs");
		}

		#endregion
	}
}
