using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests verifying that units spawn with their correct initial health
	/// as defined in Constants.HEALTH.
	/// </summary>
	[TestFixture]
	public class UnitInitialHealthTests : PlayModeTestBase
	{
		#region Mobile Units

		/// <summary>
		/// Freshly placed PAWN has health equal to Constants.HEALTH[PAWN].
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_SpawnsWithFullHealth()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.PAWN], pawn.Health, 0.1f,
				"PAWN should spawn with HEALTH[PAWN] health");
			yield return null;
		}

		/// <summary>
		/// Freshly placed WARRIOR has health equal to Constants.HEALTH[WARRIOR].
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_SpawnsWithFullHealth()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.WARRIOR], warrior.Health, 0.1f,
				"WARRIOR should spawn with HEALTH[WARRIOR] health");
			yield return null;
		}

		/// <summary>
		/// Freshly placed ARCHER has health equal to Constants.HEALTH[ARCHER].
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_SpawnsWithFullHealth()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 5, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.ARCHER], archer.Health, 0.1f,
				"ARCHER should spawn with HEALTH[ARCHER] health");
			yield return null;
		}

		/// <summary>
		/// Freshly placed LANCER has health equal to Constants.HEALTH[LANCER].
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_SpawnsWithFullHealth()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(5, 5, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.LANCER], lancer.Health, 0.1f,
				"LANCER should spawn with HEALTH[LANCER] health");
			yield return null;
		}

		#endregion

		#region Buildings

		/// <summary>
		/// Freshly placed BASE has health equal to Constants.HEALTH[BASE].
		/// </summary>
		[UnityTest]
		public IEnumerator Base_SpawnsWithFullHealth()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.BASE], baseUnit.Health, 0.1f,
				"BASE should spawn with HEALTH[BASE] health");
			yield return null;
		}

		/// <summary>
		/// Freshly placed BARRACKS has health equal to Constants.HEALTH[BARRACKS].
		/// </summary>
		[UnityTest]
		public IEnumerator Barracks_SpawnsWithFullHealth()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.BARRACKS], barracks.Health, 0.1f,
				"BARRACKS should spawn with HEALTH[BARRACKS] health");
			yield return null;
		}

		/// <summary>
		/// Freshly placed TOWER has health equal to Constants.HEALTH[TOWER].
		/// </summary>
		[UnityTest]
		public IEnumerator Tower_SpawnsWithFullHealth()
		{
			Unit tower = PlaceUnit(UnitType.TOWER, new Vector3Int(10, 10, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.TOWER], tower.Health, 0.1f,
				"TOWER should spawn with HEALTH[TOWER] health");
			yield return null;
		}

		#endregion

		#region Health Is Positive

		/// <summary>
		/// All placed units have positive health on spawn.
		/// </summary>
		[UnityTest]
		public IEnumerator AllUnitTypes_SpawnWithPositiveHealth()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(3, 3, 0));
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(4, 3, 0));
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 3, 0));
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(6, 3, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(8, 8, 0));
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(12, 8, 0));
			Unit tower = PlaceUnit(UnitType.TOWER, new Vector3Int(16, 8, 0));

			Assert.Greater(pawn.Health, 0f, "PAWN health should be positive");
			Assert.Greater(warrior.Health, 0f, "WARRIOR health should be positive");
			Assert.Greater(archer.Health, 0f, "ARCHER health should be positive");
			Assert.Greater(lancer.Health, 0f, "LANCER health should be positive");
			Assert.Greater(mine.Health, 0f, "MINE health should be positive");
			Assert.Greater(baseUnit.Health, 0f, "BASE health should be positive");
			Assert.Greater(barracks.Health, 0f, "BARRACKS health should be positive");
			Assert.Greater(tower.Health, 0f, "TOWER health should be positive");

			yield return null;
		}

		#endregion

		#region Health Matches Constants

		/// <summary>
		/// BASE health matches HEALTH[BASE], which is larger than PAWN's health.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_HasMoreHealthThanPawn()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			Assert.Greater(baseUnit.Health, pawn.Health,
				"BASE should have more health than PAWN");
			yield return null;
		}

		#endregion
	}
}
