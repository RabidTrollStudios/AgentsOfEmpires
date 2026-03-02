using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests covering Unit code paths with no existing test:
	///   ChangeColor, CanTrainUnit, CanBuildUnit, CenterGridPosition,
	///   LateUpdate / UpdateStateColor, Update / UpdateDebuggingInfo,
	///   UpdatePathVisualization, UpdateTargetVisualization,
	///   and MapVelocityToDirection (dead code, reached via reflection).
	/// </summary>
	[TestFixture]
	public class UnitVisualizationTests : PlayModeTestBase
	{
		// ── Reflection helpers ─────────────────────────────────────────────────────

		/// <summary>Set a public-property-with-private-setter on GameManager.</summary>
		private static void SetGmProperty(string name, object value) =>
			typeof(GameManager)
				.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, value);

		/// <summary>Write a private field on a Unit instance.</summary>
		private static void SetUnitField(Unit unit, string name, object value) =>
			typeof(Unit)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(unit, value);

		/// <summary>Read a private field from a Unit instance.</summary>
		private static T GetUnitField<T>(Unit unit, string name) =>
			(T)typeof(Unit)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(unit);

		/// <summary>Invoke a private void method on a Unit with no parameters.</summary>
		private static void InvokeUnitMethod(Unit unit, string name) =>
			typeof(Unit)
				.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(unit, null);

		// ── Child-component finders ────────────────────────────────────────────────

		private static SpriteRenderer Indicator(Unit unit, string childName) =>
			unit.GetComponentsInChildren<SpriteRenderer>(includeInactive: true)
				.FirstOrDefault(sr => sr.gameObject.name == childName);

		private static Text TextField(Unit unit, string childName) =>
			unit.GetComponentsInChildren<Text>(includeInactive: true)
				.FirstOrDefault(t => t.name == childName);

		private static Canvas UnitCanvas(Unit unit) =>
			unit.GetComponentInChildren<Canvas>(includeInactive: true);

		private static LineRenderer PathLineRenderer(Unit unit) =>
			unit.GetComponent<LineRenderer>();

		private static GameObject TargetLineGo(Unit unit) =>
			unit.GetComponentsInChildren<Transform>(includeInactive: true)
				.FirstOrDefault(t => t.name == "TargetLine")?.gameObject;

		// ── EnableDebugging helper ─────────────────────────────────────────────────

		private void EnableUnitDebugging()
		{
			SetGmProperty("HasUnitDebugging", true);
			Unit.HasDebugging = true;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// ChangeColor
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator ChangeColor_UpdatesColorProperty()
		{
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			unit.ChangeColor(Color.red);

			Assert.AreEqual(Color.red, unit.Color,
				"ChangeColor should update the Color property");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// CanTrainUnit / CanBuildUnit
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator CanTrainUnit_BarracksSoldier_ReturnsTrue()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));

			Assert.IsTrue(barracks.CanTrainUnit(UnitType.SOLDIER),
				"BARRACKS should be able to train SOLDIER");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanTrainUnit_BarracksWorker_ReturnsFalse()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));

			Assert.IsFalse(barracks.CanTrainUnit(UnitType.WORKER),
				"BARRACKS should not be able to train WORKER");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanBuildUnit_WorkerBase_ReturnsTrue()
		{
			var worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			Assert.IsTrue(worker.CanBuildUnit(UnitType.BASE),
				"WORKER should be able to build BASE");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanBuildUnit_WorkerSoldier_ReturnsFalse()
		{
			var worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			Assert.IsFalse(worker.CanBuildUnit(UnitType.SOLDIER),
				"WORKER should not be able to build SOLDIER");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// CenterGridPosition
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator CenterGridPosition_1x1Unit_EqualsGridPosition()
		{
			var pos    = new Vector3Int(10, 10, 0);
			var worker = PlaceUnit(UnitType.WORKER, pos);

			Assert.AreEqual(pos, worker.CenterGridPosition,
				"1×1 unit CenterGridPosition should equal GridPosition");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CenterGridPosition_3x3Building_IsOffsetByOne()
		{
			var pos      = new Vector3Int(5, 15, 0);
			var baseUnit = PlaceUnit(UnitType.BASE, pos);
			var expected = new Vector3Int(pos.x + 1, pos.y - 1, 0);

			Assert.AreEqual(expected, baseUnit.CenterGridPosition,
				"3×3 building CenterGridPosition should be GridPosition + (1,−1)");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// LateUpdate / UpdateStateColor
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator LateUpdate_IdleUnit_AllIndicatorsDisabled()
		{
			SetGmProperty("HasAttackTint", true);
			SetGmProperty("HasMoveTint",   true);
			SetGmProperty("HasGatherTint", true);
			SetGmProperty("HasBuildTint",  true);
			var unit = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.IDLE;

			unit.LateUpdate();

			Assert.IsFalse(Indicator(unit, "AttackIndicator")?.enabled ?? false, "attack off");
			Assert.IsFalse(Indicator(unit, "MoveIndicator")?.enabled   ?? false, "move off");
			Assert.IsFalse(Indicator(unit, "GatherIndicator")?.enabled ?? false, "gather off");
			Assert.IsFalse(Indicator(unit, "BuildIndicator")?.enabled  ?? false, "build off");
			yield return null;
		}

		[UnityTest]
		public IEnumerator LateUpdate_AttackTintOn_AttackIndicatorEnabled()
		{
			SetGmProperty("HasAttackTint", true);
			var unit = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.ATTACK;

			unit.LateUpdate();

			Assert.IsTrue(Indicator(unit, "AttackIndicator")?.enabled,
				"AttackIndicator should be on when attacking with HasAttackTint=true");
			yield return null;
		}

		[UnityTest]
		public IEnumerator LateUpdate_AttackTintOff_AttackIndicatorDisabled()
		{
			SetGmProperty("HasAttackTint", false);
			var unit = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.ATTACK;

			unit.LateUpdate();

			Assert.IsFalse(Indicator(unit, "AttackIndicator")?.enabled ?? false,
				"AttackIndicator should be off when HasAttackTint=false");
			yield return null;
		}

		[UnityTest]
		public IEnumerator LateUpdate_MoveTintOn_MoveIndicatorEnabled()
		{
			SetGmProperty("HasMoveTint", true);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.MOVE;

			unit.LateUpdate();

			Assert.IsTrue(Indicator(unit, "MoveIndicator")?.enabled,
				"MoveIndicator should be on when moving with HasMoveTint=true");
			yield return null;
		}

		[UnityTest]
		public IEnumerator LateUpdate_GatherTintOn_GatherIndicatorEnabled()
		{
			SetGmProperty("HasGatherTint", true);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.GATHER;

			unit.LateUpdate();

			Assert.IsTrue(Indicator(unit, "GatherIndicator")?.enabled,
				"GatherIndicator should be on when gathering with HasGatherTint=true");
			yield return null;
		}

		[UnityTest]
		public IEnumerator LateUpdate_BuildTintOn_BuildIndicatorEnabled()
		{
			SetGmProperty("HasBuildTint", true);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.BUILD;

			unit.LateUpdate();

			Assert.IsTrue(Indicator(unit, "BuildIndicator")?.enabled,
				"BuildIndicator should be on when building with HasBuildTint=true");
			yield return null;
		}

		[UnityTest]
		public IEnumerator LateUpdate_InsideMine_AllIndicatorsHidden()
		{
			SetGmProperty("HasAttackTint", true);
			SetGmProperty("HasMoveTint",   true);
			SetGmProperty("HasGatherTint", true);
			SetGmProperty("HasBuildTint",  true);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.GATHER;
			SetUnitField(unit, "isInsideMine", true);

			unit.LateUpdate();

			Assert.IsFalse(Indicator(unit, "AttackIndicator")?.enabled ?? false, "attack hidden in mine");
			Assert.IsFalse(Indicator(unit, "MoveIndicator")?.enabled   ?? false, "move hidden in mine");
			Assert.IsFalse(Indicator(unit, "GatherIndicator")?.enabled ?? false, "gather hidden in mine");
			Assert.IsFalse(Indicator(unit, "BuildIndicator")?.enabled  ?? false, "build hidden in mine");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// Update / UpdateDebuggingInfo
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator Update_DebuggingOff_CanvasDisabled()
		{
			// HasUnitDebugging is false by default in PlayModeTestHelper
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.IsFalse(UnitCanvas(unit)?.enabled,
				"Canvas should be disabled when HasDebugging is false");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_CanvasEnabled()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.IsTrue(UnitCanvas(unit)?.enabled,
				"Canvas should be enabled when HasDebugging is true");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Idle_StateVariableEmpty()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.IDLE;

			unit.Update();

			Assert.AreEqual("", TextField(unit, "State Variable")?.text,
				"State Variable should be empty for IDLE");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Move_StateVariableShowsPathCount()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.MOVE;
			// path is empty by default, so path.Count = 0

			unit.Update();

			Assert.AreEqual("0", TextField(unit, "State Variable")?.text,
				"State Variable for MOVE should show path.Count as a string");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Attack_StateVariableShowsDamage()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.ATTACK;
			// totalDamage defaults to 0.0f

			unit.Update();

			Assert.AreEqual("0.0", TextField(unit, "State Variable")?.text,
				"State Variable for ATTACK should show totalDamage (0.0)");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Build_StateVariableShowsTaskTime()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.BUILD;
			// taskTime defaults to 0.0f

			unit.Update();

			Assert.AreEqual("0.0", TextField(unit, "State Variable")?.text,
				"State Variable for BUILD should show taskTime (0.0)");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Gather_StateVariableShowsGold()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.GATHER;
			// totalGold defaults to 0

			unit.Update();

			Assert.AreEqual("0.0", TextField(unit, "State Variable")?.text,
				"State Variable for GATHER should show totalGold (0.0)");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Train_StateVariableShowsTaskTime()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));
			unit.IsBuilt = true;
			unit.CurrentAction = UnitAction.TRAIN;
			// taskTime defaults to 0.0f

			unit.Update();

			Assert.AreEqual("0.0", TextField(unit, "State Variable")?.text,
				"State Variable for TRAIN should show taskTime (0.0)");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_UnitNumber_ShowsUnitNbr()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.AreEqual(unit.UnitNbr.ToString(), TextField(unit, "Unit Number")?.text,
				"Unit Number text should show the unit's UnitNbr");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_HealthValue_ShowsHealth()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.AreEqual(unit.Health.ToString("0.0"), TextField(unit, "Health Value")?.text,
				"Health Value text should show the unit's current health");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// Update / UpdatePathVisualization
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator Update_HasPathTintOff_PathLineRendererEmpty()
		{
			SetGmProperty("HasPathTint", false);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			// Give the unit a path so we can verify it's hidden when tint is off
			SetUnitField(unit, "path", new List<Vector3Int> { new Vector3Int(6, 5, 0) });

			unit.Update();

			Assert.AreEqual(0, PathLineRenderer(unit)?.positionCount,
				"Path line should be hidden when HasPathTint is false");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_HasPathTintOn_EmptyPath_LineRendererEmpty()
		{
			SetGmProperty("HasPathTint", true);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			// path is empty by default

			unit.Update();

			Assert.AreEqual(0, PathLineRenderer(unit)?.positionCount,
				"Path line should have 0 positions when path is empty");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_HasPathTintOn_WithPath_LineRendererHasPositions()
		{
			SetGmProperty("HasPathTint", true);
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.MOVE;
			SetUnitField(unit, "path", new List<Vector3Int>
			{
				new Vector3Int(6, 5, 0),
				new Vector3Int(7, 5, 0),
			});

			unit.Update();

			// positionCount = path.Count + 1 (world position prepended)
			Assert.AreEqual(3, PathLineRenderer(unit)?.positionCount,
				"Path line should have path.Count+1 positions when path is non-empty");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_HasPathTintOn_AttackTintOff_AttackPathHidden()
		{
			SetGmProperty("HasPathTint",  true);
			SetGmProperty("HasAttackTint", false);
			var unit = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.ATTACK;
			SetUnitField(unit, "path", new List<Vector3Int> { new Vector3Int(6, 5, 0) });

			unit.Update();

			Assert.AreEqual(0, PathLineRenderer(unit)?.positionCount,
				"Attack pursuit path should be hidden when HasAttackTint is false");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// Update / UpdateTargetVisualization
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator Update_HasTargetLineTintOff_TargetLineHidden()
		{
			SetGmProperty("HasTargetLineTint", false);
			var attacker = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5,  5, 0));
			var target   = PlaceUnit(UnitType.SOLDIER, new Vector3Int(7, 7, 0), ctx.Agent1Go);
			attacker.CurrentAction = UnitAction.ATTACK;
			attacker.AttackUnit    = target;

			attacker.Update();

			Assert.IsFalse(TargetLineGo(attacker)?.activeSelf,
				"TargetLine should be inactive when HasTargetLineTint is false");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_HasTargetLineTintOn_NotAttacking_TargetLineHidden()
		{
			SetGmProperty("HasTargetLineTint", true);
			var unit = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.IDLE;

			unit.Update();

			Assert.IsFalse(TargetLineGo(unit)?.activeSelf,
				"TargetLine should be inactive when unit is not attacking");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_HasTargetLineTintOn_Attacking_TargetLineVisible()
		{
			SetGmProperty("HasTargetLineTint", true);
			var attacker = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5,  5, 0));
			var target   = PlaceUnit(UnitType.SOLDIER, new Vector3Int(7, 7, 0), ctx.Agent1Go);
			attacker.CurrentAction = UnitAction.ATTACK;
			attacker.AttackUnit    = target;

			attacker.Update();

			Assert.IsTrue(TargetLineGo(attacker)?.activeSelf,
				"TargetLine should be active when attacking a living target with tint on");
			yield return null;
		}

		// ══════════════════════════════════════════════════════════════════════════
		// MapVelocityToDirection — dead code exercised via reflection
		// ══════════════════════════════════════════════════════════════════════════

		[UnityTest]
		public IEnumerator MapVelocityToDirection_NullAnimator_DoesNotThrow()
		{
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			SetUnitField(unit, "animator", null);

			Assert.DoesNotThrow(() => InvokeUnitMethod(unit, "MapVelocityToDirection"),
				"MapVelocityToDirection with null animator should return early without throwing");
			yield return null;
		}

		[UnityTest]
		public IEnumerator MapVelocityToDirection_SouthVelocity_DoesNotThrow()
		{
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			// velocity.x ≈ 0, velocity.y ≈ 1 → first branch (south)
			SetUnitField(unit, "velocity", new Vector3(0f, 0.95f, 0f));

			Assert.DoesNotThrow(() => InvokeUnitMethod(unit, "MapVelocityToDirection"),
				"MapVelocityToDirection with south velocity should not throw");
			yield return null;
		}

		[UnityTest]
		public IEnumerator MapVelocityToDirection_DiagonalVelocity_DoesNotThrow()
		{
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			// velocity.x > 0.1, velocity.y ≈ 1 → second else-if branch
			SetUnitField(unit, "velocity", new Vector3(0.5f, 0.95f, 0f));

			Assert.DoesNotThrow(() => InvokeUnitMethod(unit, "MapVelocityToDirection"),
				"MapVelocityToDirection with diagonal velocity should not throw");
			yield return null;
		}
	}
}
