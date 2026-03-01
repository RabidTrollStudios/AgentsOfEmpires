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

		}
}
