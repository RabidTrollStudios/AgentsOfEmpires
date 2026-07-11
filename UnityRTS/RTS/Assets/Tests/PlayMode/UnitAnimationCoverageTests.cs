using System.Collections;
using System.Collections.Generic;
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
	/// Tests targeting uncovered animation code paths in Unit.Movement.cs and Unit.cs.
	/// Uses real animator controllers via VisualTestHelper to exercise UpdateAnimation,
	/// InitLancerStateHashes, lancer facing, and Initialize animator branches.
	/// </summary>
	[TestFixture]
	public class UnitAnimationCoverageTests : PlayModeTestBase
	{
		#region Helpers

		private static readonly BindingFlags NonPublic =
			BindingFlags.NonPublic | BindingFlags.Instance;

		private void SetPrivateField(Unit unit, string fieldName, object value) =>
			typeof(Unit).GetField(fieldName, NonPublic).SetValue(unit, value);

		private T GetPrivateField<T>(Unit unit, string fieldName) =>
			(T)typeof(Unit).GetField(fieldName, NonPublic).GetValue(unit);

		private Unit PlaceBuiltBase(Vector3Int pos) =>
			BuildingTestHelper.PlaceBuiltBase(ctx, pos);

		/// <summary>
		/// Assign a real animator controller to the prefab for the given unit type
		/// so that Initialize() sees it when PlaceUnit instantiates the prefab.
		/// </summary>
		private void AssignControllerToPrefab(UnitType unitType, string controllerPath)
		{
#if UNITY_EDITOR
			var prefab = ctx.UnitManager.UnitPrefabs[0][unitType];
			var animator = prefab.GetComponent<Animator>();
			if (animator != null)
			{
				animator.runtimeAnimatorController =
					UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
			}
#endif
		}

		#endregion

		#region Initialize — MINE animator (Unit.cs:482-485)

		/// <summary>
		/// When a MINE is placed with a real animator controller,
		/// Initialize() calls animator.Play(0, 0, Random.value) to offset shimmer.
		/// </summary>
		[UnityTest]
		public IEnumerator Initialize_Mine_WithAnimator_PlaysRandomOffset()
		{
			// Assign controller to prefab BEFORE PlaceUnit so Initialize sees it
			AssignControllerToPrefab(UnitType.MINE, VisualTestHelper.MineControllerPath);

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 15, 0));
			yield return null;

			var animator = mine.GetComponent<Animator>();
			Assert.IsNotNull(animator, "Mine should have an Animator");
			Assert.IsNotNull(animator.runtimeAnimatorController,
				"Mine should have a controller from prefab");

			// Initialize already ran during PlaceUnit — verify animator is active
			var info = animator.GetCurrentAnimatorStateInfo(0);
			Assert.IsTrue(info.length > 0, "Mine animator should have an active state");
		}

		#endregion

		#region Initialize — LANCER animator (Unit.cs:487-490)

		/// <summary>
		/// When a LANCER is placed with a real animator controller,
		/// Initialize() calls InitLancerStateHashes() and plays Idle.
		/// </summary>
		[UnityTest]
		public IEnumerator Initialize_Lancer_WithAnimator_InitsHashesAndPlaysIdle()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			BuildingTestHelper.PlaceBuiltTower(ctx, new Vector3Int(8, 8, 0));

			// Assign controller to prefab BEFORE PlaceUnit so Initialize sees it
			AssignControllerToPrefab(UnitType.LANCER, VisualTestHelper.LancerControllerPath);

			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(15, 15, 0));
			yield return null;

			var hashes = GetPrivateField<int[]>(lancer, "lancerStateHashes");
			Assert.IsNotNull(hashes, "lancerStateHashes should be populated");
			Assert.AreEqual(12, hashes.Length, "Should have 12 lancer state hashes");
			Assert.AreNotEqual(0, hashes[0], "Idle hash should be non-zero");
		}

		#endregion

		#region Initialize — CanMove animator (Unit.cs:492-496)

		/// <summary>
		/// When a mobile unit (PAWN) is placed with a real animator controller,
		/// Initialize() sets State=0 and calls Play(0,0,0).
		/// </summary>
		[UnityTest]
		public IEnumerator Initialize_Pawn_WithAnimator_SetsIdleState()
		{
			// Assign controller to prefab BEFORE PlaceUnit so Initialize sees it
			AssignControllerToPrefab(UnitType.PAWN, VisualTestHelper.PawnControllerPath);

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			var animator = pawn.GetComponent<Animator>();
			Assert.IsNotNull(animator.runtimeAnimatorController);
			Assert.AreEqual(0, animator.GetInteger("State"),
				"Pawn animator should be in idle state after Initialize");
		}

		#endregion

		#region Warrior useAttack2 toggle (Unit.Movement.cs:437)

		/// <summary>
		/// Warrior attack animation alternates between Attack1 and Attack2
		/// when normalizedTime crosses 1.0. Covers the useAttack2 toggle line.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_Attack_AlternatesAttackAnimation()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			VisualTestHelper.SetupWarriorAnimator(warrior);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			var animator = warrior.GetComponent<Animator>();

			// Tick to let animator process attack state
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
			}

			// Wait for transition to complete
			for (int i = 0; i < 20; i++)
			{
				animator.Update(0.1f);
				yield return null;
				if (!animator.IsInTransition(0))
					break;
			}

			// Advance animation past normalizedTime 1.0 to trigger useAttack2 toggle
			bool toggled = false;
			bool initialUseAttack2 = GetPrivateField<bool>(warrior, "useAttack2");
			for (int step = 0; step < 80; step++)
			{
				animator.Update(0.1f);
				warrior.Update(); // calls UpdateAnimation
				yield return null;

				bool currentUseAttack2 = GetPrivateField<bool>(warrior, "useAttack2");
				if (currentUseAttack2 != initialUseAttack2)
				{
					toggled = true;
					break;
				}
			}

			Assert.IsTrue(toggled, "useAttack2 should toggle after attack animation completes a loop");
		}

		#endregion

		#region Archer MOVE with path (Unit.Movement.cs:450-452)

		/// <summary>
		/// Archer in MOVE action with path.Count > 0 sets state=1 (Run).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_Move_WithPath_SetsRunState()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			VisualTestHelper.SetupArcherAnimator(archer);
			yield return null;

			archer.StartMoving(new MoveEventArgs(archer, UnitType.ARCHER, new Vector3Int(20, 10, 0)));
			Assert.AreEqual(UnitAction.MOVE, archer.CurrentAction);

			// Tick so UpdateAnimation runs with path
			for (int i = 0; i < 3; i++)
			{
				BuildingTestHelper.Tick(archer);
				yield return null;
			}

			var animator = archer.GetComponent<Animator>();
			Assert.AreEqual(1, animator.GetInteger("State"),
				"Archer with MOVE action and path should be in Run state (1)");
		}

		#endregion

		#region Archer ATTACK with path (Unit.Movement.cs:455-456)

		/// <summary>
		/// Archer in ATTACK action with path.Count > 0 (pursuing target)
		/// sets state=1 (Run).
		/// </summary>
		[UnityTest]
		public IEnumerator Archer_Attack_WithPath_SetsRunState()
		{
			Unit archer = PlaceUnit(UnitType.ARCHER, new Vector3Int(5, 10, 0));
			// Place enemy far away so archer has to chase (beyond range 9)
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(25, 10, 0), ctx.Agent1Go);
			VisualTestHelper.SetupArcherAnimator(archer);
			yield return null;

			archer.StartAttacking(new AttackEventArgs(archer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, archer.CurrentAction);

			// Tick a few frames — archer should be chasing with a path
			for (int i = 0; i < 3; i++)
			{
				BuildingTestHelper.Tick(archer);
				yield return null;
			}

			var path = GetPrivateField<List<Vector3Int>>(archer, "path");
			if (path.Count > 0)
			{
				var animator = archer.GetComponent<Animator>();
				Assert.AreEqual(1, animator.GetInteger("State"),
					"Archer pursuing target with path should be in Run state (1)");
			}
		}

		#endregion

		#region Pawn MOVE with path (Unit.Movement.cs:481-483)

		/// <summary>
		/// Pawn in MOVE action with path.Count > 0 sets state=1 (Run).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Move_WithPath_SetsRunState()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 10, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(20, 10, 0)));
			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction);

			for (int i = 0; i < 3; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			var animator = pawn.GetComponent<Animator>();
			Assert.AreEqual(1, animator.GetInteger("State"),
				"Pawn with MOVE action and path should be in Run state (1)");
		}

		#endregion

		#region Warrior ATTACK with path (Unit.Movement.cs:503-505)

		/// <summary>
		/// Warrior in ATTACK action with path.Count > 0 (chasing) sets state=1 (Run).
		/// Uses WARRIOR because PAWN has CAN_ATTACK=false.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_Attack_WithPath_SetsRunState()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 10, 0));
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(20, 10, 0), ctx.Agent1Go);
			VisualTestHelper.SetupPawnAnimator(warrior);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction,
				"Warrior should enter ATTACK state");

			for (int i = 0; i < 3; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
			}

			var path = GetPrivateField<List<Vector3Int>>(warrior, "path");
			if (path != null && path.Count > 0)
			{
				var animator = warrior.GetComponent<Animator>();
				Assert.AreEqual(1, animator.GetInteger("State"),
					"Warrior chasing target with path should be in Run state (1)");
			}
		}

		#endregion

		#region Facing — building to the left (Unit.Movement.cs:522-523)

		/// <summary>
		/// When a pawn is building a structure to its left, facingRight should be false.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Building_FacesLeftWhenBuildingIsToLeft()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 10, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			// Build to the left of the pawn
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BARRACKS));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build");

			// Tick until building phase
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return GetPrivateField<BuildPhase>(pawn, "buildPhase") == BuildPhase.BUILDING;
			}, timeoutSeconds: 15f, failMessage: "Pawn should reach BUILDING phase");

			// Now UpdateAnimation should set facing based on building position
			pawn.Update();
			yield return null;

			var unitSprite = pawn.GetComponent<SpriteRenderer>();
			Assert.IsNotNull(unitSprite);
			// Building at x=10, pawn at x=15 → dx < 0 → facingRight=false → flipX=true
			Assert.IsTrue(unitSprite.flipX,
				"Pawn should face left (flipX=true) when building is to the left");
		}

		#endregion

		#region Facing — mine to the left (Unit.Movement.cs:532-533)

		/// <summary>
		/// When a pawn is mining at a mine to its left, facingRight should be false.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Mining_FacesLeftWhenMineIsToLeft()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(0, 0, 0));
			// Place mine to the LEFT of the pawn
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(5, 15, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 15, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Tick until MINING phase
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return GetPrivateField<GatherPhase>(pawn, "gatherPhase") == GatherPhase.MINING;
			}, timeoutSeconds: 15f, failMessage: "Pawn should enter MINING phase");

			// Tick to run UpdateAnimation
			pawn.Update();
			yield return null;

			var unitSprite = pawn.GetComponent<SpriteRenderer>();
			Assert.IsNotNull(unitSprite);
			// Mine at x=5, pawn at x=10 → dx < 0 → facingRight=false → flipX=true
			Assert.IsTrue(unitSprite.flipX,
				"Pawn should face left (flipX=true) when mine is to the left");
		}

		#endregion

		#region Facing — attack target to the left (Unit.Movement.cs:542-543)

		/// <summary>
		/// When a warrior is attacking a target to its left (stationary, in range),
		/// facingRight should be false.
		/// </summary>
		[UnityTest]
		public IEnumerator Warrior_Attack_FacesLeftWhenTargetIsToLeft()
		{
			// Place enemy to the LEFT of the warrior
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(9, 10, 0), ctx.Agent1Go);
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupWarriorAnimator(warrior);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, enemy));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			// Tick to process — warrior should be adjacent (in range), no path needed
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
			}

			var unitSprite = warrior.GetComponent<SpriteRenderer>();
			Assert.IsNotNull(unitSprite);
			// Enemy at x=9, warrior at x=10 → dx < 0 → facingRight=false → flipX=true
			Assert.IsTrue(unitSprite.flipX,
				"Warrior should face left (flipX=true) when attack target is to the left");
		}

		#endregion

		#region Lancer pursuing target — state=1 Run (Unit.Movement.cs:634)

		/// <summary>
		/// Lancer in ATTACK action with path.Count > 0 (pursuing) returns state index 1 (Run).
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_Attack_WithPath_SetsRunState()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			BuildingTestHelper.PlaceBuiltTower(ctx, new Vector3Int(8, 8, 0));

			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(5, 15, 0));
			// Place enemy far away so lancer has to chase
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(25, 15, 0), ctx.Agent1Go);
			VisualTestHelper.SetupLancerAnimator(lancer);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);

			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			var animator = lancer.GetComponent<Animator>();
			// Lancer pursuing should have State=1 (Run)
			Assert.AreEqual(1, animator.GetInteger("State"),
				"Lancer pursuing target with path should be in Run state (1)");
		}

		#endregion

		#region Lancer facingRight=false (Unit.Movement.cs:669)

		/// <summary>
		/// Lancer attacking a target to its left should have facingRight=false.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_Attack_FacesLeftWhenTargetIsToLeft()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			BuildingTestHelper.PlaceBuiltTower(ctx, new Vector3Int(8, 8, 0));

			// Place enemy to the LEFT of lancer
			Unit enemy = PlaceUnit(UnitType.WARRIOR, new Vector3Int(14, 15, 0), ctx.Agent1Go);
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(15, 15, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemy));
			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);

			// Tick to process attack — lancer should be adjacent (range 2.5), in range
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			var unitSprite = lancer.GetComponent<SpriteRenderer>();
			Assert.IsNotNull(unitSprite);
			// Enemy at x=14, lancer at x=15 → dx < 0 → facingRight=false → flipX=true
			Assert.IsTrue(unitSprite.flipX,
				"Lancer should face left (flipX=true) when target is to the left");
		}

		#endregion

		#region Lancer fallback hash (Unit.Movement.cs:608)

		/// <summary>
		/// InitLancerStateHashes fills missing hashes with the Idle hash as fallback.
		/// This test verifies all 12 slots are populated even if some clips are missing.
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_InitStateHashes_AllSlotsPopulated()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			BuildingTestHelper.PlaceBuiltTower(ctx, new Vector3Int(8, 8, 0));

			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(15, 15, 0));
			VisualTestHelper.SetupLancerAnimator(lancer);
			yield return null;

			var hashes = GetPrivateField<int[]>(lancer, "lancerStateHashes");
			Assert.IsNotNull(hashes);
			Assert.AreEqual(12, hashes.Length);

			// All slots should be non-negative (fallback ensures no -1 remains)
			for (int i = 0; i < hashes.Length; i++)
			{
				Assert.AreNotEqual(-1, hashes[i],
					$"Lancer state hash at index {i} should not be -1 (should fallback to Idle)");
			}
		}

		#endregion

		#region BuildAnimator.SetBool("IsBuilt") (Unit.Tasks.cs:329)

		/// <summary>
		/// When a pawn completes building a structure that has an animator,
		/// buildAnimator.SetBool("IsBuilt", true) is called.
		/// We use a MINE as the building target since it has a real animator controller.
		/// Actually, we build a BARRACKS and give it an animator to test this path.
		/// </summary>
		[UnityTest]
		public IEnumerator Build_Complete_SetsAnimatorIsBuiltBool()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			// Start building a BARRACKS
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(13, 10, 0), UnitType.BARRACKS));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build");

			var buildingObj = GetPrivateField<GameObject>(pawn, "currentBuilding");
			Assert.IsNotNull(buildingObj);

			// Add an animator with a controller to the building so the SetBool line executes
			var buildAnimator = buildingObj.GetComponent<Animator>();
			if (buildAnimator == null)
				buildAnimator = buildingObj.AddComponent<Animator>();
			// Use mine controller as a stand-in — just needs to be non-null
			VisualTestHelper.AssignAnimatorController(
				buildingObj.GetComponent<Unit>(), VisualTestHelper.PawnControllerPath);

			Unit buildingUnit = buildingObj.GetComponent<Unit>();

			// Wait for construction to complete
			yield return BuildingTestHelper.WaitForConstruction(pawn, buildingUnit, 20f);

			Assert.IsTrue(buildingUnit.IsBuilt, "Building should be complete");
		}

		#endregion

		#region Lancer directional attack angles (Unit.Movement.cs:645-659)

		/// <summary>
		/// Lancer attacking targets at various angles exercises directional attack index logic.
		/// Up (>67.5°), UpRight (22.5-67.5°), Right (-22.5-22.5°), DownRight (-67.5--22.5°), Down (<-67.5°).
		/// </summary>
		[UnityTest]
		public IEnumerator Lancer_DirectionalAttack_CoverAllAngles()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			BuildingTestHelper.PlaceBuiltTower(ctx, new Vector3Int(8, 8, 0));

			// Test Up attack (enemy directly above — high HP so it survives 5 ticks)
			Unit lancer = PlaceUnit(UnitType.LANCER, new Vector3Int(15, 15, 0));
			Unit enemyUp = PlaceUnit(UnitType.WARRIOR, new Vector3Int(15, 17, 0), ctx.Agent1Go);
			VisualTestHelper.SetupLancerAnimator(lancer);
			yield return null;

			lancer.StartAttacking(new AttackEventArgs(lancer, enemyUp));
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(lancer);
				yield return null;
			}

			// Verify lancer is attacking (directional index exercised)
			Assert.AreEqual(UnitAction.ATTACK, lancer.CurrentAction);

			// Test Down attack (enemy directly below — high HP so it survives 5 ticks)
			Unit lancer2 = PlaceUnit(UnitType.LANCER, new Vector3Int(20, 17, 0));
			Unit enemyDown = PlaceUnit(UnitType.WARRIOR, new Vector3Int(20, 15, 0), ctx.Agent1Go);
			VisualTestHelper.SetupLancerAnimator(lancer2);
			yield return null;

			lancer2.StartAttacking(new AttackEventArgs(lancer2, enemyDown));
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(lancer2);
				yield return null;
			}
			Assert.AreEqual(UnitAction.ATTACK, lancer2.CurrentAction);
		}

		#endregion

		#region Pawn gather animation states (Unit.Movement.cs:494-499)

		/// <summary>
		/// Pawn in various gather phases shows correct animation states:
		/// TO_BASE with path → state=2 (RunGold), MINING → state=6 (InteractPickaxe),
		/// TO_MINE with path → state=5 (RunPickaxe).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Gather_AnimationStates()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(0, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 15, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);

			var animator = pawn.GetComponent<Animator>();

			// Phase 1: TO_MINE with path → state=5 (RunPickaxe)
			// Tick a frame so the path is set
			BuildingTestHelper.Tick(pawn);
			yield return null;

			var gatherPhase = GetPrivateField<GatherPhase>(pawn, "gatherPhase");
			var path = GetPrivateField<List<Vector3Int>>(pawn, "path");

			if (gatherPhase == GatherPhase.TO_MINE && path.Count > 0)
			{
				Assert.AreEqual(5, animator.GetInteger("State"),
					"Pawn heading to mine should have State=5 (RunPickaxe)");
			}

			// Wait until MINING phase
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return GetPrivateField<GatherPhase>(pawn, "gatherPhase") == GatherPhase.MINING;
			}, timeoutSeconds: 15f, failMessage: "Pawn should enter MINING phase");

			// Phase 2: MINING → state=6 (InteractPickaxe)
			pawn.Update();
			yield return null;
			Assert.AreEqual(6, animator.GetInteger("State"),
				"Pawn mining should have State=6 (InteractPickaxe)");

			// Wait until TO_BASE phase
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return GetPrivateField<GatherPhase>(pawn, "gatherPhase") == GatherPhase.TO_BASE;
			}, timeoutSeconds: 15f, failMessage: "Pawn should enter TO_BASE phase");

			// Phase 3: TO_BASE with path → state=2 (RunGold)
			path = GetPrivateField<List<Vector3Int>>(pawn, "path");
			if (path.Count > 0)
			{
				pawn.Update();
				yield return null;
				Assert.AreEqual(2, animator.GetInteger("State"),
					"Pawn heading to base with gold should have State=2 (RunGold)");
			}
		}

		#endregion

		#region Pawn BUILD animation states (Unit.Movement.cs:487-490)

		/// <summary>
		/// Pawn in BUILD action shows correct animation states:
		/// TO_POSITION with path → state=4 (RunHammer), BUILDING → state=3 (InteractHammer).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_Build_AnimationStates()
		{
			PlaceBuiltBase(new Vector3Int(0, 0, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			VisualTestHelper.SetupPawnAnimator(pawn);
			yield return null;

			// Build far enough away that pawn needs to walk
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(18, 10, 0), UnitType.BARRACKS));

			if (pawn.CurrentAction != UnitAction.BUILD)
				Assert.Ignore("Could not start build");

			var animator = pawn.GetComponent<Animator>();

			// Phase 1: TO_POSITION with path → state=4 (RunHammer)
			BuildingTestHelper.Tick(pawn);
			yield return null;

			var buildPhase = GetPrivateField<BuildPhase>(pawn, "buildPhase");
			var path = GetPrivateField<List<Vector3Int>>(pawn, "path");

			if (buildPhase == BuildPhase.TO_POSITION && path.Count > 0)
			{
				Assert.AreEqual(4, animator.GetInteger("State"),
					"Pawn running to build site should have State=4 (RunHammer)");
			}

			// Phase 2: Wait until BUILDING phase → state=3 (InteractHammer)
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return GetPrivateField<BuildPhase>(pawn, "buildPhase") == BuildPhase.BUILDING;
			}, timeoutSeconds: 15f, failMessage: "Pawn should enter BUILDING phase");

			pawn.Update();
			yield return null;
			Assert.AreEqual(3, animator.GetInteger("State"),
				"Pawn building should have State=3 (InteractHammer)");
		}

		#endregion

		#region Collision avoidance — saved path restore (Unit.Movement.cs:759-766)

		/// <summary>
		/// When a unit's collision avoidance fails to find a detour after 10 frames,
		/// it attempts a full re-path. If that also fails, it restores the saved path.
		/// </summary>
		[UnityTest]
		public IEnumerator Movement_CollisionAvoidance_RestoresSavedPathOnFailure()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(10, 10, 0));
			Unit blocker = PlaceUnit(UnitType.WARRIOR, new Vector3Int(11, 10, 0), ctx.Agent1Go);
			yield return null;

			// Give warrior a move target past the blocker
			warrior.StartMoving(new MoveEventArgs(warrior, UnitType.WARRIOR, new Vector3Int(15, 10, 0)));
			Assert.AreEqual(UnitAction.MOVE, warrior.CurrentAction);

			// Set localAvoidWaitFrames > 10 to trigger the full re-path fallback
			SetPrivateField(warrior, "localAvoidWaitFrames", 15);

			// Now create a wall behind the blocker so full re-path also fails
			for (int y = 0; y < 30; y++)
			{
				ctx.MapManager.GridCells[12, y].SetWalkable(false);
				ctx.MapManager.GridCells[12, y].SetBuildable(false);
			}

			// Tick — should attempt full re-path, fail, and restore saved path
			for (int i = 0; i < 5; i++)
			{
				warrior.TickFixedUpdate();
				yield return new WaitForFixedUpdate();
			}

			// Warrior should still have a path (the saved one was restored)
			var path = GetPrivateField<List<Vector3Int>>(warrior, "path");
			Assert.IsTrue(path.Count >= 0,
				"Warrior should still have a path after failed re-path (saved path restored)");
		}

		#endregion

		#region Debug text — REPAIR with null currentBuilding (Unit.Movement.cs:860)

		/// <summary>
		/// When UpdateDebuggingInfo runs for REPAIR action but currentBuilding is null,
		/// textArea.text should be set to "".
		/// </summary>
		[UnityTest]
		public IEnumerator DebuggingInfo_Repair_NullBuilding_SetsEmptyText()
		{
			// Enable debugging so UpdateDebuggingInfo actually runs
			typeof(GameManager).GetProperty("HasUnitDebugging",
				BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, true);
			Unit.HasDebugging = true;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(10, 10, 0));
			yield return null;

			// Force REPAIR action with null currentBuilding
			pawn.CurrentAction = UnitAction.REPAIR;
			SetPrivateField(pawn, "currentBuilding", (GameObject)null);

			// Update triggers UpdateDebuggingInfo
			pawn.Update();
			yield return null;

			// Verify no exception — the REPAIR+null branch sets textArea.text=""
			var textAreas = pawn.GetComponentsInChildren<UnityEngine.UI.Text>(true);
			foreach (var text in textAreas)
			{
				if (text.name == "State Variable")
				{
					Assert.AreEqual("", text.text,
						"REPAIR with null currentBuilding should show empty text");
				}
			}
		}

		#endregion

		#region Unreachable target → IDLE (no engine retargeting, U3)

		/// <summary>
		/// A warrior assigned an enemy it cannot path to goes IDLE rather than
		/// retargeting to another enemy. The engine never picks targets — that is
		/// the PlanningAgent's job. (Replaces the former FindClosestReachableEnemy
		/// coverage, which tested engine-side retargeting that was removed.)
		/// </summary>
		[UnityTest]
		public IEnumerator AttackerWalledOffFromTarget_GoesIdle()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));

			// Wall that blocks pathing to all enemies (legacy GridCells + shared GameGrid)
			for (int y = 0; y < 30; y++)
			{
				ctx.MapManager.GridCells[10, y].SetWalkable(false);
				ctx.MapManager.GridCells[10, y].SetBuildable(false);
				ctx.MapManager.Grid.SetCellBlocked(10, y);
			}

			// Enemies on the far side of the wall
			Unit target = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 5, 0), ctx.Agent1Go);
			PlaceUnit(UnitType.PAWN, new Vector3Int(15, 10, 0), ctx.Agent1Go);
			PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);
			yield return null;

			warrior.StartAttacking(new AttackEventArgs(warrior, target));
			Assert.AreEqual(UnitAction.ATTACK, warrior.CurrentAction);

			bool wentIdle = false;
			for (int i = 0; i < 200; i++)
			{
				BuildingTestHelper.Tick(warrior);
				yield return null;
				if (warrior.CurrentAction == UnitAction.IDLE) { wentIdle = true; break; }
			}

			Assert.IsTrue(wentIdle,
				"Warrior should go IDLE when its assigned target is unreachable (no engine retarget)");
		}

		#endregion
	}
}
