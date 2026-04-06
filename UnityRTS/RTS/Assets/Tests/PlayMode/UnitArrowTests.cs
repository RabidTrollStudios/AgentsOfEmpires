using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for archer arrow spawning (SpawnArrow).
	/// Exercises the full ArrowProjectile lifecycle: launch, flight, stuck, explosion.
	/// Uses real sprites and animator controllers via VisualTestHelper.
	/// </summary>
	[TestFixture]
	public class UnitArrowTests : PlayModeTestBase
	{
		/// <summary>
		/// Force the archer animator into attack (Shoot) state and advance it.
		/// In PlayMode tests the animation system may not advance normalizedTime
		/// on its own, so we manually drive the animator.
		/// </summary>
		private static void ForceArcherAttackState(Unit archer)
		{
			var anim = archer.GetComponent<Animator>();
			if (anim != null && anim.runtimeAnimatorController != null)
			{
				anim.SetInteger("State", 2); // Shoot state for Archer
				anim.Update(0f); // Apply state change
			}
		}

		private static void AdvanceAnimator(Unit unit, float dt)
		{
			var anim = unit.GetComponent<Animator>();
			if (anim != null)
				anim.Update(dt);
		}

		#region Arrow Spawning

		/// <summary>
		/// An archer attacking an adjacent enemy spawns an Arrow GameObject
		/// when the ArrowSprite is available.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttackInRange_SpawnsArrow()
		{
			VisualTestHelper.InjectVisualAssets();

			// Place archer and enemy adjacent
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			yield return null;

			// Start attack — archer should be in range (attack range 9 > distance 3)
			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction);
			ForceArcherAttackState(archer);

			// Step many frames to let the animator reach frame 5 (normalizedTime >= 0.625)
			// and trigger SpawnArrow
			bool arrowFound = false;
			for (int i = 0; i < 120; i++)
			{
				AdvanceAnimator(archer, Time.fixedDeltaTime);
				BuildingTestHelper.Step(archer);
				yield return null;

				if (GameObject.Find("Arrow") != null)
				{
					arrowFound = true;
					break;
				}
			}

			Assert.IsTrue(arrowFound, "Arrow should be spawned during archer attack animation");
		}

		/// <summary>
		/// Arrow spawned by archer has a SpriteRenderer with the arrow sprite assigned.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_Arrow_HasSpriteRenderer()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			yield return null;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));

			// Force the animator into attack state and advance past the arrow fire point
			var anim = archer.GetComponent<Animator>();
			if (anim != null && anim.runtimeAnimatorController != null)
			{
				anim.SetInteger("State", 2); // Shoot state for Archer
				anim.Update(0f); // Apply state change
			}

			for (int i = 0; i < 120; i++)
			{
				// Advance animator to ensure normalizedTime progresses past 0.625
				if (anim != null)
					anim.Update(Time.fixedDeltaTime);

				BuildingTestHelper.Step(archer);
				yield return null;

				var arrowGo = GameObject.Find("Arrow");
				if (arrowGo != null)
				{
					var sr = arrowGo.GetComponent<SpriteRenderer>();
					Assert.IsNotNull(sr, "Arrow should have SpriteRenderer");
					Assert.IsNotNull(sr.sprite, "Arrow SpriteRenderer should have sprite assigned");

					var projectile = arrowGo.GetComponent<ArrowProjectile>();
					Assert.IsNotNull(projectile, "Arrow should have ArrowProjectile component");
					yield break;
				}
			}

			Assert.Fail("Arrow was not spawned within timeout");
		}

		/// <summary>
		/// Arrow aimed at a building targets the closest cell.
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_AttackBuilding_SpawnsArrow()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			// Place enemy barracks (3x3 building) within range
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(10, 10, 0), ctx.Agent1Go);
			barracks.IsBuilt = true;
			yield return null;

			archer.StartAttacking(new AttackEventArgs(archer, barracks));
			ForceArcherAttackState(archer);

			bool arrowFound = false;
			for (int i = 0; i < 120; i++)
			{
				AdvanceAnimator(archer, Time.fixedDeltaTime);
				BuildingTestHelper.Step(archer);
				yield return null;

				if (GameObject.Find("Arrow") != null)
				{
					arrowFound = true;
					break;
				}
			}

			Assert.IsTrue(arrowFound, "Arrow should spawn when attacking a building");
		}

		/// <summary>
		/// Without ArrowSprite, no Arrow is spawned (null guard in SpawnArrow).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_NoArrowSprite_NoArrowSpawned()
		{
			// Don't inject visual assets — ArrowSprite stays null on fresh PrefabLoader
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			yield return null;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			ForceArcherAttackState(archer);

			for (int i = 0; i < 60; i++)
			{
				AdvanceAnimator(archer, Time.fixedDeltaTime);
				BuildingTestHelper.Step(archer);
				yield return null;
			}

			var arrow = GameObject.Find("Arrow");
			Assert.IsNull(arrow, "No arrow should spawn without ArrowSprite");
		}

		#endregion

		#region Arrow Lifecycle

		/// <summary>
		/// An arrow that has been spawned eventually self-destructs
		/// (goes through Flying → Stuck → Exploding → Destroy).
		/// </summary>
		[UnityTest]
		public IEnumerator Arrow_CompletesLifecycle_SelfDestructs()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			yield return null;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			ForceArcherAttackState(archer);

			// Wait for arrow to spawn
			GameObject arrowGo = null;
			for (int i = 0; i < 120; i++)
			{
				AdvanceAnimator(archer, Time.fixedDeltaTime);
				BuildingTestHelper.Step(archer);
				yield return null;
				arrowGo = GameObject.Find("Arrow");
				if (arrowGo != null) break;
			}

			Assert.IsNotNull(arrowGo, "Arrow should spawn during archer attack to test lifecycle");

			// Now wait for the arrow to be destroyed (flight + stuck + exploding ≈ 1-2 seconds)
			float elapsed = 0f;
			while (arrowGo != null && elapsed < 5f)
			{
				elapsed += Time.deltaTime;
				yield return null;
			}

			// After enough time, arrow should be destroyed
			// (Unity sets destroyed objects to null-equivalent)
			Assert.IsTrue(arrowGo == null,
				"Arrow should self-destruct after completing its lifecycle");
		}

		#endregion
	}
}
