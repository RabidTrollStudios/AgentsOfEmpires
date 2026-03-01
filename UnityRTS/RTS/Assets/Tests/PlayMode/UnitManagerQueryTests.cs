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

			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Assert.AreEqual(1, ctx.UnitManager.GetAllUnits().Count);

			PlaceUnit(UnitType.SOLDIER, new Vector3Int(6, 5, 0));
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
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(15, 15, 0), ctx.Agent1Go);

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
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			int nbr = worker.UnitNbr;

			Unit retrieved = ctx.UnitManager.GetUnit(nbr);
			Assert.IsNotNull(retrieved);
			Assert.AreEqual(nbr, retrieved.UnitNbr);
			Assert.AreEqual(UnitType.WORKER, retrieved.UnitType);

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
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			int nbr = worker.UnitNbr;

			worker.Health = 0;
			worker.Update();
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
		public IEnumerator GetUnitNbrsOfType_AllWorkers_ReturnsAll()
		{
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(6, 5, 0));
			PlaceUnit(UnitType.WORKER, new Vector3Int(15, 5, 0), ctx.Agent1Go);
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(7, 5, 0));

			var workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER);
			Assert.AreEqual(3, workers.Count,
				"GetUnitNbrsOfType(WORKER) should return 3 workers (both agents)");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType with agent filter returns only that agent's units.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_AgentFilter_OnlyThatAgent()
		{
			Unit w0 = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(15, 5, 0), ctx.Agent1Go);

			int agent0Nbr = w0.Agent.GetComponent<AgentController>().Agent.AgentNbr;
			int agent1Nbr = w1.Agent.GetComponent<AgentController>().Agent.AgentNbr;

			var agent0Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent0Nbr);
			var agent1Workers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER, agent1Nbr);

			Assert.AreEqual(1, agent0Workers.Count);
			Assert.AreEqual(1, agent1Workers.Count);
			Assert.AreNotEqual(agent0Workers[0], agent1Workers[0],
				"Different agents should have different UnitNbrs for their workers");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType returns empty list when no units of that type exist.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_NoUnitsOfType_ReturnsEmpty()
		{
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			var soldiers = ctx.UnitManager.GetUnitNbrsOfType(UnitType.SOLDIER);
			Assert.AreEqual(0, soldiers.Count,
				"Should return empty list when no SOLDIERs exist");

			yield return null;
		}

		/// <summary>
		/// GetUnitNbrsOfType count decrements when a unit of that type is destroyed.
		/// </summary>
		[UnityTest]
		public IEnumerator GetUnitNbrsOfType_AfterDestruction_CountDecreases()
		{
			Unit w1 = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			Unit w2 = PlaceUnit(UnitType.WORKER, new Vector3Int(6, 5, 0));

			Assert.AreEqual(2, ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER).Count);

			w1.Health = 0;
			w1.Update();
			yield return null;

			Assert.AreEqual(1, ctx.UnitManager.GetUnitNbrsOfType(UnitType.WORKER).Count,
				"Worker count should decrease after one is destroyed");
		}

		#endregion

		#region DestroyAllUnits

		/// <summary>
		/// DestroyAllUnits removes all units from UnitManager.
		/// </summary>
		[UnityTest]
		public IEnumerator DestroyAllUnits_EmptiesUnitManager()
		{
			PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));
			PlaceUnit(UnitType.SOLDIER, new Vector3Int(6, 5, 0));
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
