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
	/// Play Mode tests for TOWER training behavior.
	/// Verifies that TOWER trains LANCER (not other unit types),
	/// that gold is deducted at train start, and that training completes correctly.
	/// </summary>
	[TestFixture]
	public class TrainingFromTowerTests : PlayModeTestBase
	{
		private void TickUnit(Unit unit)
		{
			// Drive a real game tick (TickFixedUpdate → SimulateTick), not just visual
			// Update() — the inactive test GameManager GO never fires FixedUpdate.
			unit.TickFixedUpdate();
			unit.Update();
		}

		private Unit PlaceBuiltTower(Vector3Int position)
		{
			Unit tower = PlaceUnit(UnitType.TOWER, position);
			tower.IsBuilt = true;
			return tower;
		}

		#region Happy Path

		/// <summary>
		/// A built TOWER trains a LANCER. After training completes,
		/// a LANCER appears in UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator TowerTrainsLancer_LancerAppearsAfterTimer()
		{
			Unit tower = PlaceBuiltTower(new Vector3Int(10, 10, 0));
			int unitsBefore = ctx.UnitManager.GetAllUnits().Count;

			tower.StartTraining(new TrainEventArgs(tower, UnitType.LANCER));
			Assert.AreEqual(UnitAction.TRAIN, tower.CurrentAction,
				"Tower should enter TRAIN state for LANCER");

			yield return WaitUntil(() =>
			{
				TickUnit(tower);
				return ctx.UnitManager.GetAllUnits().Count > unitsBefore;
			}, timeoutSeconds: 10f, failMessage: "LANCER never appeared after TOWER training");

			Unit newUnit = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.OrderByDescending(u => u.UnitNbr)
				.First();

			Assert.AreEqual(UnitType.LANCER, newUnit.UnitType,
				"TOWER should produce a LANCER");
		}

		/// <summary>
		/// Gold is deducted immediately when TOWER starts training a LANCER.
		/// </summary>
		[UnityTest]
		public IEnumerator TowerTrainsLancer_GoldDeductedAtStart()
		{
			Unit tower = PlaceBuiltTower(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;
			int lancerCost = (int)Constants.COST[UnitType.LANCER];

			tower.StartTraining(new TrainEventArgs(tower, UnitType.LANCER));

			Assert.AreEqual(goldBefore - lancerCost, agent.Gold,
				"Gold should be deducted immediately when TOWER starts training");

			yield return null;
		}

		/// <summary>
		/// TOWER goes IDLE after training completes.
		/// </summary>
		[UnityTest]
		public IEnumerator TowerTrainsLancer_GoesIdleAfterCompletion()
		{
			Unit tower = PlaceBuiltTower(new Vector3Int(10, 10, 0));
			tower.StartTraining(new TrainEventArgs(tower, UnitType.LANCER));

			yield return WaitUntil(() =>
			{
				TickUnit(tower);
				return tower.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "TOWER did not go IDLE after training");

			Assert.AreEqual(UnitAction.IDLE, tower.CurrentAction,
				"TOWER should be IDLE after training completes");
		}

		#endregion

		#region Error

		/// <summary>
		/// TOWER cannot train PAWN. Command is rejected, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Tower_TrainsPawn_Rejected()
		{
			Unit tower = PlaceBuiltTower(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			tower.StartTraining(new TrainEventArgs(tower, UnitType.PAWN));

			Assert.AreEqual(UnitAction.IDLE, tower.CurrentAction,
				"TOWER should not be able to train PAWN");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted for rejected training");

			yield return null;
		}

		/// <summary>
		/// TOWER cannot train WARRIOR. Command is rejected, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Tower_TrainsWarrior_Rejected()
		{
			Unit tower = PlaceBuiltTower(new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			tower.StartTraining(new TrainEventArgs(tower, UnitType.WARRIOR));

			Assert.AreEqual(UnitAction.IDLE, tower.CurrentAction,
				"TOWER should not be able to train WARRIOR");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted for rejected training");

			yield return null;
		}

		/// <summary>
		/// An unbuilt TOWER rejects training commands.
		/// </summary>
		[UnityTest]
		public IEnumerator UnbuiltTower_TrainCommand_Rejected()
		{
			Unit tower = PlaceUnit(UnitType.TOWER, new Vector3Int(10, 10, 0));
			Assert.IsFalse(tower.IsBuilt, "TOWER should start unbuilt");

			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			tower.StartTraining(new TrainEventArgs(tower, UnitType.LANCER));

			Assert.AreEqual(UnitAction.IDLE, tower.CurrentAction,
				"Unbuilt TOWER should not accept train commands");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted");

			yield return null;
		}

		#endregion
	}
}
