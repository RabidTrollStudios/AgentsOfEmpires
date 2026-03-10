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
	/// PlayMode tests for Unit.Update / UpdateDebuggingInfo:
	/// canvas enabled state and the State Variable text for each unit action.
	/// </summary>
	[TestFixture]
	public class UnitDebuggingInfoTests : PlayModeTestBase
	{
		// ── Helpers ────────────────────────────────────────────────────────────

		private static void SetGmProperty(string name, object value) =>
			typeof(GameManager)
				.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
				.SetValue(GameManager.Instance, value);

		private static Text TextField(Unit unit, string childName) =>
			unit.GetComponentsInChildren<Text>(includeInactive: true)
				.FirstOrDefault(t => t.name == childName);

		private static Canvas UnitCanvas(Unit unit) =>
			unit.GetComponentInChildren<Canvas>(includeInactive: true);

		private void EnableUnitDebugging()
		{
			SetGmProperty("HasUnitDebugging", true);
			Unit.HasDebugging = true;
		}

		// ── Canvas enabled state ───────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_DebuggingOff_CanvasDisabled()
		{
			// HasUnitDebugging is false by default in PlayModeTestBase
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.IsFalse(UnitCanvas(unit)?.enabled,
				"Canvas should be disabled when HasDebugging is false");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_CanvasEnabled()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.IsTrue(UnitCanvas(unit)?.enabled,
				"Canvas should be enabled when HasDebugging is true");
			yield return null;
		}

		// ── State Variable text per action ─────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_DebuggingOn_Idle_StateVariableEmpty()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
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

		// ── Static debug fields ────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Update_DebuggingOn_UnitNumber_ShowsUnitNbr()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.AreEqual(unit.UnitNbr.ToString(), TextField(unit, "Unit Number")?.text,
				"Unit Number text should show the unit's UnitNbr");
			yield return null;
		}

		[UnityTest]
		public IEnumerator Update_DebuggingOn_HealthValue_ShowsHealth()
		{
			EnableUnitDebugging();
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			unit.Update();

			Assert.AreEqual(unit.Health.ToString("0.0"), TextField(unit, "Health Value")?.text,
				"Health Value text should show the unit's current health");
			yield return null;
		}
	}
}
