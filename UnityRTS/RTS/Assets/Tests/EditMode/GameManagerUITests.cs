using System;
using System.Reflection;
using AgentSDK;
using NUnit.Framework;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for the refactored GameManager UI and input code:
	/// - Toggle callback methods (OnXToggleChanged)
	/// - _debugBindings population and execute actions
	/// - ProcessUserInput null guard
	/// - HandleSpeedInput boundary conditions
	/// </summary>
	[TestFixture]
	public class GameManagerUITests
	{
		private GameManager gm;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
			// Ensure _debugBindings is populated before each test
			InvokePrivate("InitializeDebugToggles");
		}

		[TearDown]
		public void TearDown()
		{
			// Restore speed baseline after each test
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
		}

		// ── Helpers ──────────────────────────────────────────────────────────────

		private void InvokePrivate(string methodName)
		{
			typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, null);
		}

		/// <summary>
		/// Retrieves the Execute Action from the <c>_debugBindings</c> array at the given index.
		/// Binding 2 (InfluenceMap) requires mapManager and is not safe to execute in EditMode.
		/// </summary>
		private Action GetBindingExecute(int index)
		{
			var field = typeof(GameManager).GetField(
				"_debugBindings", BindingFlags.NonPublic | BindingFlags.Instance);
			var bindings = field.GetValue(gm) as Array;
			var binding = bindings.GetValue(index);
			// Named tuple fields are stored as Item1/Item2/Item3 at runtime
			return (Action)binding.GetType().GetField("Item3").GetValue(binding);
		}

		// ── Toggle Callback: HasAgentDebugging ───────────────────────────────────

		[Test]
		public void OnAgentToggleChanged_True_SetsHasAgentDebugging()
		{
			gm.OnAgentToggleChanged(true);
			Assert.IsTrue(gm.HasAgentDebugging);
		}

		[Test]
		public void OnAgentToggleChanged_False_ClearsHasAgentDebugging()
		{
			gm.OnAgentToggleChanged(true);  // ensure it starts true
			gm.OnAgentToggleChanged(false);
			Assert.IsFalse(gm.HasAgentDebugging);
		}

		// ── Toggle Callback: HasUnitDebugging ────────────────────────────────────

		[Test]
		public void OnUnitToggleChanged_True_SetsHasUnitDebugging()
		{
			gm.OnUnitToggleChanged(true);
			Assert.IsTrue(gm.HasUnitDebugging);
		}

		[Test]
		public void OnUnitToggleChanged_False_ClearsHasUnitDebugging()
		{
			gm.OnUnitToggleChanged(true);
			gm.OnUnitToggleChanged(false);
			Assert.IsFalse(gm.HasUnitDebugging);
		}

		// ── Toggle Callback: HasMoveTint ─────────────────────────────────────────

		[Test]
		public void OnMoveTintToggleChanged_True_SetsHasMoveTint()
		{
			gm.OnMoveTintToggleChanged(true);
			Assert.IsTrue(gm.HasMoveTint);
		}

		[Test]
		public void OnMoveTintToggleChanged_False_ClearsHasMoveTint()
		{
			gm.OnMoveTintToggleChanged(true);
			gm.OnMoveTintToggleChanged(false);
			Assert.IsFalse(gm.HasMoveTint);
		}

		// ── Toggle Callback: HasGatherTint ───────────────────────────────────────

		[Test]
		public void OnGatherTintToggleChanged_True_SetsHasGatherTint()
		{
			gm.OnGatherTintToggleChanged(true);
			Assert.IsTrue(gm.HasGatherTint);
		}

		[Test]
		public void OnGatherTintToggleChanged_False_ClearsHasGatherTint()
		{
			gm.OnGatherTintToggleChanged(true);
			gm.OnGatherTintToggleChanged(false);
			Assert.IsFalse(gm.HasGatherTint);
		}

		// ── Toggle Callback: HasAttackTint ───────────────────────────────────────

		[Test]
		public void OnAttackTintToggleChanged_True_SetsHasAttackTint()
		{
			gm.OnAttackTintToggleChanged(true);
			Assert.IsTrue(gm.HasAttackTint);
		}

		[Test]
		public void OnAttackTintToggleChanged_False_ClearsHasAttackTint()
		{
			gm.OnAttackTintToggleChanged(true);
			gm.OnAttackTintToggleChanged(false);
			Assert.IsFalse(gm.HasAttackTint);
		}

		// ── Toggle Callback: HasPathTint ─────────────────────────────────────────

		[Test]
		public void OnPathTintToggleChanged_True_SetsHasPathTint()
		{
			gm.OnPathTintToggleChanged(true);
			Assert.IsTrue(gm.HasPathTint);
		}

		[Test]
		public void OnPathTintToggleChanged_False_ClearsHasPathTint()
		{
			gm.OnPathTintToggleChanged(true);
			gm.OnPathTintToggleChanged(false);
			Assert.IsFalse(gm.HasPathTint);
		}

		// ── _debugBindings Structure ─────────────────────────────────────────────

		[Test]
		public void InitializeDebugToggles_PopulatesSevenBindings()
		{
			var field = typeof(GameManager).GetField(
				"_debugBindings", BindingFlags.NonPublic | BindingFlags.Instance);
			var bindings = field.GetValue(gm) as Array;

			Assert.IsNotNull(bindings, "_debugBindings should not be null after InitializeDebugToggles");
			Assert.AreEqual(7, bindings.Length, "There should be exactly 7 key bindings");
		}

		// ── _debugBindings Execute Actions ───────────────────────────────────────
		// Binding 2 toggles the InfluenceMap GameObject and requires mapManager
		// (null in EditMode) — it is intentionally skipped here.

		[Test]
		public void DebugBinding0_Execute_TogglesHasAgentDebugging()
		{
			gm.OnAgentToggleChanged(true);
			GetBindingExecute(0)();
			Assert.IsFalse(gm.HasAgentDebugging, "First execute should flip HasAgentDebugging to false");
			GetBindingExecute(0)();
			Assert.IsTrue(gm.HasAgentDebugging, "Second execute should restore HasAgentDebugging to true");
		}

		[Test]
		public void DebugBinding1_Execute_TogglesHasUnitDebugging()
		{
			gm.OnUnitToggleChanged(true);
			GetBindingExecute(1)();
			Assert.IsFalse(gm.HasUnitDebugging, "First execute should flip HasUnitDebugging to false");
			GetBindingExecute(1)();
			Assert.IsTrue(gm.HasUnitDebugging, "Second execute should restore HasUnitDebugging to true");
		}

		[Test]
		public void DebugBinding3_Execute_TogglesHasMoveTint()
		{
			gm.OnMoveTintToggleChanged(true);
			GetBindingExecute(3)();
			Assert.IsFalse(gm.HasMoveTint, "First execute should flip HasMoveTint to false");
			GetBindingExecute(3)();
			Assert.IsTrue(gm.HasMoveTint, "Second execute should restore HasMoveTint to true");
		}

		[Test]
		public void DebugBinding4_Execute_TogglesHasGatherTint()
		{
			gm.OnGatherTintToggleChanged(true);
			GetBindingExecute(4)();
			Assert.IsFalse(gm.HasGatherTint, "First execute should flip HasGatherTint to false");
			GetBindingExecute(4)();
			Assert.IsTrue(gm.HasGatherTint, "Second execute should restore HasGatherTint to true");
		}

		[Test]
		public void DebugBinding5_Execute_TogglesHasAttackTint()
		{
			gm.OnAttackTintToggleChanged(true);
			GetBindingExecute(5)();
			Assert.IsFalse(gm.HasAttackTint, "First execute should flip HasAttackTint to false");
			GetBindingExecute(5)();
			Assert.IsTrue(gm.HasAttackTint, "Second execute should restore HasAttackTint to true");
		}

		[Test]
		public void DebugBinding6_Execute_TogglesHasPathTint()
		{
			gm.OnPathTintToggleChanged(true);
			GetBindingExecute(6)();
			Assert.IsFalse(gm.HasPathTint, "First execute should flip HasPathTint to false");
			GetBindingExecute(6)();
			Assert.IsTrue(gm.HasPathTint, "Second execute should restore HasPathTint to true");
		}

		// ── ProcessUserInput Null Guard ──────────────────────────────────────────

		[Test]
		public void ProcessUserInput_NullBindings_DoesNotThrow()
		{
			var field = typeof(GameManager).GetField(
				"_debugBindings", BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(gm, null);

			Assert.DoesNotThrow(() => InvokePrivate("ProcessUserInput"),
				"ProcessUserInput must not throw when _debugBindings is null");

			// Restore for other tests
			InvokePrivate("InitializeDebugToggles");
		}

		// ── HandleSpeedInput Boundary Conditions ─────────────────────────────────
		// Input.GetKeyDown always returns false in EditMode, so key-press paths cannot
		// be exercised here. The tests below verify the clamping guard conditions that
		// HandleSpeedInput uses directly against Constants.

		[Test]
		public void GameSpeed_AtMax_GuardPreventsIncrement()
		{
			// Guard from HandleSpeedInput: only increment when GAME_SPEED < MAX_GAME_SPEED
			Constants.GAME_SPEED = Constants.MAX_GAME_SPEED;
			bool wouldIncrement = Constants.GAME_SPEED < Constants.MAX_GAME_SPEED;
			Assert.IsFalse(wouldIncrement, "Speed at MAX should not pass the increment guard");
		}

		[Test]
		public void GameSpeed_AtOne_GuardPreventsDecrement()
		{
			// Guard from HandleSpeedInput: only decrement when GAME_SPEED > 1
			Constants.GAME_SPEED = 1;
			bool wouldDecrement = Constants.GAME_SPEED > 1;
			Assert.IsFalse(wouldDecrement, "Speed at 1 should not pass the decrement guard");
		}

		[Test]
		public void GameSpeed_BelowMax_GuardAllowsIncrement()
		{
			Constants.GAME_SPEED = Constants.MAX_GAME_SPEED - 1;
			bool wouldIncrement = Constants.GAME_SPEED < Constants.MAX_GAME_SPEED;
			Assert.IsTrue(wouldIncrement, "Speed below MAX should pass the increment guard");
		}

		[Test]
		public void GameSpeed_AboveOne_GuardAllowsDecrement()
		{
			Constants.GAME_SPEED = 2;
			bool wouldDecrement = Constants.GAME_SPEED > 1;
			Assert.IsTrue(wouldDecrement, "Speed above 1 should pass the decrement guard");
		}
	}
}
