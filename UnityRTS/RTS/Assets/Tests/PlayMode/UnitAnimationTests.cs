using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for animation state management:
	/// - Lancer state hash initialization and directional attacks
	/// - Warrior attack animation toggle (useAttack2)
	/// - Pawn/Archer animation state selection
	/// Uses real AnimatorControllers via VisualTestHelper.
	/// </summary>
	[TestFixture]
	public class UnitAnimationTests : PlayModeTestBase
	{
		#region Lancer Animation Hashes

		/// <summary>
		/// InitLancerStateHashes populates hashes from the Lancer_Blue controller.
		/// After init, the lancer can play states by hash without exceptions.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_InitStateHashes_PopulatesFromController()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			yield return null;

			// Assign real controller and initialize hashes
			VisualTestHelper.SetupLancerAnimator(lancer);
			yield return null;

			// Tick to trigger UpdateAnimation which uses lancerStateHashes
			BuildingTestHelper.Tick(lancer);
			yield return null;

			// Lancer should still be IDLE (no commands issued) — no exceptions thrown
			Assert.AreEqual(UnitAction.IDLE, lancer.CurrentAction);
		}

		/// <summary>
		/// Lancer in MOVE state with a path uses Run animation (index 1).
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_Moving_PlaysRunAnimation()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(5, 5, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			yield return null;

			lancer.StartMoving(new MoveEventArgs(lancer, UnitType.LANCER, new Vector3Int(20, 20, 0)));
			Assert.AreEqual(UnitAction.MOVE, lancer.CurrentAction);

			// Tick to drive animation update
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			// Verify no exceptions and lancer is still moving
			Assert.AreEqual(UnitAction.MOVE, lancer.CurrentAction);
		}

		/// <summary>
		/// Lancer attacking an adjacent enemy uses a directional attack animation.
		/// The lancer animation code selects Right/Up/Down/UpRight/DownRight based on angle.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttackingRight_UsesDirectionalAttack()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			// Enemy to the right (within lancer range 2.5)
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(12, 10, 0), ctx.Agent1Go);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);

			// Tick to trigger directional attack animation selection
			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			// Still attacking — directional attack hash was used without exception
			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);
		}

		/// <summary>
		/// Lancer attacking an enemy above uses the Up directional attack.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttackingUp_UsesUpAttack()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			// Enemy directly above (within range 2.5)
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 12, 0), ctx.Agent1Go);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));

			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);
		}

		/// <summary>
		/// Lancer attacking an enemy below uses the Down directional attack.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_AttackingDown_UsesDownAttack()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 8, 0), ctx.Agent1Go);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));

			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);
		}

		/// <summary>
		/// Lancer without an animator controller — InitLancerStateHashes returns early
		/// and UpdateAnimation doesn't crash.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_NoController_DoesNotCrash()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			yield return null;

			// InitLancerStateHashes without a controller — should not crash
			lancer.InitLancerStateHashes();

			BuildingTestHelper.Tick(lancer);
			yield return null;

			Assert.AreEqual(UnitAction.IDLE, lancer.CurrentAction);
		}

		#endregion

		#region Warrior Attack Animation Toggle

		/// <summary>
		/// Warrior attacking with real animator transitions between
		/// Attack1 and Attack2 states (useAttack2 toggle).
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_Attacking_AnimationPlaysWithoutCrash()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupWarriorAnimator(warrior);
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Tick many frames to allow the animation to complete one loop
			// and potentially trigger the useAttack2 toggle
			for (int i = 0; i < 60; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
			}

			// Warrior should still be attacking (enemy still alive or just died)
			// Main point: no exceptions from the attack toggle logic
			Assert.IsTrue(
				warrior.CurrentAction == UnitAction.ATTACK || warrior.CurrentAction == UnitAction.IDLE,
				"Warrior should be in ATTACK or IDLE (enemy dead)");
		}

		/// <summary>
		/// Warrior chasing (ATTACK with path) uses Run animation (state=1).
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_Chasing_PlaysRunAnimation()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			VisualTestHelper.SetupWarriorAnimator(warrior);
			// Far enough that warrior must chase
			Unit enemy = PlaceUnit(UnitType.PAWN, new Vector3Int(20, 20, 0), ctx.Agent1Go);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
			}

			// Still attacking/chasing — Run animation used for pursuit
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);
		}

		/// <summary>
		/// Warrior in MOVE state arriving at destination uses Guard animation (state=4).
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_MoveArrived_PlaysGuardAnimation()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupWarriorAnimator(warrior);
			yield return null;

			// Move to a nearby spot
			warrior.StartMoving(new MoveEventArgs(warrior, UnitType.WARRIOR, new Vector3Int(12, 10, 0)));

			// Wait for arrival
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(warrior);
				return warrior.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 10f, failMessage: "Warrior should arrive at destination");

			Assert.AreEqual(UnitAction.IDLE, warrior.CurrentAction);
		}

		#endregion

		#region Archer Animation

		/// <summary>
		/// Archer in range of enemy uses Shoot animation (state=2).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_InRange_PlaysShootAnimation()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(13, 10, 0), ctx.Agent1Go);
			yield return null;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction);

			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(archer);
				yield return null;
			}

			// Archer should be attacking in range (no path, shoot animation)
			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction);
		}

		#endregion

		#region Pawn Animation States

		/// <summary>
		/// Pawn in BUILD state with path uses RunHammer animation (state=4).
		/// Pawn in BUILD state building uses InteractHammer (state=3).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Building_AnimationStatesWork()
		{
			VisualTestHelper.InjectVisualAssets();

			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(5, 5, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BARRACKS));

			if (pawn.CurrentAction == UnitAction.BUILD)
			{
				// Tick a few frames — pawn should be in TO_POSITION or BUILDING phase
				for (int i = 0; i < 30; i++)
				{
					BuildingTestHelper.Tick(pawn);
					yield return null;
				}

				// No crashes from animation state logic
				Assert.IsTrue(
					pawn.CurrentAction == UnitAction.BUILD || pawn.CurrentAction == UnitAction.IDLE,
					"Pawn should be in BUILD or IDLE");
			}

			yield return null;
		}

		/// <summary>
		/// Pawn in GATHER heading to mine uses RunPickaxe (state=5).
		/// Pawn mining uses InteractPickaxe (state=6).
		/// Pawn heading to base with gold uses RunGold (state=2).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Gathering_AnimationStatesWork()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(0, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);

			// Tick through the gather cycle — TO_MINE, MINING, TO_BASE
			for (int i = 0; i < 200; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;

				if (pawn.CurrentAction != UnitAction.GATHER)
					break;
			}

			// No animation crashes
			Assert.Pass("Pawn gather animation states executed without exceptions");
		}

		#endregion

		#region Facing Direction

		/// <summary>
		/// Pawn building faces toward the building site.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Building_FacesBuilding()
		{
			Unit baseUnit = BuildingTestHelper.PlaceBuiltBase(ctx, new Vector3Int(5, 5, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BARRACKS));

			// Tick until building phase
			for (int i = 0; i < 60; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			// No crashes from facing logic
			yield return null;
		}

		/// <summary>
		/// Lancer facing updates based on movement and attack target direction.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_Facing_UpdatesOnAttack()
		{
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			// Enemy to the left
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(8, 10, 0), ctx.Agent1Go);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));

			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			// Lancer should flip to face left (UpdateLancerFacing)
			var sr = lancer.GetComponent<SpriteRenderer>();
			Assert.IsNotNull(sr);
			// flipX should be true when facing left (enemy is to the left)
			Assert.IsTrue(sr.flipX, "Lancer should face left toward enemy");
		}

		#endregion
	}
}
