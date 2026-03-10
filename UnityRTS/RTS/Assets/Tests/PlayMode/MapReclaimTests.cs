using System.Collections;
using System.Collections.Generic;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for map reclamation after unit destruction:
	/// cells blocked by buildings and mobile units are freed when those
	/// units are destroyed via combat or health-zero.
	/// </summary>
	[TestFixture]
	public class MapReclaimTests : PlayModeTestBase
	{
		#region Building Destruction

		/// <summary>
		/// When a 3x3 BASE is destroyed (health set to 0), all 9 footprint cells
		/// become buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_Destroyed_All9CellsReclaimed()
		{
			var basePos = new Vector3Int(10, 10, 0);
			Unit building = PlaceUnit(UnitType.BASE, basePos);

			var size = Constants.UNIT_SIZE[UnitType.BASE];
			var footprint = new List<Vector3Int>();
			for (int i = 0; i < size.x; i++)
				for (int j = 0; j < size.y; j++)
					footprint.Add(basePos + new Vector3Int(i, -j, 0));

			// All footprint cells should be blocked
			foreach (var cell in footprint)
				Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(cell),
					$"Cell {cell} should be blocked while BASE is alive");

			building.Health = 0;
			building.Update();
			yield return null;

			foreach (var cell in footprint)
				Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(cell),
					$"Cell {cell} should be buildable after BASE is destroyed");
		}

		/// <summary>
		/// When a BARRACKS (3x3) is destroyed via combat (warrior attacks it),
		/// all its footprint cells are freed.
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_DestroyedByCombat_CellsReclaimed()
		{
			var barracksPos = new Vector3Int(15, 10, 0);
			Unit barracks = PlaceUnit(UnitType.BARRACKS, barracksPos, ctx.Agent1Go);

			var size = Constants.UNIT_SIZE[UnitType.BARRACKS];
			var footprint = new List<Vector3Int>();
			for (int i = 0; i < size.x; i++)
				for (int j = 0; j < size.y; j++)
					footprint.Add(barracksPos + new Vector3Int(i, -j, 0));

			int barracksNbr = barracks.UnitNbr;

			// Three warriors attack the barracks
			Unit s1 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 10, 0));
			Unit s2 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 9, 0));
			Unit s3 = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 11, 0));

			s1.StartAttacking(new AttackEventArgs(s1, barracks));
			s2.StartAttacking(new AttackEventArgs(s2, barracks));
			s3.StartAttacking(new AttackEventArgs(s3, barracks));

			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(barracksNbr) == null,
				timeoutSeconds: 30f,
				failMessage: "Barracks was not destroyed by warriors");

			yield return null; // let Destroy process

			foreach (var cell in footprint)
				Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(cell),
					$"Cell {cell} should be buildable after BARRACKS is destroyed");
		}

		/// <summary>
		/// After a building is destroyed, the previously blocked area can be
		/// used for a new building.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_Destroyed_AreaUsableForRebuild()
		{
			var basePos = new Vector3Int(10, 10, 0);
			Unit building = PlaceUnit(UnitType.BASE, basePos);

			building.Health = 0;
			building.Update();
			yield return null;

			// Verify buildability is restored
			Assert.IsTrue(ctx.MapManager.IsAreaBuildable(UnitType.BASE, basePos),
				"Area should be buildable after original BASE is destroyed");
		}

		#endregion

		#region Mobile Unit Destruction

		/// <summary>
		/// When a PAWN is destroyed, its 1x1 cell becomes buildable.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Destroyed_CellBecomesBuildable()
		{
			var pos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pos);

			Assert.IsFalse(ctx.MapManager.IsGridPositionBuildable(pos),
				"Pawn's cell should not be buildable while alive");

			pawn.Health = 0;
			pawn.Update();
			yield return null;

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(pos),
				"Pawn's cell should be buildable after pawn dies");
		}

		/// <summary>
		/// When a WARRIOR is destroyed in combat, its cell becomes buildable again.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_DestroyedByCombat_CellReclaimed()
		{
			var warriorPos = new Vector3Int(10, 10, 0);
			Unit friendlyWarrior = PlaceUnit(UnitType.WARRIOR, warriorPos, ctx.Agent1Go);
			int warriorNbr = friendlyWarrior.UnitNbr;
			friendlyWarrior.Health = 1f;

			Unit attacker = PlaceUnit(UnitType.WARRIOR, new Vector3Int(9, 10, 0));
			attacker.StartAttacking(new AttackEventArgs(attacker, friendlyWarrior));

			yield return WaitUntil(
				() => ctx.UnitManager.GetUnit(warriorNbr) == null,
				timeoutSeconds: 10f,
				failMessage: "Warrior was not destroyed by attacker");

			yield return null;

			Assert.IsTrue(ctx.MapManager.IsGridPositionBuildable(warriorPos),
				"Warrior's cell should be buildable after death");
		}

		#endregion

		#region Walkability Reclamation

		/// <summary>
		/// A BASE's footprint cells are not walkable while the building stands.
		/// After destruction, they become walkable for pathfinding.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_Destroyed_FootprintBecomesWalkable()
		{
			var basePos = new Vector3Int(10, 10, 0);
			Unit building = PlaceUnit(UnitType.BASE, basePos);

			var size = Constants.UNIT_SIZE[UnitType.BASE];
			// Check center cell (not walkable while BASE is alive)
			var centerCell = basePos + new Vector3Int(1, -1, 0);
			Assert.IsFalse(ctx.MapManager.IsGridPositionWalkable(centerCell),
				"BASE footprint cell should not be walkable while building stands");

			building.Health = 0;
			building.Update();
			yield return null;

			Assert.IsTrue(ctx.MapManager.IsGridPositionWalkable(centerCell),
				"BASE footprint cell should be walkable after building is destroyed");
		}

		#endregion
	}
}
