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
			unit.TickFixedUpdate();
			unit.Update();
		}

		#region Attack interrupts Move

		/// <summary>
		/// A moving warrior can be interrupted by an attack command.
		/// The warrior transitions from MOVE to ATTACK.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_MovingThenAttackCommand_SwitchesToAttack()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0), ctx.Agent1Go);

			// Start moving somewhere distant
			warrior.StartMoving(new MoveEventArgs(warrior, UnitType.WARRIOR, new Vector3Int(25, 25, 0)));
			Assert.AreEqual(UnitAction.MOVE, warrior.CurrentAction);

			// Let warrior move for a bit
			yield return WaitFrames(5);

			// Issue attack command (interrupt)
			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));

			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction,
				"Warrior should switch from MOVE to ATTACK when attack is issued");
		}

		#endregion

		#region Move interrupts Attack

		/// <summary>
		/// A warrior mid-attack can be interrupted by a move command.
		/// The warrior transitions from ATTACK to MOVE.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_AttackingThenMoveCommand_SwitchesToMove()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.BASE, new Vector3Int(11, 10, 0), ctx.Agent1Go);

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Let attack run briefly
			yield return WaitFrames(5);

			// Issue move command (interrupt)
			warrior.StartMoving(new MoveEventArgs(warrior, UnitType.WARRIOR, new Vector3Int(20, 20, 0)));

			Assert.AreEqual(UnitAction.MOVE, warrior.CurrentAction,
				"Warrior should switch from ATTACK to MOVE when move is issued");
		}

		#endregion

		#region Move interrupts Build

		/// <summary>
		/// A pawn mid-build can be interrupted by a move command.
		/// The pawn transitions from BUILD to MOVE; building retains its progress.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_BuildingThenMoveCommand_SwitchesToMove()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction);

			yield return WaitFrames(5);

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(20, 20, 0)));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction,
				"Pawn should switch from BUILD to MOVE when move command is issued");
		}

		#endregion

		#region Build interrupts Gather

		/// <summary>
		/// A pawn mid-gather can be interrupted by a build command.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_GatheringThenBuildCommand_SwitchesToBuild()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction,
				"Pawn should start gathering");

			// Wait a few frames for gather to be in progress
			yield return WaitFrames(5);

			// Interrupt with a move command
			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(15, 15, 0)));

			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction,
				"Pawn should switch from GATHER to MOVE when move is issued");
		}

		#endregion
	}
}
