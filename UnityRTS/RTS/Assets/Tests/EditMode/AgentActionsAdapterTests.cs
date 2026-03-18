using AgentSDK;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for AgentActionsAdapter: verifies that each method returns
	/// early (without throwing) when the requested unit ID is not found in UnitManager.
	/// These cover the null-guard branches in every adapter method.
	/// </summary>
	[TestFixture]
	public class AgentActionsAdapterTests
	{
		// ── Stub ─────────────────────────────────────────────────────────────────

		/// <summary>
		/// Minimal concrete Agent for testing — satisfies the three abstract methods.
		/// </summary>
		private class StubAgent : Agent
		{
			public override void InitializeMatch() { }
			public override void InitializeRound() { }
			public override void Learn() { }
		}

		// ── Fields ────────────────────────────────────────────────────────────────

		private GameObject          agentGo;
		private StubAgent           agent;
		private UnitManager         unitManager;
		private AgentActionsAdapter adapter;

		[SetUp]
		public void SetUp()
		{
			agentGo     = new GameObject("StubAgentGO");
			agent       = agentGo.AddComponent<StubAgent>();
			// mapManager and prefabs are only accessed by PlaceUnit/CreateUnit etc., not by GetUnit
			unitManager = new UnitManager(null, null);
			adapter     = new AgentActionsAdapter(agent, unitManager);
		}

		[TearDown]
		public void TearDown()
		{
			UnityEngine.Object.DestroyImmediate(agentGo);
		}

		// ── Move ─────────────────────────────────────────────────────────────────

		[Test]
		public void Move_UnknownUnit_DoesNotThrow()
		{
			Assert.DoesNotThrow(
				() => adapter.Move(999, new Position(5, 5)),
				"Move with an unknown unit ID should return early without throwing");
		}

		// ── Build ─────────────────────────────────────────────────────────────────

		[Test]
		public void Build_UnknownUnit_DoesNotThrow()
		{
			Assert.DoesNotThrow(
				() => adapter.Build(999, new Position(5, 5), UnitType.BASE),
				"Build with an unknown unit ID should return early without throwing");
		}

		// ── Gather ────────────────────────────────────────────────────────────────

		[Test]
		public void Gather_AllUnknownUnits_DoesNotThrow()
		{
			// All three GetUnit calls execute before the combined null check
			Assert.DoesNotThrow(
				() => adapter.Gather(999, 998, 997),
				"Gather with all unknown unit IDs should return early without throwing");
		}

		// ── Train ─────────────────────────────────────────────────────────────────

		[Test]
		public void Train_UnknownBuilding_DoesNotThrow()
		{
			Assert.DoesNotThrow(
				() => adapter.Train(999, UnitType.WARRIOR),
				"Train with an unknown building ID should return early without throwing");
		}

		// ── Attack ────────────────────────────────────────────────────────────────

		[Test]
		public void Attack_BothUnknownUnits_DoesNotThrow()
		{
			// Both GetUnit calls execute before the combined null check
			Assert.DoesNotThrow(
				() => adapter.Attack(999, 998),
				"Attack with both unknown unit IDs should return early without throwing");
		}

		// ── Repair ────────────────────────────────────────────────────────────────

		[Test]
		public void Repair_UnknownPawn_ReturnsUnitNotFound()
		{
			var result = adapter.Repair(999, 998);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		// ── Return value assertions ──────────────────────────────────────────────

		[Test]
		public void Move_UnknownUnit_ReturnsUnitNotFound()
		{
			var result = adapter.Move(999, new Position(5, 5));
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[Test]
		public void Build_UnknownUnit_ReturnsUnitNotFound()
		{
			var result = adapter.Build(999, new Position(5, 5), UnitType.BASE);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[Test]
		public void Gather_AllUnknown_ReturnsUnitNotFound()
		{
			var result = adapter.Gather(999, 998, 997);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[Test]
		public void Train_UnknownBuilding_ReturnsUnitNotFound()
		{
			var result = adapter.Train(999, UnitType.WARRIOR);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[Test]
		public void Attack_BothUnknown_ReturnsUnitNotFound()
		{
			var result = adapter.Attack(999, 998);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		// ── Cooldown tests ───────────────────────────────────────────────────────
		// In EditMode, Time.frameCount is always 0.
		// After a failure, cooldownExpiry[unit] = 0 + 15 = 15.
		// Next call: Time.frameCount (0) < 15 → ON_COOLDOWN.

		[Test]
		public void Move_AfterFailure_ReturnsOnCooldown()
		{
			adapter.Move(999, new Position(5, 5)); // fail → triggers cooldown
			var result = adapter.Move(999, new Position(5, 5));
			Assert.AreEqual(CommandResult.ON_COOLDOWN, result);
		}

		[Test]
		public void Build_AfterFailure_ReturnsOnCooldown()
		{
			adapter.Build(999, new Position(5, 5), UnitType.BASE);
			var result = adapter.Build(999, new Position(5, 5), UnitType.BASE);
			Assert.AreEqual(CommandResult.ON_COOLDOWN, result);
		}

		[Test]
		public void Gather_AfterFailure_ReturnsOnCooldown()
		{
			adapter.Gather(999, 998, 997);
			var result = adapter.Gather(999, 998, 997);
			Assert.AreEqual(CommandResult.ON_COOLDOWN, result);
		}

		[Test]
		public void Train_AfterFailure_ReturnsOnCooldown()
		{
			adapter.Train(999, UnitType.WARRIOR);
			var result = adapter.Train(999, UnitType.WARRIOR);
			Assert.AreEqual(CommandResult.ON_COOLDOWN, result);
		}

		[Test]
		public void Attack_AfterFailure_ReturnsOnCooldown()
		{
			adapter.Attack(999, 998);
			var result = adapter.Attack(999, 998);
			Assert.AreEqual(CommandResult.ON_COOLDOWN, result);
		}

		[Test]
		public void Repair_AfterFailure_ReturnsOnCooldown()
		{
			adapter.Repair(999, 998);
			var result = adapter.Repair(999, 998);
			Assert.AreEqual(CommandResult.ON_COOLDOWN, result);
		}
	}
}
