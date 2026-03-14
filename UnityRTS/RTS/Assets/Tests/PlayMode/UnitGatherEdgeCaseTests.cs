using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for gather edge cases:
	/// mine depletion while mining, base destruction during gather,
	/// and TO_BASE re-pathing when not yet arrived.
	/// </summary>
	[TestFixture]
	public class UnitGatherEdgeCaseTests : PlayModeTestBase
	{
		private Unit PlaceBuiltBase(Vector3Int position)
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, position);
			baseUnit.IsBuilt = true;
			return baseUnit;
		}

		private void StartGathering(Unit pawn, Unit mine, Unit baseUnit) =>
			pawn.StartGathering(new GatherEventArgs(pawn, mine, baseUnit));

		#region Mine Depletion During Mining

		/// <summary>
		/// When the mine depletes (health drops to 0) while a pawn is in MINING phase,
		/// the pawn should switch to TO_BASE to deposit whatever gold it has.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineDepletedDuringMining_PawnGoesToBase()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(10, 5, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 5, 0));

			StartGathering(pawn, mine, baseUnit);

			// Wait until the pawn is mining (has arrived at mine and is extracting)
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				// Pawn is gathering and mine health is decreasing
				return pawn.CurrentAction == UnitAction.GATHER && mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Deplete the mine
			mine.Health = 0;
			mine.Update();
			yield return WaitFrames(2);

			// Tick the pawn — it should detect the mine is gone
			for (int i = 0; i < 10; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			// Pawn should either still be gathering (heading to base) or have deposited and gone idle
			// The key behavior: pawn doesn't crash and eventually returns to IDLE
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 15f, failMessage: "Pawn should eventually go IDLE after mine depleted");
		}

		#endregion

		#region Base Destroyed During Gather

		/// <summary>
		/// When the base is destroyed while the pawn is heading to the mine,
		/// the gather cycle should eventually stop (pawn goes IDLE when it
		/// tries to deposit and finds no base).
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_BaseDestroyedDuringGather_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(15, 5, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 5, 0));

			StartGathering(pawn, mine, baseUnit);
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);

			// Let the pawn gather for a bit, wait for it to start mining
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return mine.Health < Constants.HEALTH[UnitType.MINE];
			}, timeoutSeconds: 15f, failMessage: "Pawn should start mining");

			// Destroy the base
			baseUnit.Health = 0;
			baseUnit.Update();
			yield return WaitFrames(2);

			// Tick enough for the pawn to try to deposit and fail
			yield return WaitUntil(() =>
			{
				BuildingTestHelper.Tick(pawn);
				return pawn.CurrentAction == UnitAction.IDLE;
			}, timeoutSeconds: 20f, failMessage: "Pawn should go IDLE when base is destroyed and can't deposit");
		}

		#endregion

		#region Mine Destroyed While Heading To Mine

		/// <summary>
		/// If the mine is destroyed while the pawn is in TO_MINE phase,
		/// the pawn should immediately go IDLE.
		/// </summary>
		[UnityTest]
		public IEnumerator Gather_MineDestroyedWhileHeadingToMine_PawnGoesIdle()
		{
			Unit baseUnit = PlaceBuiltBase(new Vector3Int(5, 5, 0));
			// Place mine far away so the pawn has time to walk
			Unit mine = PlaceUnit(UnitType.MINE, new Vector3Int(25, 5, 0));
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 5, 0));

			StartGathering(pawn, mine, baseUnit);
			Assert.AreEqual(UnitAction.GATHER, pawn.CurrentAction);

			// Tick a few frames so pawn is moving toward the mine
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			// Destroy the mine
			mine.Health = 0;
			mine.Update();
			yield return WaitFrames(2);

			// Pawn should detect mine is gone during TO_MINE phase
			for (int i = 0; i < 5; i++)
			{
				BuildingTestHelper.Tick(pawn);
				yield return null;
			}

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should go IDLE when mine is destroyed during TO_MINE phase");
		}

		#endregion
	}
}
