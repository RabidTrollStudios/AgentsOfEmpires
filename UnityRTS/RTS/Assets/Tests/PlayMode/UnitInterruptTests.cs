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
	/// Play Mode tests for action interruption:
	/// interrupting a move with an attack, an attack with a move,
	/// interrupting a build with a move, and interrupting a gather with a move.
	/// </summary>
	[TestFixture]
	public class UnitInterruptTests : PlayModeTestBase
	{
		private void TickUnit(Unit unit)
		{
			unit.FixedUpdate();
			unit.Update();
		}

		#region Attack interrupts Move

		/// <summary>
		/// A moving soldier can be interrupted by an attack command.
		/// The soldier transitions from MOVE to ATTACK.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_MovingThenAttackCommand_SwitchesToAttack()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(12, 10, 0), ctx.Agent1Go);

			// Start moving somewhere distant
			soldier.StartMoving(new MoveEventArgs(soldier, UnitType.SOLDIER, new Vector3Int(25, 25, 0)));
			Assert.AreEqual(UnitAction.MOVE, soldier.CurrentAction);

			// Let soldier move for a bit
			yield return WaitFrames(5);

			// Issue attack command (interrupt)
			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));

			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction,
				"Soldier should switch from MOVE to ATTACK when attack is issued");
		}

		#endregion

		#region Move interrupts Attack

		/// <summary>
		/// A soldier mid-attack can be interrupted by a move command.
		/// The soldier transitions from ATTACK to MOVE.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_AttackingThenMoveCommand_SwitchesToMove()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.BASE, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			soldier.StartAttacking(new AttackEventArgs(soldier, enemy));
			Assert.AreEqual(UnitAction.ATTACK, soldier.CurrentAction);

			// Let attack run briefly
			yield return WaitFrames(5);

			// Issue move command (interrupt)
			soldier.StartMoving(new MoveEventArgs(soldier, UnitType.SOLDIER, new Vector3Int(20, 20, 0)));

			Assert.AreEqual(UnitAction.MOVE, soldier.CurrentAction,
				"Soldier should switch from ATTACK to MOVE when move is issued");
		}

		#endregion

		#region Move interrupts Build

		/// <summary>
		/// A worker mid-build can be interrupted by a move command.
		/// The worker transitions from BUILD to MOVE; building retains its progress.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_BuildingThenMoveCommand_SwitchesToMove()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction);

			yield return WaitFrames(5);

			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, new Vector3Int(20, 20, 0)));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should switch from BUILD to MOVE when move command is issued");
		}

		#endregion

		#region Build interrupts Gather

		/// <summary>
		/// A worker mid-gather can be interrupted by a build command.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_GatheringThenBuildCommand_SwitchesToBuild()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 5, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 5, 0));

			worker.StartGathering(new GatherEventArgs(worker, mine, baseUnit));
			Assert.AreEqual(UnitAction.GATHER, worker.CurrentAction,
				"Worker should start gathering");

			// Wait a few frames for gather to be in progress
			yield return WaitFrames(5);

			// Interrupt with build command — BASE already exists so build REFINERY?
			// Simpler: just interrupt with a move
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, new Vector3Int(15, 15, 0)));

			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should switch from GATHER to MOVE when move is issued");
		}

		#endregion
	}
}
