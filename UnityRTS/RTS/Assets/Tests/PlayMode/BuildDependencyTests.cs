using System.Collections;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for build dependency enforcement.
	/// BARRACKS requires a BASE to be built first.
	/// Tests verify that build commands are rejected before the dependency
	/// is satisfied, and accepted afterward.
	/// </summary>
	[TestFixture]
	public class BuildDependencyTests : PlayModeTestBase
	{
		private void TickUnit(Unit unit)
		{
			unit.FixedUpdate();
			unit.Update();
		}

		#region Happy Path

		/// <summary>
		/// After building a BASE, a pawn can then build a BARRACKS.
		/// </summary>
		[UnityTest]
		public IEnumerator AfterBase_PawnCanBuildBarracks()
		{
			// Place and complete a BASE
			Vector3Int pawnPos = new Vector3Int(9, 10, 0);
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pawnPos);

			pawn.StartBuilding(new BuildEventArgs(pawn, basePos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should start building BASE");

			// Wait for BASE to complete
			Unit baseUnit = null;
			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				baseUnit = ctx.UnitManager.GetAllUnits().Values
					.Select(go => go.GetComponent<Unit>())
					.FirstOrDefault(u => u.UnitType == UnitType.BASE);
				return baseUnit != null && baseUnit.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "BASE did not complete");

			// Pawn is now idle; try building BARRACKS
			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 5f,
				failMessage: "Pawn did not go IDLE after building BASE");

			Vector3Int barracksPos = new Vector3Int(10, 5, 0);
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			pawn.StartBuilding(new BuildEventArgs(pawn, barracksPos, UnitType.BARRACKS));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should be able to build BARRACKS after BASE is complete");
			Assert.Less(agent.Gold, goldBefore,
				"Gold should be deducted when BARRACKS build is accepted");
		}

		/// <summary>
		/// After building a BASE, a pawn can then build a TOWER.
		/// </summary>
		[UnityTest]
		public IEnumerator AfterBase_PawnCanBuildTower()
		{
			Vector3Int pawnPos = new Vector3Int(9, 10, 0);
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, pawnPos);

			pawn.StartBuilding(new BuildEventArgs(pawn, basePos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should start building BASE");

			Unit baseUnit = null;
			yield return WaitUntil(() =>
			{
				TickUnit(pawn);
				baseUnit = ctx.UnitManager.GetAllUnits().Values
					.Select(go => go.GetComponent<Unit>())
					.FirstOrDefault(u => u.UnitType == UnitType.BASE);
				return baseUnit != null && baseUnit.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "BASE did not complete");

			yield return WaitUntil(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 5f,
				failMessage: "Pawn did not go IDLE after building BASE");

			Vector3Int towerPos = new Vector3Int(10, 5, 0);
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			pawn.StartBuilding(new BuildEventArgs(pawn, towerPos, UnitType.TOWER));

			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should be able to build TOWER after BASE is complete");
			Assert.Less(agent.Gold, goldBefore,
				"Gold should be deducted when TOWER build is accepted");
		}

		#endregion

		}
}
