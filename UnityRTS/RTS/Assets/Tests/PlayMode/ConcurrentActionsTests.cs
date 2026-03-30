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
			unit.TickFixedUpdate();
			unit.Update();
		}

		#region Happy Path

		/// <summary>
		/// A pawn builds while a warrior attacks an enemy simultaneously.
		/// Both complete successfully.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_BuildsWhileWarrior_Attacks_BothComplete()
		{
			// Pawn builds a base
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction);

			// Warrior attacks an enemy
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0), ctx.Agent1Go);
			enemy.Health = 1f;
			int enemyNbr = enemy.UnitNbr;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Wait for both to complete
			bool warriorDone = false;
			bool pawnDone = false;

			Unit building = null;
			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				if (!warriorDone && ctx.UnitManager.GetUnit(enemyNbr) == null)
					warriorDone = true;
				if (!pawnDone)
				{
					building = ctx.UnitManager.GetAllUnits().Values
						.Select(go => go.GetComponent<Unit>())
						.FirstOrDefault(u => u.UnitType == UnitType.BASE);
					if (building != null && building.IsBuilt)
						pawnDone = true;
				}
				return warriorDone && pawnDone;
			}, timeoutSeconds: 20f, failMessage: "Not both actions completed (build + attack)");

			Assert.IsTrue(warriorDone, "Warrior should have killed the enemy");
			Assert.IsTrue(pawnDone, "Pawn should have finished building");
		}

		/// <summary>
		/// Two pawns building different structures simultaneously both complete.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoPawns_BuildDifferentStructures_BothComplete()
		{
			// Pawn 0 builds a base at (10,10)
			Unit pawn0 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			// Pawn 1 builds a base at (10,18)
			Unit pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 18, 0));

			Agent agent = GetAgent0();
			// Ensure enough gold for two bases (BASE cost * 2)
			agent.Gold = (int)(Constants.COST[UnitType.BASE] * 2 + 100);

			pawn0.StartBuilding(new BuildEventArgs(pawn0, new Vector3Int(10, 10, 0), UnitType.BASE));
			pawn1.StartBuilding(new BuildEventArgs(pawn1, new Vector3Int(10, 18, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, pawn0.CurrentAction);
			Assert.AreEqual(UnitAction.BUILD, pawn1.CurrentAction);

			// Wait for both pawns to go IDLE (build complete)
			yield return WaitUntil(() =>
			{
				TickUnit(pawn0);
				TickUnit(pawn1);
				return pawn0.CurrentAction == UnitAction.IDLE
					&& pawn1.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 20f, failMessage: "Both pawns did not complete their builds");

			// Count bases
			int baseCount = ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.Count(u => u.UnitType == UnitType.BASE && u.IsBuilt);

			Assert.AreEqual(2, baseCount,
				"Two completed BASEs should exist after both pawns finish");
		}

		/// <summary>
		/// A pawn gathers while a warrior moves simultaneously.
		/// Both complete their tasks independently.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_GathersWhileWarrior_Moves_BothComplete()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));
			warrior.StartMoving(new MoveEventArgs(warrior, UnitType.WARRIOR, new Vector3Int(20, 20, 0)));

			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);
			Assert.AreEqual(UnitAction.MOVE, warrior.CurrentAction);

			// Wait for warrior to arrive AND at least one gold deposit
			yield return WaitUntil(
				() => warrior.CurrentAction == UnitAction.IDLE && agent.Gold > initialGold,
				timeoutSeconds: 15f,
				failMessage: "Warrior did not finish moving or pawn did not deposit gold");

			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction,
				"Warrior should have arrived at target");
			Assert.Greater(agent.Gold, initialGold,
				"Pawn should have deposited gold while warrior was moving");
		}

		#endregion
	}
}
