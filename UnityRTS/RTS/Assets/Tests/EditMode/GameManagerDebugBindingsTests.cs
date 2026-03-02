using System;
using System.Reflection;
using NUnit.Framework;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for the GameManager _debugBindings array:
	/// - InitializeDebugToggles populates 9 bindings
	/// - Each binding's Execute action toggles the expected property
	/// - ProcessUserInput handles null bindings without throwing
	/// </summary>
	[TestFixture]
	public class GameManagerDebugBindingsTests
	{
		private GameManager gm;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
			InvokePrivate("InitializeDebugToggles");
		}

		[TearDown]
		public void TearDown()
		{
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
			// Ensure bindings are restored for other tests
			InvokePrivate("InitializeDebugToggles");
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		private void InvokePrivate(string methodName) =>
			typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, null);

		/// <summary>
		/// Retrieves the Execute Action from the _debugBindings array at the given index.
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

		// ── Structure ─────────────────────────────────────────────────────────

		[Test]
		public void InitializeDebugToggles_PopulatesNineBindings()
		{
			var field = typeof(GameManager).GetField(
				"_debugBindings", BindingFlags.NonPublic | BindingFlags.Instance);
			var bindings = field.GetValue(gm) as Array;

			Assert.IsNotNull(bindings, "_debugBindings should not be null after InitializeDebugToggles");
			Assert.AreEqual(9, bindings.Length, "There should be exactly 9 key bindings");
		}

		// ── Execute Actions ───────────────────────────────────────────────────
		// Binding 2 toggles the InfluenceMap GameObject and requires mapManager
		// (null in EditMode) — intentionally skipped here.
		// Key layout: 0=Agent, 1=Unit, 2=Influence, 3=Move, 4=Gather,
		//             5=Build, 6=Attack, 7=Path, 8=TargetLine

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
		public void DebugBinding5_Execute_TogglesHasBuildTint()
		{
			gm.OnBuildTintToggleChanged(true);
			GetBindingExecute(5)();
			Assert.IsFalse(gm.HasBuildTint, "First execute should flip HasBuildTint to false");
			GetBindingExecute(5)();
			Assert.IsTrue(gm.HasBuildTint, "Second execute should restore HasBuildTint to true");
		}

		[Test]
		public void DebugBinding6_Execute_TogglesHasAttackTint()
		{
			gm.OnAttackTintToggleChanged(true);
			GetBindingExecute(6)();
			Assert.IsFalse(gm.HasAttackTint, "First execute should flip HasAttackTint to false");
			GetBindingExecute(6)();
			Assert.IsTrue(gm.HasAttackTint, "Second execute should restore HasAttackTint to true");
		}

		[Test]
		public void DebugBinding7_Execute_TogglesHasPathTint()
		{
			gm.OnPathTintToggleChanged(true);
			GetBindingExecute(7)();
			Assert.IsFalse(gm.HasPathTint, "First execute should flip HasPathTint to false");
			GetBindingExecute(7)();
			Assert.IsTrue(gm.HasPathTint, "Second execute should restore HasPathTint to true");
		}

		[Test]
		public void DebugBinding8_Execute_TogglesHasTargetLineTint()
		{
			gm.OnTargetLineTintToggleChanged(true);
			GetBindingExecute(8)();
			Assert.IsFalse(gm.HasTargetLineTint, "First execute should flip HasTargetLineTint to false");
			GetBindingExecute(8)();
			Assert.IsTrue(gm.HasTargetLineTint, "Second execute should restore HasTargetLineTint to true");
		}

		// ── ProcessUserInput null guard ───────────────────────────────────────

		[Test]
		public void ProcessUserInput_NullBindings_DoesNotThrow()
		{
			var field = typeof(GameManager).GetField(
				"_debugBindings", BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(gm, null);

			Assert.DoesNotThrow(() => InvokePrivate("ProcessUserInput"),
				"ProcessUserInput must not throw when _debugBindings is null");
		}
	}
}
