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
	/// Play Mode tests for concurrent unit actions:
	/// multiple units performing different tasks simultaneously without
	/// interfering with each other.
	/// </summary>
	[TestFixture]
	public class ConcurrentActionsTests : PlayModeTestBase
	{
		private void TickUnit(Unit unit)
		{
			unit.FixedUpdate();
			unit.Update();
		}

		#region Happy Path

		/// <summary>
		/// A worker builds while a soldier attacks an enemy simultaneously.
		/// Both complete successfully.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_BuildsWhileSoldier_Attacks_BothComplete()
		{
			// Worker builds a base
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction);

			// Soldier attacks an enemy
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(6, 5, 0), ctx.Agent1Go);
			enemy.Health = 1f;
			int enemyNbr = enemy.UnitNbr;

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));
			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction);

			// Wait for both to complete
			bool soldierDone = false;
			bool workerDone = false;

			Unit building = null;
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				if (!soldierDone && ctx.UnitManager.GetUnit(enemyNbr) == null)
					soldierDone = true;
				if (!workerDone)
				{
					building = ctx.UnitManager.GetAllUnits().Values
						.Select(go => go.GetComponent<Unit>())
						.FirstOrDefault(u => u.UnitType == UnitType.BASE);
					if (building != null && building.IsBuilt)
						workerDone = true;
				}
				return soldierDone && workerDone;
			}, timeoutSeconds: 20f, failMessage: "Not both actions completed (build + attack)");

			Assert.IsTrue(soldierDone, "Soldier should have killed the enemy");
			Assert.IsTrue(workerDone, "Worker should have finished building");
		}

		/// <summary>
		/// Two workers building different structures simultaneously both complete.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoWorkers_BuildDifferentStructures_BothComplete()
		{
			// Worker 0 builds a base at (10,10)
			Unit worker0 = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			// Worker 1 builds a base at (10,18)
			Unit worker1 = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 18, 0));

			Agent agent = GetAgent0();
			// Ensure enough gold for two bases (BASE cost * 2)
			agent.Gold = (int)(Constants.COST[UnitType.BASE] * 2 + 100);

			worker0.StartBuilding(new BuildEventArgs(worker0, new Vector3Int(10, 10, 0), UnitType.BASE));
			worker1.StartBuilding(new BuildEventArgs(worker1, new Vector3Int(10, 18, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, worker0.CurrentAction);
			Assert.AreEqual(UnitAction.BUILD, worker1.CurrentAction);

			// Wait for both workers to go IDLE (build complete)
			yield return WaitUntil(() =>
			{
				TickUnit(worker0);
				TickUnit(worker1);
				return worker0.CurrentAction == UnitAction.IDLE
					&& worker1.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 20f, failMessage: "Both workers did not complete their builds");

			// Count bases
			int baseCount = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Count(u => u.UnitType == UnitType.BASE && u.IsBuilt);

			Assert.AreEqual(2, baseCount,
				"Two completed BASEs should exist after both workers finish");
		}

		/// <summary>
		/// A worker gathers while a soldier moves simultaneously.
		/// Both complete their tasks independently.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_GathersWhileSoldier_Moves_BothComplete()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));
			soldier.StartMoving(new MoveEventArgs(soldier, UnitType.SOLDIER, new Vector3Int(20, 20, 0)));

			Assert.AreEqual(UnitAction.GATHER, worker.CurrentAction);
			Assert.AreEqual(UnitAction.MOVE, soldier.CurrentAction);

			// Wait for soldier to arrive AND at least one gold deposit
			yield return WaitUntil(
				() => soldier.CurrentAction == UnitAction.IDLE && agent.Gold > initialGold,
				timeoutSeconds: 60f,
				failMessage: "Soldier did not finish moving or worker did not deposit gold");

			Assert.AreEqual(UnitAction.IDLE, soldier.CurrentAction,
				"Soldier should have arrived at target");
			Assert.Greater(agent.Gold, initialGold,
				"Worker should have deposited gold while soldier was moving");
		}

		#endregion
	}
}
