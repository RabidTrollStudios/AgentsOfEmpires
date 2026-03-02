using NUnit.Framework;
using System.Reflection;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for GameManager debug toggle callback methods
	/// (On*ToggleChanged) — verifies that each toggle sets and clears
	/// the corresponding public property on GameManager.
	/// </summary>
	[TestFixture]
	public class GameManagerToggleTests
	{
		private GameManager gm;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
		}

		// ── HasAgentDebugging ─────────────────────────────────────────────────

		[Test]
		public void OnAgentToggleChanged_True_SetsHasAgentDebugging()
		{
			gm.OnAgentToggleChanged(true);
			Assert.IsTrue(gm.HasAgentDebugging);
		}

		[Test]
		public void OnAgentToggleChanged_False_ClearsHasAgentDebugging()
		{
			gm.OnAgentToggleChanged(true);
			gm.OnAgentToggleChanged(false);
			Assert.IsFalse(gm.HasAgentDebugging);
		}

		// ── HasUnitDebugging ──────────────────────────────────────────────────

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

		// ── HasMoveTint ───────────────────────────────────────────────────────

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

		// ── HasGatherTint ─────────────────────────────────────────────────────

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

		// ── HasAttackTint ─────────────────────────────────────────────────────

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

		// ── HasPathTint ───────────────────────────────────────────────────────

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

		// ── HasBuildTint ──────────────────────────────────────────────────────

		[Test]
		public void OnBuildTintToggleChanged_True_SetsHasBuildTint()
		{
			gm.OnBuildTintToggleChanged(true);
			Assert.IsTrue(gm.HasBuildTint);
		}

		[Test]
		public void OnBuildTintToggleChanged_False_ClearsHasBuildTint()
		{
			gm.OnBuildTintToggleChanged(true);
			gm.OnBuildTintToggleChanged(false);
			Assert.IsFalse(gm.HasBuildTint);
		}

		// ── HasTargetLineTint ─────────────────────────────────────────────────

		[Test]
		public void OnTargetLineTintToggleChanged_True_SetsHasTargetLineTint()
		{
			gm.OnTargetLineTintToggleChanged(true);
			Assert.IsTrue(gm.HasTargetLineTint);
		}

		[Test]
		public void OnTargetLineTintToggleChanged_False_ClearsHasTargetLineTint()
		{
			gm.OnTargetLineTintToggleChanged(true);
			gm.OnTargetLineTintToggleChanged(false);
			Assert.IsFalse(gm.HasTargetLineTint);
		}
	}
}
