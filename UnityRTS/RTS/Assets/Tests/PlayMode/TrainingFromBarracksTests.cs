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
	/// Play Mode tests for BARRACKS training behavior.
	/// Verifies that BARRACKS trains WARRIOR and ARCHER (not PAWN),
	/// that gold is deducted at train start, and that sequential training works.
	/// </summary>
	[TestFixture]
	public class TrainingFromBarracksTests : PlayModeTestBase
	{
		private void TickUnit(Unit unit)
		{
			unit.Update();
		}

		private Unit PlaceBuiltBarracks(Vector3Int position)
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, position);
			barracks.IsBuilt = true;
			return barracks;
		}

		#region Happy Path

		/// <summary>
		/// A built BARRACKS trains a WARRIOR. After training completes,
		/// a WARRIOR appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator BarracksTrainsWarrior_WarriorAppearsAfterTimer()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"Barracks should enter TRAIN state for WARRIOR");

			yield return WaitUntil(() =>
			{
				TickUnit(barracks);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 10f, failMessage: "WARRIOR never appeared after BARRACKS training");

			// Find the new unit
			Unit newUnit = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Assert.AreEqual(UnitType.WARRIOR, newUnit.UnitType,
				"BARRACKS should produce a WARRIOR");
		}

		/// <summary>
		/// A built BARRACKS trains an ARCHER. After training completes,
		/// an ARCHER appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator BarracksTrainsArcher_ArcherAppearsAfterTimer()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.ARCHER));
			Assert.AreEqual(UnitAction.TRAIN, barracks.CurrentAction,
				"Barracks should enter TRAIN state for ARCHER");

			yield return WaitUntil(() =>
			{
				TickUnit(barracks);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 10f, failMessage: "ARCHER never appeared after BARRACKS training");

			Unit newUnit = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Assert.AreEqual(UnitType.ARCHER, newUnit.UnitType,
				"BARRACKS should produce an ARCHER");
		}

		/// <summary>
		/// Gold is deducted immediately when BARRACKS starts training a WARRIOR.
		/// </summary>
		[UnityTest]
		public IEnumerator BarracksTrainsWarrior_GoldDeductedAtStart()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;
			int warriorCost = (int)Constants.COST[UnitType.WARRIOR];

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));

			Assert.AreEqual(goldBefore - warriorCost, agent.Gold,
				"Gold should be deducted immediately when BARRACKS starts training");

			yield return null;
		}

		/// <summary>
		/// BARRACKS goes IDLE after training completes.
		/// </summary>
		[UnityTest]
		public IEnumerator BarracksTrainsWarrior_GoesIdleAfterCompletion()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));

			yield return WaitUntil(() =>
			{
				TickUnit(barracks);
				return barracks.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "BARRACKS did not go IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"BARRACKS should be IDLE after training completes");
		}

		#endregion

		#region Error

		/// <summary>
		/// BARRACKS cannot train PAWN (only BASE can). Command is rejected, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_TrainsPawn_Rejected()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.PAWN));

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"BARRACKS should not be able to train PAWN");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted for rejected training");

			yield return null;
		}

		/// <summary>
		/// An unbuilt BARRACKS rejects training commands.
		/// </summary>
		[UnityTest]
		public IEnumerator UnbuiltBarracks_TrainCommand_Rejected()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			Assert.IsFalse(barracks.IsBuilt, "BARRACKS should start unbuilt");

			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"Unbuilt BARRACKS should not accept train commands");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted");

			yield return null;
		}

		/// <summary>
		/// BARRACKS training sequentially: train WARRIOR then ARCHER. Both appear on distinct cells.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_TrainWarriorThenArcher_BothOnDistinctCells()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));

			// Train WARRIOR
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			int countBefore = ctx.UnitManager.GetAllUnits().Count;

			yield return WaitUntil(() =>
			{
				TickUnit(barracks);
				return barracks.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "WARRIOR training did not complete");

			int countAfterWarrior = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(countBefore + 1, countAfterWarrior,
				"One WARRIOR should have been created");

			// Train ARCHER
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.ARCHER));

			yield return WaitUntil(() =>
			{
				TickUnit(barracks);
				return barracks.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "ARCHER training did not complete");

			int countAfterArcher = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(countBefore + 2, countAfterArcher,
				"One ARCHER should have been created after WARRIOR");

			// Both on distinct cells
			var warrior = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.WARRIOR);
			var archer = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u.UnitType == UnitType.ARCHER);

			Assert.IsNotNull(warrior);
			Assert.IsNotNull(archer);
			Assert.AreNotEqual(warrior.GridPosition, archer.GridPosition,
				"WARRIOR and ARCHER should occupy distinct grid cells");
		}

		#endregion
	}
}
