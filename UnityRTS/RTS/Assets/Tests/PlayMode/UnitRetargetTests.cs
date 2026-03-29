using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for FindClosestReachableEnemy — the attack retargeting
	/// logic that fires when a unit can't path to its assigned target.
	/// Neighbors must be made non-walkable AFTER StartAttacking succeeds,
	/// because UpdatePath(avoidUnits=false) ignores mobile units.
	/// </summary>
	[TestFixture]
	public class UnitRetargetTests : PlayModeTestBase
	{
		/// <summary>
		/// Make the 8 neighbor cells of a position non-walkable.
		/// </summary>
		private void WallOff(int cx, int cy)
		{
			for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++)
					if (dx != 0 || dy != 0)
					{
						ctx.MapManager.GridCells[cx + dx, cy + dy].SetWalkable(false);
						ctx.MapManager.Grid.SetCellBlocked(cx + dx, cy + dy);
					}
		}

		#region FindClosestReachableEnemy

		/// <summary>
		/// When a warrior's path to its attack target is blocked repeatedly,
		/// it retargets to the closest reachable enemy via FindClosestReachableEnemy.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_PathBlocked_RetargetsToReachableEnemy()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit unreachableEnemy = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			Unit reachableEnemy = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 5, 0), ctx.Agent1Go);
			yield return null;

			// Start attacking — neighbors are walkable so initial path succeeds
			warrior.StartAttacking(new AttackEventArgs(warrior, unreachableEnemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Now wall off the unreachable enemy so subsequent re-paths fail
			WallOff(20, 20);

			// Tick — after pathFailCount >= 3, FindClosestReachableEnemy fires
			bool retargeted = false;
			for (int i = 0; i < 300; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;

				if (warrior.AttackUnit == reachableEnemy)
				{
					retargeted = true;
					break;
				}
			}

			Assert.IsTrue(retargeted,
				"Warrior should retarget to reachable enemy when path to original target is blocked");
		}

		/// <summary>
		/// When all enemies are unreachable, the attacker stays in ATTACK state
		/// and keeps retrying (pathFailCount resets).
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AllEnemiesUnreachable_StaysInAttack()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			yield return null;

			// Start attacking — neighbors walkable so initial path succeeds
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Now wall off the enemy so subsequent re-paths fail
			WallOff(20, 20);

			// Tick many frames — warrior should stay in ATTACK (doesn't go IDLE)
			// because attack retargeting resets pathFailCount when no alt found
			for (int i = 0; i < 200; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
			}

			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction,
				"Warrior should stay in ATTACK state when all enemies unreachable (retries)");
		}

		/// <summary>
		/// FindClosestReachableEnemy skips mines and friendly units.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_Retarget_SkipsMinesAndFriendlies()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit unreachableEnemy = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);

			// Place a mine (should be skipped) and a friendly pawn (should be skipped)
			PlaceUnit(UnitType.MINE, new Vector3Int(8, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0)); // friendly — same agent

			// Place a reachable enemy further away
			Unit reachableEnemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 5, 0), ctx.Agent1Go);
			yield return null;

			// Start attacking — initial path succeeds
			warrior.StartAttacking(new AttackEventArgs(warrior, unreachableEnemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Now wall off the unreachable enemy
			WallOff(20, 20);

			bool retargeted = false;
			for (int i = 0; i < 300; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;

				if (warrior.AttackUnit == reachableEnemy)
				{
					retargeted = true;
					break;
				}
			}

			Assert.IsTrue(retargeted,
				"Warrior should retarget to enemy warrior, skipping mines and friendly units");
		}

		#endregion
	}
}
