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
	/// Verifies that BARRACKS trains WARRIOR (not PAWN or ARCHER),
	/// that gold is deducted at train start, and that sequential training works.
	/// ARCHERY trains ARCHER (tested separately).
	/// </summary>
	[TestFixture]
	public class TrainingFromBarracksTests : PlayModeTestBase
	{
		private void StepUnit(Unit unit)
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
				StepUnit(barracks);
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
		/// A built ARCHERY trains an ARCHER. After training completes,
		/// an ARCHER appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator ArcheryTrainsArcher_ArcherAppearsAfterTimer()
		{
			Unit archery = PlaceUnit(UnitType.ARCHERY, new Vector3Int(10, 10, 0));
			archery.IsBuilt = true;
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			archery.StartTraining(new TrainEventArgs(archery, UnitType.ARCHER));
			Assert.AreEqual(UnitAction.TRAIN, archery.CurrentAction,
				"ARCHERY should enter TRAIN state for ARCHER");

			yield return WaitUntil(() =>
			{
				StepUnit(archery);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 10f, failMessage: "ARCHER never appeared after ARCHERY training");

			Unit newUnit = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Assert.AreEqual(UnitType.ARCHER, newUnit.UnitType,
				"ARCHERY should produce an ARCHER");
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
				StepUnit(barracks);
				return barracks.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "BARRACKS did not go IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"BARRACKS should be IDLE after training completes");
		}

		#endregion

		#region Error

		/// <summary>
		/// BARRACKS cannot train ARCHER (ARCHERY does). Command is rejected, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_TrainsArcher_Rejected()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.ARCHER));

			Assert.AreEqual(UnitAction.IDLE, barracks.CurrentAction,
				"BARRACKS should not be able to train ARCHER");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted for rejected training");

			yield return null;
		}

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
		/// BARRACKS trains two WARRIORs sequentially. Both appear on distinct cells.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_TrainTwoWarriors_BothOnDistinctCells()
		{
			Unit barracks = PlaceBuiltBarracks(new Vector3Int(10, 10, 0));

			// Train first WARRIOR
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));
			int countBefore = ctx.UnitManager.GetAllUnits().Count;

			yield return WaitUntil(() =>
			{
				StepUnit(barracks);
				return barracks.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "First WARRIOR training did not complete");

			int countAfterFirst = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(countBefore + 1, countAfterFirst,
				"One WARRIOR should have been created");

			// Train second WARRIOR
			barracks.StartTraining(new TrainEventArgs(barracks, UnitType.WARRIOR));

			yield return WaitUntil(() =>
			{
				StepUnit(barracks);
				return barracks.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Second WARRIOR training did not complete");

			int countAfterSecond = ctx.UnitManager.GetAllUnits().Count;
			Assert.AreEqual(countBefore + 2, countAfterSecond,
				"Two WARRIORs should have been created");

			// Both on distinct cells
			var warriors = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Where(u => u.UnitType == UnitType.WARRIOR)
				.ToList();

			Assert.AreEqual(2, warriors.Count, "Should have exactly 2 WARRIORs");
			Assert.AreNotEqual(warriors[0].GridPosition, warriors[1].GridPosition,
				"WARRIORs should occupy distinct grid cells");
		}

		#endregion
	}
}
