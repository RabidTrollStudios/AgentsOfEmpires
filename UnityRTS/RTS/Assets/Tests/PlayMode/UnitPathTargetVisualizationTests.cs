using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for Unit.Update path and target visualization:
	/// UpdatePathVisualization, UpdateTargetVisualization,
	/// and MapVelocityToDirection (exercised via reflection).
	/// </summary>
	[TestFixture]
	public class UnitPathTargetVisualizationTests : PlayModeTestBase
	{
		// ── Helpers ────────────────────────────────────────────────────────────

		private static void SetGmProperty(string name, object value) =>
			typeof(GameManager)
				.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, value);

		private static void SetUnitField(Unit unit, string name, object value) =>
			typeof(Unit)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(unit, value);

		private static void InvokeUnitMethod(Unit unit, string name) =>
			typeof(Unit)
				.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(unit, null);

		private static LineRenderer PathLineRenderer(Unit unit) =>
			unit.GetComponent<LineRenderer>();

		private static GameObject TargetLineGo(Unit unit) =>
			unit.GetComponentsInChildren<Transform>(includeInactive: true)
				.FirstOrDefault(t => t.name == "TargetLine")?.gameObject;

		// ── UpdatePathVisualization ────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_HasPathTintOff_PathLineRendererEmpty()
		{
			SetGmProperty("HasPathTint", false);
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			SetGmProperty("HasPathTint",   true);
			SetGmProperty("HasAttackTint", false);
			var unit = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.ATTACK;
			SetUnitField(unit, "path", new List<Vector3Int> { new Vector3Int(6, 5, 0) });

			unit.Update();

			Assert.AreEqual(0, PathLineRenderer(unit)?.positionCount,
				"Attack pursuit path should be hidden when HasAttackTint is false");
			yield return null;
		}

		// ── UpdateTargetVisualization ──────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_HasTargetLineTintOff_TargetLineHidden()
		{
			SetGmProperty("HasTargetLineTint", false);
			var attacker = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5,  5, 0));
			var target   = PlaceUnit(UnitType.WARRIOR, new Vector3Int(7, 7, 0), ctx.Agent1Go);
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
			var unit = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
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
			var attacker = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5,  5, 0));
			var target   = PlaceUnit(UnitType.WARRIOR, new Vector3Int(7, 7, 0), ctx.Agent1Go);
			attacker.CurrentAction = UnitAction.ATTACK;
			attacker.AttackUnit    = target;

			attacker.Update();

			Assert.IsTrue(TargetLineGo(attacker)?.activeSelf,
				"TargetLine should be active when attacking a living target with tint on");
			yield return null;
		}

		// ── MapVelocityToDirection (dead code, exercised via reflection) ───────

		[UnityTest]
		public IEnumerator MapVelocityToDirection_NullAnimator_DoesNotThrow()
		{
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			SetUnitField(unit, "animator", null);

			Assert.DoesNotThrow(() => InvokeUnitMethod(unit, "MapVelocityToDirection"),
				"MapVelocityToDirection with null animator should return early without throwing");
			yield return null;
		}

		[UnityTest]
		public IEnumerator MapVelocityToDirection_SouthVelocity_DoesNotThrow()
		{
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			SetUnitField(unit, "velocity", new Vector3(0f, 0.95f, 0f));

			Assert.DoesNotThrow(() => InvokeUnitMethod(unit, "MapVelocityToDirection"),
				"MapVelocityToDirection with south velocity should not throw");
			yield return null;
		}

		[UnityTest]
		public IEnumerator MapVelocityToDirection_DiagonalVelocity_DoesNotThrow()
		{
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			SetUnitField(unit, "velocity", new Vector3(0.5f, 0.95f, 0f));

			Assert.DoesNotThrow(() => InvokeUnitMethod(unit, "MapVelocityToDirection"),
				"MapVelocityToDirection with diagonal velocity should not throw");
			yield return null;
		}
	}
}
