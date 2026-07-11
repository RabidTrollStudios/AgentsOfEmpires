using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for attack pursuit (U3). The engine does NOT retarget:
	/// a unit pursues only the target its agent assigned. When that target
	/// becomes unreachable, the unit goes IDLE (pursuit failed) so the agent
	/// can decide what to do next — it never auto-switches to another enemy.
	/// (Previously the engine retargeted via FindClosestReachableEnemy; that
	/// behavior was removed — target selection belongs to the PlanningAgent.)
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

		#region No engine retargeting

		/// <summary>
		/// When a warrior's path to its assigned target becomes blocked, the engine
		/// does NOT switch it to a closer reachable enemy — the unit keeps the target
		/// its agent assigned (and ultimately goes IDLE when it can't reach it).
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_PathBlocked_DoesNotRetargetToOtherEnemy()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit unreachableEnemy = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			Unit otherEnemy = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 5, 0), ctx.Agent1Go);
			yield return null;

			// Assign the unreachable enemy as the target.
			warrior.StartAttacking(new AttackEventArgs(warrior, unreachableEnemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);
			int assignedNbr = unreachableEnemy.UnitNbr;

			// Wall off the assigned target so pursuit fails.
			WallOff(20, 20);

			// Tick — the warrior must NEVER switch to otherEnemy.
			for (int i = 0; i < 200; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;

				if (warrior.CurrentAction == UnitAction.ATTACK)
					Assert.AreNotEqual(otherEnemy.UnitNbr, warrior.AttackTargetNbr,
						"Engine must not retarget to a different enemy");
				else
					break; // went IDLE — expected once the target is unreachable
			}
		}

		/// <summary>
		/// When the assigned target is unreachable, the warrior gives up pursuit
		/// and goes IDLE (so the agent can re-decide), rather than staying stuck.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_UnreachableTarget_GoesIdle()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Wall off the enemy so no path to it exists.
			WallOff(20, 20);

			bool wentIdle = false;
			for (int i = 0; i < 200; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
				if (warrior.CurrentAction == UnitAction.IDLE) { wentIdle = true; break; }
			}

			Assert.IsTrue(wentIdle,
				"Warrior should go IDLE when its assigned target is unreachable");
		}

		#endregion
	}
}
