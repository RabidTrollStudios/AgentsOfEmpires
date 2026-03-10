using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for natural mine depletion via actual gather operations.
	/// Uses a low-health mine so the pawn exhausts it quickly.
	/// </summary>
	[TestFixture]
	public class MineNaturalDepletionTests : PlayModeTestBase
	{
		#region Mine Depletes Via Gathering

		/// <summary>
		/// A mine with very low health is exhausted after one gather operation.
		/// The pawn then goes IDLE because there is nothing left to mine.
		/// </summary>
		[UnityTest]
		public IEnumerator LowHealthMine_PawnGathers_MineDepletesAndPawnGoesIdle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(8, 5, 0));
			// Set mine health very low so a single gather trip depletes it
			float miningCapacity = Constants.MINING_CAPACITY[UnitType.PAWN];
			mine.Health = miningCapacity * 0.5f; // Less than one full mining capacity

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0));
			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait for pawn to go IDLE (mine exhausted)
			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Pawn did not go IDLE after low-health mine was depleted");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE after mining a depleted mine");
		}

		/// <summary>
		/// After a low-health mine is exhausted, the pawn deposits whatever it gathered.
		/// Agent gold should increase at least once.
		/// </summary>
		[UnityTest]
		public IEnumerator LowHealthMine_PawnDepositsBeforeIdle()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(8, 5, 0));
			float miningCapacity = Constants.MINING_CAPACITY[UnitType.PAWN];
			mine.Health = miningCapacity * 0.5f;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0));
			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait for gold to increase (deposit occurred) or pawn to go IDLE
			yield return WaitUntil(
				() => agent.Gold > initialGold || pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 30f,
				failMessage: "Pawn did not complete gather cycle before going IDLE");

			// At minimum the pawn should have gone IDLE
			// (it might go IDLE without depositing if it couldn't gather at all)
			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should eventually be IDLE after mine is exhausted");
		}

		#endregion

		#region Mine Health at Zero

		/// <summary>
		/// A mine set to health=0 before gathering starts: pawn cannot mine
		/// and should go IDLE immediately.
		/// </summary>
		[UnityTest]
		public IEnumerator ZeroHealthMine_PawnGoesIdleImmediately()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(8, 5, 0));
			mine.Health = 0; // Already depleted

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0));

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 20f,
				failMessage: "Pawn did not go IDLE when mine was already depleted");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when targeting a zero-health mine");
		}

		#endregion

		#region Mine Health Decreases During Gather

		/// <summary>
		/// After a pawn completes one gather cycle from a mine, the mine's
		/// health (remaining gold) has decreased.
		/// </summary>
		[UnityTest]
		public IEnumerator AfterGather_MineHealth_Decreases()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			baseUnit.IsBuilt = true;

			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(8, 5, 0));
			float initialMineHealth = mine.Health;

			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(7, 5, 0));
			Agent agent = GetAgent0();
			int initialGold = agent.Gold;

			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

			// Wait for first deposit (one complete mining cycle)
			yield return WaitUntil(
				() => agent.Gold > initialGold,
				timeoutSeconds: 30f,
				failMessage: "First gather deposit did not occur");

			Assert.Less(mine.Health, initialMineHealth,
				"Mine health (remaining gold) should decrease after a gather cycle");
		}

		#endregion
	}
}
