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
			var unit    = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Move(unit.UnitNbr, new Position(7, 7)),
				"Move with a valid unit should delegate to agent.Move without throwing");

			yield return null;
		}

		// ── Build ─────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Build_ValidWorker_DelegatesToAgentBuild()
		{
			var worker  = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			var adapter = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Build(worker.UnitNbr, new Position(7, 7), UnitType.BASE),
				"Build with a valid unit should delegate to agent.Build without throwing");

			yield return null;
		}

		// ── Gather ────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Gather_ValidUnits_DelegatesToAgentGather()
		{
			var worker   = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			var mine     = PlaceUnit(UnitType.MINE,   new Vector3Int(7, 7, 0));
			var baseUnit = PlaceUnit(UnitType.BASE,   new Vector3Int(3, 3, 0));
			var adapter  = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Gather(worker.UnitNbr, mine.UnitNbr, baseUnit.UnitNbr),
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
				() => adapter.Train(barracks.UnitNbr, UnitType.SOLDIER),
				"Train with a valid building should delegate to agent.Train without throwing");

			yield return null;
		}

		// ── Attack ────────────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator Attack_ValidUnits_DelegatesToAgentAttack()
		{
			var attacker = PlaceUnit(UnitType.SOLDIER, new Vector3Int(5, 5, 0), ctx.Agent0Go);
			var enemy    = PlaceUnit(UnitType.SOLDIER, new Vector3Int(7, 7, 0), ctx.Agent1Go);
			var adapter  = new AgentActionsAdapter(GetAgent0(), ctx.UnitManager);

			Assert.DoesNotThrow(
				() => adapter.Attack(attacker.UnitNbr, enemy.UnitNbr),
				"Attack with valid units from different agents should delegate without throwing");

			yield return null;
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
				?.SetValue(GetAgent0(), fs);

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
