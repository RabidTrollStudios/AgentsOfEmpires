using System.Collections;
using System.IO;
using System.Reflection;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for AgentActionsAdapter delegation paths.
	/// Exercises the lines that call through to Agent commands when a valid unit is found.
	/// </summary>
	[TestFixture]
	public class AgentActionsAdapterTests : PlayModeTestBase
	{
		// ── Move ─────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Move_ValidUnit_DelegatesToAgentMove()
		{
			var unit    = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Move(unit.UnitNbr, new Position(7, 7)),
				"Move with a valid unit should delegate to agent.Move without throwing");

			yield return null;
		}

		// ── Build ─────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Build_ValidPawn_DelegatesToAgentBuild()
		{
			var pawn  = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Build(pawn.UnitNbr, new Position(7, 7), UnitType.BASE),
				"Build with a valid unit should delegate to agent.Build without throwing");

			yield return null;
		}

		// ── Gather ────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_ValidUnits_DelegatesToAgentGather()
		{
			var pawn   = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(7, 7, 0));
			var baseUnit = PlaceUnit(UnitType.BASE,   new Vector3Int(3, 3, 0));
			var adapter  = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Gather(pawn.UnitNbr, mine.UnitNbr, baseUnit.UnitNbr),
				"Gather with valid units should delegate to agent.Gather without throwing");

			yield return null;
		}

		// ── Train ─────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Train_ValidBuilding_DelegatesToAgentTrain()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));
			var adapter  = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Train(barracks.UnitNbr, UnitType.WARRIOR),
				"Train with a valid building should delegate to agent.Train without throwing");

			yield return null;
		}

		// ── Attack ────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Attack_ValidUnits_DelegatesToAgentAttack()
		{
			var attacker = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0), ctx.Agent0Go);
			var enemy    = PlaceUnit(UnitType.WARRIOR, new Vector3Int(7, 7, 0), ctx.Agent1Go);
			var adapter  = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Attack(attacker.UnitNbr, enemy.UnitNbr),
				"Attack with valid units from different agents should delegate without throwing");

			yield return null;
		}

		// ── Repair ────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Repair_ValidUnits_DelegatesToAgentRepair()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(8, 8, 0));
			baseUnit.IsBuilt = true;
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Repair(pawn.UnitNbr, baseUnit.UnitNbr),
				"Repair with valid units should delegate to agent.Repair without throwing");

			yield return null;
		}

		[UnityTest]
		public IEnumerator Repair_UnknownPawn_ReturnsUnitNotFound()
		{
			var baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(8, 8, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);
			yield return null;

			var result = adapter.Repair(999, baseUnit.UnitNbr);
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Repair_UnknownBuilding_ReturnsTargetNotFound()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);
			yield return null;

			var result = adapter.Repair(pawn.UnitNbr, 999);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		// ── Target not found (unit valid, target missing) ─────────────────────────

		[UnityTest]
		public IEnumerator Gather_ValidPawn_UnknownMine_ReturnsTargetNotFound()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);
			yield return null;

			var result = adapter.Gather(pawn.UnitNbr, 999, 998);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		[UnityTest]
		public IEnumerator Attack_ValidUnit_UnknownTarget_ReturnsTargetNotFound()
		{
			var warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);
			yield return null;

			var result = adapter.Attack(warrior.UnitNbr, 999);
			Assert.AreEqual(CommandResult.TARGET_NOT_FOUND, result);
		}

		// ── Cooldown expiry ───────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Move_CooldownExpires_AllowsRetry()
		{
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);
			yield return null;

			// First call with unknown unit → UNIT_NOT_FOUND, triggers cooldown
			adapter.Move(999, new Position(5, 5));
			// Immediate retry → ON_COOLDOWN
			Assert.AreEqual(CommandResult.ON_COOLDOWN,
				adapter.Move(999, new Position(5, 5)));

			// Advance past cooldown (BASE_COOLDOWN_FRAMES = 15)
			yield return WaitFrames(20);

			// Now the cooldown should have expired — gets UNIT_NOT_FOUND again, not ON_COOLDOWN
			var result = adapter.Move(999, new Position(5, 5));
			Assert.AreEqual(CommandResult.UNIT_NOT_FOUND, result,
				"After cooldown expires, command should be processed again");
		}

		// ── Log ───────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Log_DelegatesToAgentLog()
		{
			// Inject a temp FileStream so agent.Log has a valid stream to write to
			string tempPath = Path.GetTempFileName();
			var fs = File.Open(tempPath, FileMode.Append);
			typeof(Agent)
				.GetProperty("LogFileStream", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(GetAgent0(), fs);

			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);
			try
			{
				Assert.DoesNotThrow(
					() => adapter.Log("test message"),
					"Log should delegate to agent.Log without throwing");
			}
			finally
			{
				fs.Close();
				File.Delete(tempPath);
			}

			yield return null;
		}
	}
}
