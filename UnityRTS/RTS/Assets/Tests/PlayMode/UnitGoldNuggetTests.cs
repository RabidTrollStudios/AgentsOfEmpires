using System.Collections;
using System.Reflection;
using AgentSDK;
using GameManager.EnumTypes;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for gold nugget spawning during mining (SpawnGoldNugget).
	/// Uses real GoldResourceSprite via VisualTestHelper.
	/// SpawnGoldNugget is invoked via reflection to avoid animation-timing dependency
	/// (animator normalizedTime does not advance reliably with manual Step calls).
	/// </summary>
	[TestFixture]
	public class UnitGoldNuggetTests : PlayModeTestBase
	{
		private static void InvokeSpawnGoldNugget(Unit pawn)
		{
			typeof(Unit)
				.GetMethod("SpawnGoldNugget", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(pawn, null);
		}

		private static readonly FieldInfo GatherPhaseField =
			typeof(Unit).GetField("gatherPhase", BindingFlags.NonPublic | BindingFlags.Instance);

		private static GatherPhase GetGatherPhase(Unit unit) =>
			(GatherPhase)GatherPhaseField.GetValue(unit);

		#region Gold Nugget Spawning

		/// <summary>
		/// SpawnGoldNugget creates a GoldNugget GameObject when GoldResourceSprite is available.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Mining_SpawnsGoldNugget()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(14, 15, 0));

			yield return null;

			InvokeSpawnGoldNugget(pawn);

			var nuggetGo = GameObject.Find("GoldNugget");
			Assert.IsNotNull(nuggetGo,
				"GoldNugget should be spawned by SpawnGoldNugget when GoldResourceSprite is set");
		}

		/// <summary>
		/// GoldNugget has a SpriteRenderer with the gold sprite and a GoldNuggetProjectile.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Mining_NuggetHasComponents()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(14, 15, 0));

			yield return null;

			InvokeSpawnGoldNugget(pawn);

			var nuggetGo = GameObject.Find("GoldNugget");
			Assert.IsNotNull(nuggetGo, "GoldNugget should exist after SpawnGoldNugget");

			var sr = nuggetGo.GetComponent<SpriteRenderer>();
			Assert.IsNotNull(sr, "GoldNugget should have SpriteRenderer");
			Assert.IsNotNull(sr.sprite, "GoldNugget should have sprite assigned");

			var proj = nuggetGo.GetComponent<GoldNuggetProjectile>();
			Assert.IsNotNull(proj, "GoldNugget should have GoldNuggetProjectile component");
		}

		/// <summary>
		/// Full animation-integration test: pawn enters MINING phase via gather,
		/// the animator transitions to InteractPickaxe (State=6), and when
		/// normalizedTime crosses 0.5 UpdateGather calls SpawnGoldNugget.
		/// Uses animator.Update() to step the animation deterministically.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Mining_AnimationTriggersNuggetSpawn()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(0, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			VisualTestHelper.SetupMineAnimator(mine);

			// Place pawn adjacent to mine so it enters MINING immediately
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(14, 15, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			// Verify the animator controller was assigned
			var animator = pawn.GetComponent<Animator>();
			Assert.IsNotNull(animator, "Pawn should have an Animator");
			Assert.IsNotNull(animator.runtimeAnimatorController,
				"Pawn animator should have a controller assigned by SetupPawnAnimator");

			// Start gathering
			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);

			// Step until pawn enters MINING phase
			for (int i = 0; i < 60; i++)
			{
				BuildingTestHelper.Step(pawn);
				yield return null;
				if (GetGatherPhase(pawn) == GatherPhase.MINING)
					break;
			}
			Assert.AreEqual(GatherPhase.MINING, GetGatherPhase(pawn),
				"Pawn should have entered MINING phase");

			// Step once so UpdateAnimation sets State=6 (InteractPickaxe) for MINING
			pawn.Update();

			// Wait for the animator state transition to complete.
			// During transitions, normalizedTime reports the SOURCE state's time,
			// not the destination (InteractPickaxe) state.
			for (int i = 0; i < 20; i++)
			{
				animator.Update(0.1f);
				yield return null;
				if (!animator.IsInTransition(0))
					break;
			}

			// Now in InteractPickaxe — step until normalizedTime crosses 0.5
			bool nuggetFound = false;
			for (int step = 0; step < 60; step++)
			{
				animator.Update(0.1f);
				BuildingTestHelper.Step(pawn);
				yield return null;

				if (GameObject.Find("GoldNugget") != null)
				{
					nuggetFound = true;
					break;
				}
			}

			Assert.IsTrue(nuggetFound,
				"GoldNugget should be spawned when animation normalizedTime crosses 0.5 " +
				$"(final normalizedTime: {animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f:F3})");
		}

		/// <summary>
		/// GoldNugget self-destructs after its flight completes (0.4s).
		/// Creates the projectile directly to avoid animation-timing dependency.
		/// </summary>
		[UnityTest]
		public IEnumerator GoldNugget_SelfDestructsAfterFlight()
		{
			// Create a GoldNuggetProjectile directly — no animation dependency
			var nuggetGo = new GameObject("GoldNugget");
			nuggetGo.AddComponent<SpriteRenderer>();
			var nugget = nuggetGo.AddComponent<GoldNuggetProjectile>();

			Vector3 start = new Vector3(1f, 1f, 0f);
			Vector3 end = new Vector3(1f, 0.5f, 0f);
			nugget.Launch(start, end);

			yield return null;

			// Nugget should exist immediately after launch
			Assert.IsTrue(nuggetGo != null, "GoldNugget should exist after launch");

			// Wait for it to self-destruct (flight duration is 0.4s, wait up to 2s)
			float elapsed = 0f;
			while (nuggetGo != null && elapsed < 2f)
			{
				elapsed += Time.deltaTime;
				yield return null;
			}

			Assert.IsTrue(nuggetGo == null,
				"GoldNugget should self-destruct after flight completes (~0.4s)");
		}

		/// <summary>
		/// Without GoldResourceSprite, SpawnGoldNugget is a no-op (null guard).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Mining_NoNuggetWithoutSprite()
		{
			// Don't inject visual assets — GoldResourceSprite stays null
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(14, 15, 0));

			yield return null;

			InvokeSpawnGoldNugget(pawn);

			var nugget = GameObject.Find("GoldNugget");
			Assert.IsNull(nugget, "No nugget should spawn without GoldResourceSprite");
		}

		#endregion
	}
}
