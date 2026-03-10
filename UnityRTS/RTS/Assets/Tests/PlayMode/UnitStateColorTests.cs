using System.Collections;
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
	/// PlayMode tests for Unit.LateUpdate / UpdateStateColor:
	/// verifies that tint indicators are enabled/disabled correctly
	/// based on the unit's current action and the GameManager tint flags.
	/// </summary>
	[TestFixture]
	public class UnitStateColorTests : PlayModeTestBase
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

		private static SpriteRenderer Indicator(Unit unit, string childName) =>
			unit.GetComponentsInChildren<SpriteRenderer>(includeInactive: true)
				.FirstOrDefault(sr => sr.gameObject.name == childName);

		// ── Tests ──────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator LateUpdate_IdleUnit_AllIndicatorsDisabled()
		{
			SetGmProperty("HasAttackTint", true);
			SetGmProperty("HasMoveTint",   true);
			SetGmProperty("HasGatherTint", true);
			SetGmProperty("HasBuildTint",  true);
			var unit = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			unit.CurrentAction = UnitAction.GATHER;
			SetUnitField(unit, "isInsideMine", true);

			unit.LateUpdate();

			Assert.IsFalse(Indicator(unit, "AttackIndicator")?.enabled ?? false, "attack hidden in mine");
			Assert.IsFalse(Indicator(unit, "MoveIndicator")?.enabled   ?? false, "move hidden in mine");
			Assert.IsFalse(Indicator(unit, "GatherIndicator")?.enabled ?? false, "gather hidden in mine");
			Assert.IsFalse(Indicator(unit, "BuildIndicator")?.enabled  ?? false, "build hidden in mine");
			yield return null;
		}
	}
}
