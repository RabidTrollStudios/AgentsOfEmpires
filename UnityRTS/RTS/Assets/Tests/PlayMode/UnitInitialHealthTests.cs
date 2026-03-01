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
		/// Freshly placed WORKER has health equal to Constants.HEALTH[WORKER].
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_SpawnsWithFullHealth()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.WORKER], worker.Health, 0.1f,
				"WORKER should spawn with HEALTH[WORKER] health");
			yield return null;
		}

		/// <summary>
		/// Freshly placed SOLDIER has health equal to Constants.HEALTH[SOLDIER].
		/// </summary>
		[UnityTest]
		public IEnumerator Soldier_SpawnsWithFullHealth()
		{
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));

			Assert.AreEqual(Constants.HEALTH[UnitType.SOLDIER], soldier.Health, 0.1f,
				"SOLDIER should spawn with HEALTH[SOLDIER] health");
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

		#endregion

		#region Health Is Positive

		/// <summary>
		/// All placed units have positive health on spawn.
		/// </summary>
		[UnityTest]
		public IEnumerator AllUnitTypes_SpawnWithPositiveHealth()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(3, 3, 0));
			Unit soldier = PlaceUnit(UnitType.SOLDIER, new Vector3Int(4, 3, 0));
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 3, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(8, 8, 0));
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(12, 8, 0));

			Assert.Greater(worker.Health, 0f, "WORKER health should be positive");
			Assert.Greater(soldier.Health, 0f, "SOLDIER health should be positive");
			Assert.Greater(archer.Health, 0f, "ARCHER health should be positive");
			Assert.Greater(mine.Health, 0f, "MINE health should be positive");
			Assert.Greater(baseUnit.Health, 0f, "BASE health should be positive");
			Assert.Greater(barracks.Health, 0f, "BARRACKS health should be positive");

			yield return null;
		}

		#endregion

		#region Health Matches Constants

		/// <summary>
		/// BASE health matches HEALTH[BASE], which is larger than WORKER's health.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_HasMoreHealthThanWorker()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			Assert.Greater(baseUnit.Health, worker.Health,
				"BASE should have more health than WORKER");
			yield return null;
		}

		#endregion
	}
}
