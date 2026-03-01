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
	/// BARRACKS and REFINERY require a BASE to be built first.
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
		/// After building a BASE, a worker can then build a BARRACKS.
		/// </summary>
		[UnityTest]
		public IEnumerator AfterBase_WorkerCanBuildBarracks()
		{
			// Place and complete a BASE
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			worker.StartBuilding(new BuildEventArgs(worker, basePos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should start building BASE");

			// Wait for BASE to complete
			Unit baseUnit = null;
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				baseUnit = ctx.UnitManager.GetAllUnits().Values
					.Select(go => go.GetComponent<Unit>())
					.FirstOrDefault(u => u.UnitType == UnitType.BASE);
				return baseUnit != null && baseUnit.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "BASE did not complete");

			// Worker is now idle; try building BARRACKS
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 5f,
				failMessage: "Worker did not go IDLE after building BASE");

			Vector3Int barracksPos = new Vector3Int(10, 5, 0);
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, barracksPos, UnitType.BARRACKS));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should be able to build BARRACKS after BASE is complete");
			Assert.Less(agent.Gold, goldBefore,
				"Gold should be deducted when BARRACKS build is accepted");
		}

		/// <summary>
		/// After building a BASE, a worker can also build a REFINERY.
		/// </summary>
		[UnityTest]
		public IEnumerator AfterBase_WorkerCanBuildRefinery()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Vector3Int basePos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);

			worker.StartBuilding(new BuildEventArgs(worker, basePos, UnitType.BASE));

			Unit baseUnit = null;
			yield return WaitUntil(() =>
			{
				TickUnit(worker);
				baseUnit = ctx.UnitManager.GetAllUnits().Values
					.Select(go => go.GetComponent<Unit>())
					.FirstOrDefault(u => u.UnitType == UnitType.BASE);
				return baseUnit != null && baseUnit.IsBuilt;
			}, timeoutSeconds: 10f, failMessage: "BASE did not complete");

			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 5f,
				failMessage: "Worker did not go IDLE after BASE");

			Vector3Int refineryPos = new Vector3Int(10, 5, 0);
			worker.StartBuilding(new BuildEventArgs(worker, refineryPos, UnitType.REFINERY));

			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should be able to build REFINERY after BASE is complete");
		}

		#endregion

		#region Error (Dependency not met)

		/// <summary>
		/// Without a BASE, building a BARRACKS should be rejected.
		/// Gold remains unchanged.
		/// </summary>
		[UnityTest]
		public IEnumerator WithoutBase_BuildBarracks_Rejected()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BARRACKS));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Building BARRACKS without BASE should be rejected");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when BARRACKS build is rejected");

			yield return null;
		}

		/// <summary>
		/// Without a BASE, building a REFINERY should be rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator WithoutBase_BuildRefinery_Rejected()
		{
			Vector3Int workerPos = new Vector3Int(9, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, workerPos);
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.REFINERY));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"Building REFINERY without BASE should be rejected");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted when REFINERY build is rejected");

			yield return null;
		}

		/// <summary>
		/// An unbuilt (under construction) BASE does not satisfy the dependency check.
		/// Building BARRACKS while BASE is under construction should be rejected.
		/// </summary>
		[UnityTest]
		public IEnumerator UnbuiltBase_DoesNotSatisfyDependency_BarracksRejected()
		{
			// Place the BASE but do NOT mark it as built
			Unit basePlaceholder = PlaceUnit(UnitType.BASE, new Vector3Int(10, 10, 0));
			Assert.IsFalse(basePlaceholder.IsBuilt,
				"Freshly placed BASE should not be built yet");

			// Try to build BARRACKS while BASE is unbuilt
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 5, 0));
			Agent agent = GetAgent0();
			int goldBefore = agent.Gold;

			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 5, 0), UnitType.BARRACKS));

			Assert.AreNotEqual(UnitAction.BUILD, worker.CurrentAction,
				"BARRACKS should not be buildable while BASE is still under construction");
			Assert.AreEqual(goldBefore, agent.Gold,
				"Gold should not be deducted");

			yield return null;
		}

		#endregion
	}
}
