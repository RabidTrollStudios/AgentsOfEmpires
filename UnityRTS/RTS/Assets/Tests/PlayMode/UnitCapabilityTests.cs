using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests that verify unit capability constraints at runtime:
	/// non-combatants cannot attack, non-builders cannot build,
	/// immobile units stay put, and buildings cannot move or gather.
	/// </summary>
	[TestFixture]
	public class UnitCapabilityTests : PlayModeTestBase
	{
		#region Attack Capability

		/// <summary>
		/// BASE is not a combatant; an attack command should be ignored.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_CannotAttack_CommandIgnored()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			baseUnit.IsBuilt = true;
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float healthBefore = enemy.Health;

			baseUnit.StartAttacking(new AttackEventArgs(baseUnit, enemy));

			Assert.AreNotEqual(UnitAction.ATTACK, baseUnit.CurrentAction,
				"BASE should not be able to attack");
			Assert.AreEqual(healthBefore, enemy.Health,
				"Enemy health should not change when BASE tries to attack");

			yield return null;
		}

		/// <summary>
		/// BARRACKS cannot attack.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_CannotAttack_CommandIgnored()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));
			barracks.IsBuilt = true;
			Unit enemy = PlaceUnit(UnitType.WORKER, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float healthBefore = enemy.Health;

			barracks.StartAttacking(new AttackEventArgs(barracks, enemy));

			Assert.AreNotEqual(UnitAction.ATTACK, barracks.CurrentAction,
				"BARRACKS should not be able to attack");
			Assert.AreEqual(healthBefore, enemy.Health);

			yield return null;
		}

		#endregion

		#region Build Capability

		/// <summary>
		/// SOLDIER cannot build — build command is rejected, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_CannotBuild_CommandRejected()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			soldier.StartBuilding(new BuildEventArgs(soldier, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, soldier.CurrentAction,
				"SOLDIER should not be able to build");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when soldier tries to build");

			yield return null;
		}

		/// <summary>
		/// ARCHER cannot build — build command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_CannotBuild_CommandRejected()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			archer.StartBuilding(new BuildEventArgs(archer, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, archer.CurrentAction,
				"ARCHER should not be able to build");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		#endregion

		#region Train Capability

		/// <summary>
		/// WORKER cannot train units.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_CannotTrain_CommandRejected()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			worker.StartTraining(new TrainEventArgs(worker, UnitType.WORKER));

			Assert.AreNotEqual(UnitAction.TRAIN, worker.CurrentAction,
				"WORKER should not be able to train units");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		/// <summary>
		/// SOLDIER cannot train units.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_CannotTrain_CommandRejected()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			soldier.StartTraining(new TrainEventArgs(soldier, UnitType.SOLDIER));

			Assert.AreNotEqual(UnitAction.TRAIN, soldier.CurrentAction,
				"SOLDIER should not be able to train units");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		#endregion

		#region Gather Capability

		/// <summary>
		/// SOLDIER cannot gather resources.
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_CannotGather_CommandRejected()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(10, 10, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 10, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;

			soldier.StartGathering(new GatherEventArgs(soldier, mine, baseUnit));

			Assert.AreNotEqual(UnitAction.GATHER, soldier.CurrentAction,
				"SOLDIER should not be able to gather resources");

			yield return null;
		}

		/// <summary>
		/// ARCHER cannot gather resources.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_CannotGather_CommandRejected()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 10, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;

			archer.StartGathering(new GatherEventArgs(archer, mine, baseUnit));

			Assert.AreNotEqual(UnitAction.GATHER, archer.CurrentAction,
				"ARCHER should not be able to gather resources");

			yield return null;
		}

		#endregion

		#region Move Capability

		/// <summary>
		/// BASE is immobile — move command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_CannotMove_CommandRejected()
		{
			var basePos = new Vector3Int(10, 10, 0);
			Unit baseUnit = PlaceUnit(UnitType.BASE, basePos);

			baseUnit.StartMoving(new MoveEventArgs(baseUnit, UnitType.BASE, new Vector3Int(15, 15, 0)));

			Assert.AreNotEqual(UnitAction.MOVE, baseUnit.CurrentAction,
				"BASE should not be able to move");
			Assert.AreEqual(basePos, baseUnit.GridPosition,
				"BASE should remain at its original position");

			yield return null;
		}

		/// <summary>
		/// MINE is immobile — move command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Mine_CannotMove_CommandRejected()
		{
			var minePos = new Vector3Int(10, 10, 0);
			Unit mine = PlaceUnit(UnitType.MINE, minePos);

			mine.StartMoving(new MoveEventArgs(mine, UnitType.MINE, new Vector3Int(15, 15, 0)));

			Assert.AreNotEqual(UnitAction.MOVE, mine.CurrentAction,
				"MINE should not be able to move");

			yield return null;
		}

		#endregion
	}
}
