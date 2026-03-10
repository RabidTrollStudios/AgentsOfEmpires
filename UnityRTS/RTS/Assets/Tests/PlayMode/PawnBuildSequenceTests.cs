using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for sequential build workflows:
	/// a single pawn completing one structure and then starting another.
	/// </summary>
	[TestFixture]
	public class PawnBuildSequenceTests : PlayModeTestBase
	{
		#region Sequential Build: BASE then BARRACKS

		/// <summary>
		/// A pawn builds a BASE to completion and then immediately issues
		/// a BARRACKS build command. The second command is accepted (pawn enters BUILD).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_BuildsBase_ThenStartsBarracks()
		{
			Agent agent = GetAgent0();
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Vector3Int pawnPos = new Vector3Int(9, 10, 0);
			Vector3Int barracksPos = new Vector3Int(14, 10, 0);

			// Give agent enough gold for both structures
			agent.Gold = (int)(Constants.COST[UnitType.BASE] + Constants.COST[UnitType.BARRACKS] + 10);

			Unit pawn = PlaceUnit(UnitType.PAWN, pawnPos);
			pawn.StartBuilding(new BuildEventArgs(pawn, basePos, UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should enter BUILD for BASE");

			// Wait for BASE to be built
			Unit builtBase = BuildingTestHelper.FindNewestUnitOfType(ctx, UnitType.BASE);
			Assert.IsNotNull(builtBase, "BASE should have been created");

			yield return BuildingTestHelper.WaitForConstruction(pawn, builtBase,
				timeoutSeconds: 30f);

			Assert.IsTrue(builtBase.IsBuilt,
				"BASE should be fully built before starting BARRACKS");

			// Now issue BARRACKS build
			int goldAfterBase = agent.Gold;
			pawn.StartBuilding(new BuildEventArgs(pawn, barracksPos, UnitType.BARRACKS));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should enter BUILD for BARRACKS after completing BASE");
			Assert.Less(agent.Gold, goldAfterBase,
				"Gold should be deducted for the BARRACKS build");
		}

		/// <summary>
		/// After completing BASE construction, the pawn returns to IDLE before
		/// the second command is issued.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_AfterBaseComplete_ReturnsToIdle()
		{
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Vector3Int pawnPos = new Vector3Int(9, 10, 0);

			Unit pawn = PlaceUnit(UnitType.PAWN, pawnPos);
			pawn.StartBuilding(new BuildEventArgs(pawn, basePos, UnitType.BASE));

			Unit builtBase = BuildingTestHelper.FindNewestUnitOfType(ctx, UnitType.BASE);
			Assert.IsNotNull(builtBase, "BASE should have been created by the build command");

			yield return BuildingTestHelper.WaitForConstruction(pawn, builtBase,
				timeoutSeconds: 30f);

			// After construction, pawn should be IDLE
			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 10f,
				failMessage: "Pawn should return to IDLE after BASE construction completes");

			Assert.AreEqual(UnitAction.IDLE, pawn.CurrentAction,
				"Pawn should be IDLE after completing BASE build");
		}

		#endregion

		#region Two Pawns: Independent Structures

		/// <summary>
		/// Two pawns independently build two different BASE structures.
		/// Both should enter BUILD state with correct gold deductions.
		/// </summary>
		[UnityTest]
		public IEnumerator TwoPawns_BuildIndependentBases_BothStart()
		{
			Agent agent = GetAgent0();
			int baseCost = (int)Constants.COST[UnitType.BASE];
			agent.Gold = baseCost * 2 + 20;
			int goldBefore = agent.Gold;

			Unit w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 5, 0));
			Unit w2 = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 15, 0));

			w1.StartBuilding(new BuildEventArgs(w1, new Vector3Int(10, 5, 0), UnitType.BASE));
			w2.StartBuilding(new BuildEventArgs(w2, new Vector3Int(10, 15, 0), UnitType.BASE));

			Assert.AreEqual(UnitAction.BUILD, w1.CurrentAction,
				"Pawn 1 should enter BUILD state");
			Assert.AreEqual(UnitAction.BUILD, w2.CurrentAction,
				"Pawn 2 should enter BUILD state");
			Assert.AreEqual(goldBefore - baseCost * 2, agent.Gold,
				"Gold should be deducted for both BASE builds");

			yield return null;
		}

		#endregion
	}
}
