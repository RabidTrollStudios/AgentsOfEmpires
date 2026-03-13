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
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
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
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float healthBefore = enemy.Health;

			barracks.StartAttacking(new AttackEventArgs(barracks, enemy));

			Assert.AreNotEqual(UnitAction.ATTACK, barracks.CurrentAction,
				"BARRACKS should not be able to attack");
			Assert.AreEqual(healthBefore, enemy.Health);

			yield return null;
		}

		/// <summary>
		/// TOWER is not a combatant; an attack command should be ignored.
		/// </summary>
		[UnityTest]
		public IEnumerator Tower_CannotAttack_CommandIgnored()
		{
			Unit tower = PlaceUnit(UnitType.TOWER, new Vector3Int(10, 10, 0));
			tower.IsBuilt = true;
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			float healthBefore = enemy.Health;

			tower.StartAttacking(new AttackEventArgs(tower, enemy));

			Assert.AreNotEqual(UnitAction.ATTACK, tower.CurrentAction,
				"TOWER should not be able to attack");
			Assert.AreEqual(healthBefore, enemy.Health,
				"Enemy health should not change when TOWER tries to attack");

			yield return null;
		}

		#endregion

		#region Build Capability

		/// <summary>
		/// WARRIOR cannot build — build command is rejected, gold unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_CannotBuild_CommandRejected()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			warrior.StartBuilding(new BuildEventArgs(warrior, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, warrior.CurrentAction,
				"WARRIOR should not be able to build");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when warrior tries to build");

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

		/// <summary>
		/// LANCER cannot build — build command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_CannotBuild_CommandRejected()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(9, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			lancer.StartBuilding(new BuildEventArgs(lancer, new Vector3Int(10, 10, 0), UnitType.BASE));

			Assert.AreNotEqual(UnitAction.BUILD, lancer.CurrentAction,
				"LANCER should not be able to build");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		#endregion

		#region Train Capability

		/// <summary>
		/// PAWN cannot train units.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_CannotTrain_CommandRejected()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			pawn.StartTraining(new TrainEventArgs(pawn, UnitType.PAWN));

			Assert.AreNotEqual(UnitAction.TRAIN, pawn.CurrentAction,
				"PAWN should not be able to train units");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		/// <summary>
		/// WARRIOR cannot train units.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_CannotTrain_CommandRejected()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			warrior.StartTraining(new TrainEventArgs(warrior, UnitType.WARRIOR));

			Assert.AreNotEqual(UnitAction.TRAIN, warrior.CurrentAction,
				"WARRIOR should not be able to train units");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		/// <summary>
		/// LANCER cannot train units.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_CannotTrain_CommandRejected()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			lancer.StartTraining(new TrainEventArgs(lancer, UnitType.LANCER));

			Assert.AreNotEqual(UnitAction.TRAIN, lancer.CurrentAction,
				"LANCER should not be able to train units");
			Assert.AreEqual(goldBefore, agent.Gold);

			yield return null;
		}

		#endregion

		#region Gather Capability

		/// <summary>
		/// WARRIOR cannot gather resources.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_CannotGather_CommandRejected()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 10, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;

			warrior.StartGathering(new GatherEventArgs(warrior, mine, baseUnit));

			Assert.AreNotEqual(UnitAction.GATHER, warrior.CurrentAction,
				"WARRIOR should not be able to gather resources");

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

		/// <summary>
		/// LANCER cannot gather resources.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_CannotGather_CommandRejected()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 10, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 10, 0));
			baseUnit.IsBuilt = true;

			lancer.StartGathering(new GatherEventArgs(lancer, mine, baseUnit));

			Assert.AreNotEqual(UnitAction.GATHER, lancer.CurrentAction,
				"LANCER should not be able to gather resources");

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

		/// <summary>
		/// TOWER is immobile — move command is rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator Tower_CannotMove_CommandRejected()
		{
			var towerPos = new Vector3Int(10, 10, 0);
			Unit tower = PlaceUnit(UnitType.TOWER, towerPos);

			tower.StartMoving(new MoveEventArgs(tower, UnitType.TOWER, new Vector3Int(15, 15, 0)));

			Assert.AreNotEqual(UnitAction.MOVE, tower.CurrentAction,
				"TOWER should not be able to move");
			Assert.AreEqual(towerPos, tower.GridPosition,
				"TOWER should remain at its original position");

			yield return null;
		}

		#endregion
	}
}
