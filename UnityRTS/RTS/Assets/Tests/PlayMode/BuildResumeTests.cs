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
	/// Play Mode tests for pausable/resumable building construction.
	/// Verifies that build progress is stored on the building itself,
	/// survives worker death or reassignment, and can be resumed by
	/// the same or a different worker.
	/// </summary>
	[TestFixture]
	public class BuildResumeTests : PlayModeTestBase
	{
		/// <summary>
		/// Find the first incomplete building of the given type in the unit registry.
		/// </summary>
		private Unit FindUnbuiltBuilding(UnitType type)
		{
			return ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u != null && u.UnitType == type && !u.IsBuilt);
		}

		#region Progress Survives Worker Death

		/// <summary>
		/// After a worker dies mid-construction, the incomplete building retains
		/// its accumulated build progress (BuildProgress > 0).
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_KilledDuringBuild_BuildingRetainsProgress()
		{
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, new Vector3Int(10, 10, 0), UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction, "Worker should start building");

			// Wait until the building exists and has accumulated some progress
			Unit building = null;
			yield return WaitUntil(() =>
			{
				building = FindUnbuiltBuilding(UnitType.BASE);
				return building != null && building.BuildProgress > 0f;
			}, timeoutSeconds: 15f, failMessage: "Building should exist with positive progress");

			float progressBeforeDeath = building.BuildProgress;
			Assert.Greater(progressBeforeDeath, 0f, "Build progress should be positive before death");

			// Kill the worker
			worker.Health = 0;
			worker.Update();
			yield return WaitFrames(2);

			// Building should retain its progress
			Assert.IsFalse(building.IsBuilt, "Building should not be complete yet");
			Assert.GreaterOrEqual(building.BuildProgress, progressBeforeDeath,
				$"Building should retain progress after worker death (was {progressBeforeDeath:F2}s)");
		}

		#endregion

		#region Another Worker Resumes

		/// <summary>
		/// After Worker A abandons a build (by moving away), Worker B can resume
		/// construction at the same position. The building completes successfully.
		/// </summary>
		[UnityTest]
		public IEnumerator AnotherWorker_CanResumeInterruptedBuild()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit workerA = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			workerA.StartBuilding(new BuildEventArgs(workerA, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, workerA.CurrentAction, "Worker A should start building");

			// Wait for some build progress to accumulate
			Unit building = null;
			yield return WaitUntil(() =>
			{
				building = FindUnbuiltBuilding(UnitType.BASE);
				return building != null && building.BuildProgress > 0f;
			}, timeoutSeconds: 15f, failMessage: "Build progress should have started");

			float progressAtInterrupt = building.BuildProgress;

			// Worker A moves away, abandoning the build (progress preserved on building)
			workerA.StartMoving(new MoveEventArgs(workerA, UnitType.WORKER, new Vector3Int(1, 1, 0)));
			Assert.AreEqual(UnitAction.MOVE, workerA.CurrentAction, "Worker A should switch to MOVE");

			yield return WaitFrames(3);

			// Worker B starts building at the same position — should resume rather than reject
			Unit workerB = PlaceUnit(UnitType.WORKER, new Vector3Int(8, 10, 0));
			workerB.StartBuilding(new BuildEventArgs(workerB, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, workerB.CurrentAction,
				"Worker B should resume building the incomplete BASE");

			// Wait for the building to complete
			yield return WaitUntil(
				() => building != null && building.IsBuilt,
				timeoutSeconds: 30f,
				failMessage: "Building should complete after Worker B resumes");

			Assert.IsTrue(building.IsBuilt, "BASE should be fully built after resume");
		}

		#endregion

		#region Same Worker Resumes

		/// <summary>
		/// A worker that moves away mid-build can be sent back to resume construction.
		/// Progress is preserved and the building completes.
		/// </summary>
		[UnityTest]
		public IEnumerator Worker_InterruptedBuild_CanResumeSelf()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit worker = PlaceUnit(UnitType.WORKER, new Vector3Int(9, 10, 0));
			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction);

			// Wait for build progress to accumulate
			Unit building = null;
			yield return WaitUntil(() =>
			{
				building = FindUnbuiltBuilding(UnitType.BASE);
				return building != null && building.BuildProgress > 0f;
			}, timeoutSeconds: 15f, failMessage: "Build progress should have started");

			float progressAtInterrupt = building.BuildProgress;

			// Interrupt with a move — building retains its progress
			worker.StartMoving(new MoveEventArgs(worker, UnitType.WORKER, new Vector3Int(1, 10, 0)));
			Assert.AreEqual(UnitAction.MOVE, worker.CurrentAction,
				"Worker should switch from BUILD to MOVE");
			Assert.GreaterOrEqual(building.BuildProgress, progressAtInterrupt,
				"Building should retain progress after move interrupts build");

			// Wait for worker to be idle
			yield return WaitUntil(
				() => worker.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Worker should become idle after reaching move target");

			// Worker resumes the same build
			worker.StartBuilding(new BuildEventArgs(worker, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, worker.CurrentAction,
				"Worker should be able to resume its own interrupted build");

			// Wait for completion
			yield return WaitUntil(
				() => building.IsBuilt,
				timeoutSeconds: 30f,
				failMessage: "Building should complete after worker resumes");

			Assert.IsTrue(building.IsBuilt, "BASE should be fully built after self-resume");
		}

		#endregion
	}
}
