using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for UnitManager query API:
	/// GetAllUnits, GetUnit, GetUnitNbrsOfType, and DestroyAllUnits.
	/// </summary>
	[TestFixture]
	public class UnitManagerQueryTests : PlayModeTestBase
	{
		#region GetAllUnits

		/// <summary>
		/// GetAllUnits count increments for each unit placed.
		/// </summary>
		[UnityTest]
		public IEnumerator GetAllUnits_CountMatchesPlacedUnits()
		{
			Assert.AreEqual(0, ctx.UnitManager.GetAllUnits().Count,
				"UnitManager should start empty");

			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Assert.AreEqual(1, ctx.UnitManager.GetAllUnits().Count);

			PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 5, 0));
			Assert.AreEqual(2, ctx.UnitManager.GetAllUnits().Count);

			PlaceUnit(UnitType.ARCHER, new Vector3Int(7, 5, 0));
			Assert.AreEqual(3, ctx.UnitManager.GetAllUnits().Count);

			yield return null;
		}

		/// <summary>
		/// GetAllUnits contains units placed for both agents.
		/// </summary>
		[UnityTest]
		public IEnumerator GetAllUnits_IncludesBothAgents()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(15, 15, 0), ctx.Agent1Go);

			Assert.AreEqual(2, ctx.UnitManager.GetAllUnits().Count,
				"Both agents' units should appear in GetAllUnits");

			yield return null;
		}

		#endregion

		#region GetUnit

		/// <summary>
		/// GetUnit returns the correct unit by UnitNbr.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnit_ByUnitNbr_ReturnsCorrectUnit()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			int nbr = pawn.UnitNbr;

			Unit retrieved = ctx.UnitManager.GetUnit(nbr);
			Assert.IsNotNull(retrieved);
			Assert.AreEqual(nbr, retrieved.UnitNbr);
			Assert.AreEqual(UnitType.PAWN, retrieved.UnitType);

			yield return null;
		}

		/// <summary>
		/// GetUnit returns null for a nonexistent UnitNbr.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnit_InvalidNbr_ReturnsNull()
		{
			Unit result = ctx.UnitManager.GetUnit(99999);
			Assert.IsNull(result, "GetUnit should return null for nonexistent unit number");

			yield return null;
		}

		/// <summary>
		/// GetUnit returns null after a unit is destroyed.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnit_AfterDestruction_ReturnsNull()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			int nbr = pawn.UnitNbr;

			pawn.Health = 0;
			pawn.TickFixedUpdate();
			yield return null;

			Assert.IsNull(ctx.UnitManager.GetUnit(nbr),
				"GetUnit should return null after unit is destroyed");
		}

		#endregion

		#region GetUnitNbrsOfType

		/// <summary>
		/// GetUnitNbrsOfType returns all units of a given type across both agents.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_AllPawns_ReturnsAll()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));
			PlaceUnit(UnitType.PAWN, new Vector3Int(15, 5, 0), ctx.Agent1Go);
			PlaceUnit(UnitType.WARRIOR, new Vector3Int(7, 5, 0));

			var pawns = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN);
			Assert.AreEqual(3, pawns.Count,
				"GetUnitNbrsOfType(PAWN) should return 3 pawns (both agents)");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType with agent filter returns only that agent's units.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_AgentFilter_OnlyThatAgent()
		{
			Unit w0 = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(15, 5, 0), ctx.Agent1Go);

			int agent0Nbr = w0.Agent.GetComponent<AgentController>().Agent.AgentNbr;
			int agent1Nbr = w1.Agent.GetComponent<AgentController>().Agent.AgentNbr;

			var agent0Pawns = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN, agent0Nbr);
			var agent1Pawns = ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN, agent1Nbr);

			Assert.AreEqual(1, agent0Pawns.Count);
			Assert.AreEqual(1, agent1Pawns.Count);
			Assert.AreNotEqual(agent0Pawns[0], agent1Pawns[0],
				"Different agents should have different UnitNbrs for their pawns");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType returns empty list when no units of that type exist.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_NoUnitsOfType_ReturnsEmpty()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			var warriors = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WARRIOR);
			Assert.AreEqual(0, warriors.Count,
				"Should return empty list when no WARRIORs exist");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType count decrements when a unit of that type is destroyed.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_AfterDestruction_CountDecreases()
		{
			Unit w1 = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			Unit w2 = PlaceUnit(UnitType.PAWN, new Vector3Int(6, 5, 0));

			Assert.AreEqual(2, ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN).Count);

			w1.Health = 0;
			w1.TickFixedUpdate();
			yield return null;

			Assert.AreEqual(1, ctx.UnitManager.GetUnitNbrsOfType(UnitType.PAWN).Count,
				"Pawn count should decrease after one is destroyed");
		}

		#endregion

		#region DestroyAllUnits

		/// <summary>
		/// DestroyAllUnits removes all units from UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator DestroyAllUnits_EmptiesUnitManager()
		{
			PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WARRIOR, new Vector3Int(6, 5, 0));
			PlaceUnit(UnitType.ARCHER, new Vector3Int(7, 5, 0));

			Assert.AreEqual(3, ctx.UnitManager.GetAllUnits().Count);

			ctx.UnitManager.DestroyAllUnits();
			yield return null;

			Assert.AreEqual(0, ctx.UnitManager.GetAllUnits().Count,
				"All units should be removed after DestroyAllUnits");
		}

		#endregion
	}
}
