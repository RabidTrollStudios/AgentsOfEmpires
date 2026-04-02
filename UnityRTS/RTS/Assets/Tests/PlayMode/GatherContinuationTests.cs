using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for gather continuation and cycle behavior:
	/// pawn cycling back to mine after deposit and handling mine depletion
	/// mid-cycle.
	/// </summary>
	[TestFixture]
	public class GatherContinuationTests : PlayModeTestBase
	{
		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		#region Happy Path – Cycle Continuation

		/// <summary>
		/// After the first deposit, the pawn automatically cycles back toward the mine
		/// (stays in GATHER, not IDLE).
		/// </summary>
		[UnityTest]
		public IEnumerator AfterDeposit_PawnCyclesBackToMine()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			AgentSDK.CommandProcessor.ProcessGather(pawn, mine.UnitNbr, baseUnit.UnitNbr, GameManager.Instance.GetTickWorld());

			// Wait for first deposit
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 10f,
				failMessage: "First deposit did not occur");

			// Pawn should still be in GATHER action
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction,
				"Pawn should continue gathering after first deposit");
		}

		/// <summary>
		/// Gold accumulates monotonically across multiple gather trips.
		/// </summary>
		[UnityTest]
		public IEnumerator MultipleCycles_GoldMonotonicallyIncreases()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int lastGold = agent.Gold;
			AgentSDK.CommandProcessor.ProcessGather(pawn, mine.UnitNbr, baseUnit.UnitNbr, GameManager.Instance.GetTickWorld());

			// Wait for 3 deposits
			int depositCount = 0;
			yield return WaitUntil(() =>
			{
				if (agent.Gold > lastGold)
				{
					Assert.Greater(agent.Gold, lastGold,
						"Gold should only ever increase between deposits");
					lastGold = agent.Gold;
					depositCount++;
				}
				return depositCount >= 3;
			}, timeoutSeconds: 15f, failMessage: "Did not complete 3 gather deposits");

			Assert.GreaterOrEqual(depositCount, 3,
				"Pawn should complete at least 3 gather cycles");
		}

		/// <summary>
		/// A second pawn starting to gather after the first has already started
		/// both eventually deposit gold.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoPawns_SequentialStart_BothDeposit()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));

			// Pawns must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn1 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 9, 0));
			Unit pawn2 = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			// Start first pawn
			AgentSDK.CommandProcessor.ProcessGather(pawn1, mine.UnitNbr, baseUnit.UnitNbr, GameManager.Instance.GetTickWorld());

			// Wait a few frames before starting second
			yield return WaitFrames(10);
			AgentSDK.CommandProcessor.ProcessGather(pawn2, mine.UnitNbr, baseUnit.UnitNbr, GameManager.Instance.GetTickWorld());

			// Wait for gold to increase by at least 2 mining capacities
			int targetGold = initialGold + (int)(Constants.MINING_CAPACITY[UnitType.PAWN] * 2);
			yield return WaitUntil(
				() => agent.Gold >= targetGold,
				timeoutSeconds: 15f,
				failMessage: "Two pawns did not together deposit enough gold");

			Assert.GreaterOrEqual(agent.Gold, targetGold,
				"Two pawns gathering sequentially should deposit combined gold");
		}

		#endregion

		#region Error – Mid-cycle Interruption

		/// <summary>
		/// If the mine is depleted (health = 0) during the second gather cycle,
		/// the pawn eventually goes IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Mine_DepletedDuringSecondCycle_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			AgentSDK.CommandProcessor.ProcessGather(pawn, mine.UnitNbr, baseUnit.UnitNbr, GameManager.Instance.GetTickWorld());

			// Wait for first deposit (one complete cycle)
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 10f,
				failMessage: "First gather cycle did not complete");

			// Deplete the mine on the second cycle
			mine.Health = 0;

			// Pawn should eventually go IDLE when it can't mine
			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Pawn did not go IDLE after mine was depleted");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE after mine is depleted mid-cycle");
		}

		/// <summary>
		/// If the base is destroyed mid-cycle while the pawn is heading back,
		/// the pawn eventually goes IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Base_DestroyedMidSecondCycle_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(20, 10, 0));
			// Pawn must be outside the BASE footprint (6x4: x=[5,10], y=[2,5])
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(12, 10, 0));

			Agent agent = GetAgent0();
			int initialGold = agent.Gold;
			AgentSDK.CommandProcessor.ProcessGather(pawn, mine.UnitNbr, baseUnit.UnitNbr, GameManager.Instance.GetTickWorld());

			// Wait for first deposit
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 20f,
				failMessage: "First gather deposit did not occur");

			// Destroy the base
			baseUnit.Health = 0;

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Pawn did not go IDLE after base was destroyed");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE when target base is destroyed");
		}

		#endregion
	}
}
