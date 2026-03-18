using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for visual effects: SpawnDeathDust, SpawnBuildingFire,
	/// RemoveOneBuildingFire, and UpdateBuildingFires lifecycle.
	/// Uses real animator controllers loaded from the project via VisualTestHelper.
	/// </summary>
	[TestFixture]
	public class UnitVisualEffectTests : PlayModeTestBase
	{
		#region SpawnDeathDust

		/// <summary>
		/// When a unit dies with Dust2AnimatorController injected,
		/// a "DeathDust" GameObject is spawned at the unit's position.
		/// </summary>
		[UnityTest]
		public IEnumerator Unit_Death_SpawnsDeathDust()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			// Kill the pawn — Unit.Update() calls SpawnDeathDust() when Health <= 0
			pawn.Health = 0;
			BuildingTestHelper.Tick(pawn);
			yield return null;

			// A "DeathDust" object should have been created
			var dust = GameObject.Find("DeathDust");
			Assert.IsNotNull(dust, "DeathDust GameObject should be spawned on unit death");

			// Dust has an Animator with the Dust2 controller
			var animator = dust.GetComponent<Animator>();
			Assert.IsNotNull(animator, "DeathDust should have an Animator component");
			Assert.IsNotNull(animator.runtimeAnimatorController,
				"DeathDust Animator should have a controller assigned");

			yield return null;
		}

		/// <summary>
		/// Without the Dust2AnimatorController, death does NOT spawn dust (null guard).
		/// </summary>
		[UnityTest]
		public IEnumerator Unit_Death_NoDustWithoutController()
		{
			// Don't inject visual assets — controller stays null
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			pawn.Health = 0;
			BuildingTestHelper.Tick(pawn);
			yield return null;

			var dust = GameObject.Find("DeathDust");
			Assert.IsNull(dust, "No DeathDust should spawn when Dust2AnimatorController is null");
		}

		#endregion

		#region SpawnBuildingFire

		/// <summary>
		/// When a built building's health drops significantly, fire GameObjects
		/// are spawned as children of the building via UpdateBuildingFires.
		/// </summary>
		[UnityTest]
		public IEnumerator Building_HealthDrops_SpawnsFires()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			yield return null;

			// LateUpdate triggers UpdateHealthBar → UpdateBuildingFires
			// Start at full health to set lastFireThreshold = 50
			baseUnit.LateUpdate();
			yield return null;

			// Drop health to 50% — should trigger ~25 fire spawns (50 down to 25)
			baseUnit.Health = maxHp * 0.5f;
			baseUnit.LateUpdate();
			yield return null;

			// Count "BuildingFire" children
			int fireCount = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire")
					fireCount++;
			}

			Assert.Greater(fireCount, 0,
				"Building should have fire children after health drops");
		}

		/// <summary>
		/// Without BuildingFireControllers, no fires spawn (null guard).
		/// </summary>
		[UnityTest]
		public IEnumerator Building_HealthDrops_NoFiresWithoutControllers()
		{
			// Don't inject visual assets
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			yield return null;

			baseUnit.LateUpdate();
			baseUnit.Health = maxHp * 0.5f;
			baseUnit.LateUpdate();
			yield return null;

			int fireCount = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire")
					fireCount++;
			}

			Assert.AreEqual(0, fireCount,
				"No fires should spawn without BuildingFireControllers");
		}

		#endregion

		#region RemoveOneBuildingFire

		/// <summary>
		/// When a damaged building's health increases (e.g., repair),
		/// fire children are removed via RemoveOneBuildingFire.
		/// </summary>
		[UnityTest]
		public IEnumerator Building_HealthIncreases_RemovesFires()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			yield return null;

			// Initialize fire threshold at full health
			baseUnit.LateUpdate();

			// Drop to 50% to spawn fires
			baseUnit.Health = maxHp * 0.5f;
			baseUnit.LateUpdate();
			yield return null;

			int firesBefore = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire")
					firesBefore++;
			}
			Assert.Greater(firesBefore, 0, "Should have fires after damage");

			// Heal back to 80% — should remove some fires
			baseUnit.Health = maxHp * 0.8f;
			baseUnit.LateUpdate();
			yield return null; // Let Destroy process

			int firesAfter = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire" && child.gameObject != null)
					firesAfter++;
			}

			// After healing, fewer fires should remain (some destroyed)
			// Note: Object.Destroy is deferred, so we may need an extra frame
			yield return null;

			int firesFinal = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire")
					firesFinal++;
			}

			Assert.Less(firesFinal, firesBefore,
				"Fires should be removed when building health increases");
		}

		#endregion

		#region Full Fire Lifecycle

		/// <summary>
		/// Building fires spawn on damage and are cleaned up on full heal.
		/// </summary>
		[UnityTest]
		public IEnumerator Building_DamageThenHeal_FiresClearedAtFullHealth()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(10, 10, 0));
			float maxHp = Constants.HEALTH[UnitType.BASE];
			yield return null;

			baseUnit.LateUpdate();

			// Damage to 30%
			baseUnit.Health = maxHp * 0.3f;
			baseUnit.LateUpdate();
			yield return null;

			int firesDamaged = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire")
					firesDamaged++;
			}
			Assert.Greater(firesDamaged, 0, "Should have fires at 30% health");

			// Heal in 2% increments (one fire-threshold step per frame).
			// RemoveOneBuildingFire uses Object.Destroy which is deferred,
			// so only one fire can be effectively removed per frame.
			float step = maxHp * 0.02f;
			for (float hp = maxHp * 0.3f + step; hp <= maxHp; hp += step)
			{
				baseUnit.Health = Mathf.Min(hp, maxHp);
				baseUnit.LateUpdate();
				yield return null;
			}
			// Final frame at full health
			baseUnit.Health = maxHp;
			baseUnit.LateUpdate();
			yield return null;

			int firesHealed = 0;
			foreach (Transform child in baseUnit.transform)
			{
				if (child.name == "BuildingFire")
					firesHealed++;
			}
			Assert.AreEqual(0, firesHealed,
				"All fires should be removed at full health");
		}

		#endregion
	}
}
